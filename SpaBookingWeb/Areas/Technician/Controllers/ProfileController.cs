using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Technictian;

namespace SpaBookingWeb.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")] // [THAY ĐỔI IDENTITY]
    public class ProfileController : Controller
    {
        private readonly ITechnicianJobService _jobService;
        private readonly UserManager<ApplicationUser> _userManager; // [THAY ĐỔI IDENTITY] Thêm UserManager

        public ProfileController(ITechnicianJobService jobService, UserManager<ApplicationUser> userManager)
        {
            _jobService = jobService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // [THAY ĐỔI IDENTITY] Lấy User hiện tại
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            // [THAY ĐỔI IDENTITY] Lấy EmployeeId thật
            var employeeId = await _jobService.GetCurrentEmployeeIdAsync(user.Id);

            if (employeeId == null) return NotFound("Không tìm thấy liên kết nhân viên.");

            var profile = await _jobService.GetTechnicianProfileAsync(employeeId.Value);

            if (profile == null)
            {
                return NotFound("Không tìm thấy hồ sơ nhân viên.");
            }

            return View(profile);
        }
    }
}
