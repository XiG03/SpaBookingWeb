using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SpaBookingWeb.Services.Technictian;
using System.Security.Claims;

namespace SpaBookingWeb.Areas.Technician.Filters
{
    public class RequireAttendanceFilter : IAsyncActionFilter
    {
        private readonly ITechnicianJobService _jobService;

        public RequireAttendanceFilter(ITechnicianJobService jobService)
        {
            _jobService = jobService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1. Lấy User ID
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // --- GIẢ LẬP ID = 1 ĐỂ TEST (Xóa khi login thật hoạt động) ---
            int employeeId = 3;
            // ------------------------------------------------------------

            /* Logic thật sau này:
            if (userId != null) {
               var empId = await _jobService.GetCurrentEmployeeIdAsync(userId);
               if (empId != null) employeeId = empId.Value;
            }
            */

            // 2. Kiểm tra đã Check-in chưa
            var isCheckedIn = await _jobService.IsCheckedInTodayAsync(employeeId);

            if (!isCheckedIn)
            {
                // Lấy Controller và Action hiện tại để tránh vòng lặp vô tận
                var controller = context.RouteData.Values["controller"]?.ToString();
                var action = context.RouteData.Values["action"]?.ToString();

                // Nếu đang ở trang Attendance rồi thì thôi, không chặn nữa
                if (controller == "Attendance")
                {
                    await next();
                    return;
                }

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
