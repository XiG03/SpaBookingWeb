using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Technictian;

namespace SpaBookingWeb.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")] // [THAY ĐỔI IDENTITY]
    public class AttendanceController : Controller
    {
        private readonly ITechnicianJobService _jobService;
        private readonly UserManager<ApplicationUser> _userManager; // [THAY ĐỔI IDENTITY]

        public AttendanceController(ITechnicianJobService jobService, UserManager<ApplicationUser> userManager)
        {
            _jobService = jobService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // [THAY ĐỔI IDENTITY] Lấy ID thật
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            var employeeId = await _jobService.GetCurrentEmployeeIdAsync(user.Id);
            if (employeeId == null) return Content("Error: Employee records not found.");

            var schedule = await _jobService.GetTodayScheduleAsync(employeeId.Value);
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAttendance()
        {
            // [THAY ĐỔI IDENTITY] Lấy ID thật
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            var employeeId = await _jobService.GetCurrentEmployeeIdAsync(user.Id);
            if (employeeId == null) return Content("Error: Employee records not found.");

            // Lấy IP người dùng
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            // Nếu chạy localhost nó ra ::1, ta map về 127.0.0.1 cho dễ nhìn (nếu muốn)
            if (ipAddress == "::1") ipAddress = "127.0.0.1";

            var error = await _jobService.PerformAttendanceAsync(employeeId.Value, ipAddress);

            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Index");
            }
            else
            {
                TempData["SuccessMessage"] = "Attendance check successful!";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
