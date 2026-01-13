using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Receptionist;
using System.Security.Claims;

namespace SpaBookingWeb.Areas.Receptionist.Controllers
{
    [Area("Receptionist")]
    [Authorize(Roles = "Receptionist,Admin")] // [THAY ĐỔI IDENTITY] Bật bảo mật
    public class ProfileController : Controller
    {
        private readonly IReceptionistService _receptionistService;

        // Inject Service thay vì DbContext
        public ProfileController(IReceptionistService receptionistService)
        {
            _receptionistService = receptionistService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // 1. Lấy User ID hiện tại
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // [THAY ĐỔI IDENTITY] Kiểm tra nếu chưa đăng nhập (hoặc session hết hạn)
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // 2. Gọi Service để lấy dữ liệu đã xử lý
            var model = await _receptionistService.GetReceptionistProfileAsync(userId);

            if (model == null)
            {
                return Content("Không tìm thấy hồ sơ nhân viên. Vui lòng liên hệ Admin.");
            }

            // 3. Trả về View
            return View(model);
        }
    }
}
