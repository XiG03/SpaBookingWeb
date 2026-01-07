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

            // Lấy dữ liệu từ Service
            var history = await _bookingService.GetBookingHistoryAsync(user.Email);

            // Truyền cả thông tin User để hiển thị ở Sidebar
            ViewData["UserName"] = user.FullName ?? user.UserName;
            ViewData["UserEmail"] = user.Email;

            return View(history);
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointmentDetail(int id)
        {
            var detail = await _bookingService.GetAppointmentDetailAsync(id);
            if (detail == null) return NotFound();

            // Trả về JSON để JavaScript xử lý hiển thị lên Modal
            return Json(detail);
        }

        // [MỚI] Trang Lịch sử (Đã xong/Hủy)
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

        // [MỚI] Chức năng Đặt lại
        [HttpPost]
        public async Task<IActionResult> Rebook(int id)
        {
            var result = await _bookingService.RebookAsync(id);
            if (result)
            {
                // Chuyển hướng đến bước 2 (Chọn dịch vụ) với dữ liệu đã được nạp sẵn
                return RedirectToAction("Step2_Services", "Booking");
            }
            return RedirectToAction("History");
        }
    }


}
