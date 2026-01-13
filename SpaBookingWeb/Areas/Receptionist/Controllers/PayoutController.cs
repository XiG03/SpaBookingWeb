using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Areas.Receptionist.Filters;
using SpaBookingWeb.Data;
using SpaBookingWeb.ViewModels.Receptionist;

namespace SpaBookingWeb.Areas.Receptionist.Controllers
{
    [Area("Receptionist")]
    [Authorize(Roles = "Receptionist,Admin,Manager")]
    // [THÊM DÒNG NÀY] Chặn toàn bộ hành động trong Controller này nếu chưa Check-in
    [ServiceFilter(typeof(RequireReceptionistAttendanceFilter))]
    public class PayoutController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PayoutController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. MÀN HÌNH CHÍNH: DANH SÁCH CẦN TRẢ
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Lấy tất cả các khoản Tip thỏa mãn điều kiện:
            // - Có tiền (>0)
            // - Lễ tân giữ (IsDirectTip == false)
            // - Chưa trả (TipPayoutDay == null)
            // - Đơn hàng đã hoàn thành hoặc đang làm (Status != Cancelled)
            var query = _context.AppointmentDetails
                .Include(ad => ad.Technician)
                .Where(ad => ad.TipAmount > 0
                             && ad.IsDirectTip == false
                             && ad.TipPayoutDate == null
                             && ad.Status != "Cancelled");

            // Gom nhóm theo KTV
            var rawData = await query.ToListAsync(); // Lấy về RAM để GroupBy cho dễ

            var summaryList = rawData
                .Where(x => x.TechnicianId.HasValue)
                .GroupBy(x => x.TechnicianId)
                .Select(g => new TechPayoutSummaryVM
                {
                    // [SỬA LỖI TẠI ĐÂY] Chuyển đổi int? sang int
                    TechnicianId = g.Key.Value,

                    TechnicianName = g.First().Technician?.FullName ?? "Không tên",
                    Avatar = g.First().Technician?.Avatar ?? "/images/default-avatar.png",
                    TotalTransactionCount = g.Count(),
                    TotalUnpaidTip = g.Sum(x => x.TipAmount)
                })
                .OrderByDescending(x => x.TotalUnpaidTip)
                .ToList();

            return View(summaryList);
        }

        // 2. API: LẤY CHI TIẾT ĐỂ ĐỐI SOÁT (HIỆN MODAL)
        [HttpGet]
        public async Task<IActionResult> GetPayoutDetails(int techId)
        {
            var details = await _context.AppointmentDetails
                .Include(ad => ad.Appointment).ThenInclude(a => a.Customer)
                .Include(ad => ad.Service)
                .Include(ad => ad.Combo)
                .Where(ad => ad.TechnicianId == techId
                             && ad.TipAmount > 0
                             && ad.IsDirectTip == false
                             && ad.TipPayoutDate == null
                             && ad.Status != "Cancelled")
                .OrderByDescending(ad => ad.Appointment.StartTime)
                .Select(ad => new PayoutDetailItemVM
                {
                    DetailId = ad.AppointmentDetailId,
                    Date = ad.Appointment.StartTime,
                    CustomerName = ad.GuestName ?? ad.Appointment.Customer.FullName,
                    ServiceName = ad.ComboId != null ? $"[Combo] {ad.Combo.ComboName}" : ad.Service.ServiceName,
                    TipAmount = ad.TipAmount
                })
                .ToListAsync();

            return Json(new { success = true, data = details });
        }

        // 3. API: XÁC NHẬN THANH TOÁN (PAY ALL)
        [HttpPost]
        public async Task<IActionResult> ConfirmPayout(int techId)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Tìm lại các khoản nợ để chốt (tránh việc giữa lúc xem và lúc bấm có thay đổi)
                var itemsToPay = await _context.AppointmentDetails
                    .Where(ad => ad.TechnicianId == techId
                                 && ad.TipAmount > 0
                                 && ad.IsDirectTip == false
                                 && ad.TipPayoutDate == null)
                    .ToListAsync();

                if (!itemsToPay.Any())
                {
                    return Json(new { success = false, message = "Không còn khoản nào cần thanh toán cho KTV này." });
                }

                DateTime now = DateTime.Now;
                decimal totalPaid = 0;

                // Cập nhật ngày trả
                foreach (var item in itemsToPay)
                {
                    item.TipPayoutDate = now;
                    totalPaid += item.TipAmount;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã quyết toán thành công {totalPaid:N0}đ ({itemsToPay.Count} đơn)!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}
