using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Receptionist;

namespace SpaBookingWeb.Areas.Receptionist.Filters
{
    public class RequireReceptionistAttendanceFilter : IAsyncActionFilter
    {
        private readonly IReceptionistService _receptionistService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public RequireReceptionistAttendanceFilter(
            IReceptionistService receptionistService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _receptionistService = receptionistService;
            _userManager = userManager;
            _context = context;
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

            // 2. [CẬP NHẬT IDENTITY] LẤY ID LỄ TÂN TỪ USER ĐANG ĐĂNG NHẬP
            var user = context.HttpContext.User;

            // Nếu chưa đăng nhập -> Chuyển về trang Login
            if (user == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
                return;
            }

            var userId = _userManager.GetUserId(user);
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == userId);

            // Nếu tài khoản này không phải là nhân viên (hoặc chưa liên kết) -> Chặn
            if (emp == null)
            {
                // Tùy chọn: Chuyển hướng về trang lỗi hoặc thông báo
                var controllerObj = context.Controller as Controller;
                if (controllerObj != null)
                {
                    controllerObj.TempData["ErrorMessage"] = "Tài khoản của bạn chưa được liên kết với hồ sơ nhân viên!";
                }
                // Redirect tạm về trang Home hoặc Login
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
                return;
            }

            // 3. KIỂM TRA TRẠNG THÁI CHECK-IN
            // Gọi hàm lấy lịch hôm nay mà ta vừa viết ở bước trước
            var schedule = await _receptionistService.GetTodayScheduleAsync(emp.EmployeeId);

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
