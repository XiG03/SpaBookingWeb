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
    public class BookingListController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingListController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. MÀN HÌNH CHÍNH
        public IActionResult Index()
        {
            return View();
        }

        // 2. API LẤY DANH SÁCH (CÓ LỌC)
        [HttpGet]
        public async Task<IActionResult> GetBookings(string fromDate, string toDate, string status, string keyword)
        {
            // Query cơ bản (Không lấy Deleted)
            var query = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.Employee) // Lấy lễ tân tạo đơn
                .Where(a => !a.IsDeleted)
                .AsQueryable();

            // A. Lọc theo ngày
            if (DateTime.TryParse(fromDate, out DateTime start))
            {
                query = query.Where(a => a.StartTime.Date >= start.Date);
            }
            if (DateTime.TryParse(toDate, out DateTime end))
            {
                query = query.Where(a => a.StartTime.Date <= end.Date);
            }

            // B. Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(a => a.Status == status);
            }

            // C. Tìm kiếm từ khóa (SĐT, Tên, Mã đơn)
            if (!string.IsNullOrEmpty(keyword))
            {
                string k = keyword.ToLower().Trim();
                query = query.Where(a => a.Customer.PhoneNumber.Contains(k)
                                      || a.Customer.FullName.ToLower().Contains(k)
                                      || a.AppointmentId.ToString() == k);
            }

            // D. Sắp xếp: Mới nhất lên đầu
            var rawData = await query.OrderByDescending(a => a.StartTime).ToListAsync();

            // E. Map sang ViewModel
            var result = rawData.Select(a => {
                // Logic tạo text tóm tắt dịch vụ
                var details = a.AppointmentDetails.ToList();
                string summary = "Chưa chọn dịch vụ";
                if (details.Any())
                {
                    string firstSvc = details.First().Service?.ServiceName ?? "Combo";
                    int remain = details.Count - 1;
                    summary = remain > 0 ? $"{firstSvc} (+{remain} món)" : firstSvc;
                }

                return new BookingListItemVM
                {
                    AppointmentId = a.AppointmentId,
                    CustomerName = a.Customer?.FullName ?? "Vãng lai",
                    CustomerPhone = a.Customer?.PhoneNumber ?? "",
                    BookingTime = a.StartTime,
                    ServiceSummary = summary,
                    // Tính tổng tiền dự kiến
                    TotalAmount = details.Sum(d => d.PriceAtBooking),
                    DepositAmount = a.DepositAmount,
                    Status = a.Status,
                    CreatedBy = a.Employee?.FullName ?? "System"
                };
            }).ToList();

            return Json(new { success = true, data = result });
        }
    }
}
