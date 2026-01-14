using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Technictian;
using System.Security.Claims;

namespace SpaBookingWeb.Areas.Technician.Filters
{
    public class RequireAttendanceFilter : IAsyncActionFilter
    {
        private readonly ITechnicianJobService _jobService;
        private readonly UserManager<ApplicationUser> _userManager; // [THÊM]

        public RequireAttendanceFilter(ITechnicianJobService jobService, UserManager<ApplicationUser> userManager)
        {
            _jobService = jobService;
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1. TRÁNH VÒNG LẶP VÔ TẬN
            // Nếu đang ở trang Login hoặc trang Điểm danh thì cho qua luôn
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            if (controller == "Attendance" || controller == "Account")
            {
                await next();
                return;
            }

            // 2. [IDENTITY] LẤY USER HIỆN TẠI
            var userPrincipal = context.HttpContext.User;

            // Nếu chưa đăng nhập -> Đá về trang Login
            if (userPrincipal == null || !userPrincipal.Identity.IsAuthenticated)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
                return;
            }

            // Lấy UserId (Guid String)
            var userId = _userManager.GetUserId(userPrincipal);

            // Lấy EmployeeId (Int) từ Service
            var employeeId = await _jobService.GetCurrentEmployeeIdAsync(userId);

            // Nếu tài khoản này không phải nhân viên (không tìm thấy ID) -> Báo lỗi hoặc về Home
            if (employeeId == null)
            {
                var controllerObj = context.Controller as Controller;
                if (controllerObj != null)
                {
                    controllerObj.TempData["ErrorMessage"] = "Tài khoản của bạn chưa được liên kết hồ sơ Kỹ thuật viên!";
                }
                context.Result = new RedirectToActionResult("Index", "Home", new { area = "" }); // Hoặc trang lỗi
                return;
            }

            // 2. Kiểm tra đã Check-in chưa
            var isCheckedIn = await _jobService.IsCheckedInTodayAsync(employeeId.Value);

            if (!isCheckedIn)
            {
                // 3. Chặn và chuyển hướng
                var controllerObj = context.Controller as Controller;
                if (controllerObj != null)
                {
                    controllerObj.TempData["WarningMessage"] = "Vui lòng <b>Check-in (Vào ca)</b> trước khi bắt đầu làm việc!";
                }

                context.Result = new RedirectToActionResult("Index", "Attendance", new { area = "Technician" });
                return;
            }

            await next();
        }
    }
}
