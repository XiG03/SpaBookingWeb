using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Areas.Technician.Filters;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Technictian;
using System.Diagnostics;
using System.Text.Json;

namespace SpaBookingWeb.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")] // [THAY ĐỔI IDENTITY]
    [TypeFilter(typeof(RequireAttendanceFilter))] // <--- THÊM DÒNG NÀY
    public class HomeController : Controller
    {
        private readonly ITechnicianJobService _jobService;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ITechnicianJobService jobService, UserManager<ApplicationUser> userManager)
        {
            _jobService = jobService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // [THAY ĐỔI IDENTITY] 1. Kiểm tra đăng nhập và lấy User
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            // [THAY ĐỔI IDENTITY] 2. Lấy ID Nhân viên thật
            var employeeId = await _jobService.GetCurrentEmployeeIdAsync(user.Id);
            if (employeeId == null)
            {
                return Content("Error: This account is not linked to an employee profile.");
            }

            // 3. Lấy dữ liệu Lịch (Tháng hiện tại)
            var today = DateTime.Today;
            var events = await _jobService.GetCalendarEventsAsync(employeeId.Value, today.Month, today.Year);

            // 4. Đóng gói dữ liệu thành JSON gửi xuống View
            ViewBag.EventsJson = JsonSerializer.Serialize(events);

            return View();
        }

        // --- THÊM HÀM NÀY ĐỂ SỬA LỖI MÀN HÌNH ERROR ---
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Sử dụng ErrorViewModel có sẵn trong project
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
