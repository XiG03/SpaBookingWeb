using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Controllers
{
    [Authorize] // Bắt buộc đăng nhập
    public class ProfileController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(IBookingService bookingService, UserManager<ApplicationUser> userManager)
        {
            _bookingService = bookingService;
            _userManager = userManager;
        }

        // Action hiển thị danh sách lịch hẹn (Trang mặc định của profile)
        public async Task<IActionResult> Appointments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var history = await _bookingService.GetBookingHistoryAsync(user.Email);

            ViewData["UserName"] = user.FullName ?? user.UserName;
            ViewData["UserEmail"] = user.Email;

            return View(history);
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointmentDetail(int id)
        {
            var detail = await _bookingService.GetAppointmentDetailAsync(id);
            if (detail == null) return NotFound();
            return Json(detail);
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var history = await _bookingService.GetBookingHistoryArchiveAsync(user.Email);

            ViewData["UserName"] = user.FullName ?? user.UserName;
            ViewData["UserEmail"] = user.Email;

            return View(history);
        }

        // [CŨ] Đặt lại từ đầu (Step 2)
        [HttpPost]
        public async Task<IActionResult> Rebook(int id)
        {
            var result = await _bookingService.RebookAsync(id);
            if (result)
            {
                return RedirectToAction("Step2_Services", "Booking");
            }
            return RedirectToAction("History");
        }

        // [MỚI] Tiếp tục thanh toán (Step 5) cho đơn Pending
        [HttpPost]
        public async Task<IActionResult> ContinueBooking(int id)
        {
            // Gọi hàm Resume để nạp dữ liệu cũ vào Session
            var result = await _bookingService.ResumeBookingAsync(id);
            if (result)
            {
                // Chuyển hướng thẳng đến bước xác nhận/thanh toán
                return RedirectToAction("Step5_Confirm", "Booking");
            }
            // Nếu lỗi, quay lại trang danh sách
            return RedirectToAction("Appointments");
        }
    }
}