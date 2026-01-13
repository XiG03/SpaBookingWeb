using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Receptionist;

namespace SpaBookingWeb.Areas.Receptionist.Controllers
{
    [Area("Receptionist")]
    [Authorize(Roles = "Receptionist,Admin,Manager")] // [THAY ĐỔI IDENTITY] Bật bảo mật
    public class AttendanceController : Controller
    {
        private readonly IReceptionistService _receptionistService;
        private readonly UserManager<ApplicationUser> _userManager; // [THAY ĐỔI IDENTITY]
        private readonly ApplicationDbContext _context; // [THAY ĐỔI IDENTITY]

        public AttendanceController(IReceptionistService receptionistService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _receptionistService = receptionistService;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // [THAY ĐỔI IDENTITY] 1. Lấy User
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            // [THAY ĐỔI IDENTITY] 2. Tìm EmployeeId tương ứng
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == user.Id);
            if (emp == null)
            {
                return Content("Lỗi: Tài khoản này chưa được liên kết với hồ sơ nhân viên.");
            }

            var schedule = await _receptionistService.GetTodayScheduleAsync(emp.EmployeeId);
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAttendance()
        {
            // [THAY ĐỔI IDENTITY] 1. Lấy User
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            // [THAY ĐỔI IDENTITY] 2. Tìm EmployeeId
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == user.Id);
            if (emp == null)
            {
                return Content("Lỗi: Tài khoản chưa liên kết nhân viên.");
            }

            // Lấy IP người dùng
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (ipAddress == "::1") ipAddress = "127.0.0.1";

            var error = await _receptionistService.PerformAttendanceAsync(emp.EmployeeId, ipAddress);

            if (error != null)
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = "Điểm danh thành công!";
            }

            return RedirectToAction("Index");
        }
    }
}
