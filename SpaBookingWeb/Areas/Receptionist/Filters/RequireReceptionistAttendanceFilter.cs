using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SpaBookingWeb.Services.Receptionist;

namespace SpaBookingWeb.Areas.Receptionist.Filters
{
    public class RequireReceptionistAttendanceFilter : IAsyncActionFilter
    {
        private readonly IReceptionistService _receptionistService;

        public RequireReceptionistAttendanceFilter(IReceptionistService receptionistService)
        {
            _receptionistService = receptionistService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1. TRÁNH VÒNG LẶP VÔ TẬN
            // Nếu người dùng đang truy cập trang Attendance rồi thì cho qua, không chặn nữa
            var controller = context.RouteData.Values["controller"]?.ToString();
            if (controller == "Attendance")
            {
                await next();
                return;
            }

            // 2. LẤY ID LỄ TÂN (Giả lập ID = 6 để đồng bộ với code test của bạn)
            // Sau này thay bằng logic lấy từ User.Identity
            int employeeId = 6;

            // 3. KIỂM TRA TRẠNG THÁI CHECK-IN
            // Gọi hàm lấy lịch hôm nay mà ta vừa viết ở bước trước
            var schedule = await _receptionistService.GetTodayScheduleAsync(employeeId);

            // Logic chặn:
            // - Nếu không có lịch làm việc (schedule == null) -> Chặn
            // - Hoặc có lịch nhưng chưa bấm nút Check-in (CheckInTime == null) -> Chặn
            if (schedule == null || schedule.CheckInTime == null)
            {
                // Gán thông báo nhắc nhở
                var controllerObj = context.Controller as Controller;
                if (controllerObj != null)
                {
                    string msg = schedule == null
                        ? "You don't have a shift today!"
                        : "Please <b>Check-in</b> before starting work!";

                    controllerObj.TempData["WarningMessage"] = msg;
                }

                // Chuyển hướng (Redirect) về trang Chấm công
                context.Result = new RedirectToActionResult("Index", "Attendance", new { area = "Receptionist" });
                return;
            }

            // Nếu đã Check-in rồi -> Cho phép đi tiếp
            await next();
        }
    }
}
