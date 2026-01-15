using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Areas.Technician.Filters;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Technictian;
using SpaBookingWeb.ViewModels.Technician;

namespace SpaBookingWeb.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")] // [THAY ĐỔI IDENTITY] Bật lại bảo mật
    [TypeFilter(typeof(RequireAttendanceFilter))] // <--- THÊM DÒNG NÀY
    // [Authorize(Roles = "Technician")] // Tạm tắt bảo mật để test khi chưa đăng nhập được
    public class JobController : Controller
    {
        private readonly ITechnicianJobService _jobService;
        private readonly UserManager<ApplicationUser> _userManager;

        public JobController(ITechnicianJobService jobService, UserManager<ApplicationUser> userManager)
        {
            _jobService = jobService;
            _userManager = userManager;
        }

        // GET: Xem chi tiết công việc
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            // [THAY ĐỔI IDENTITY] Lấy User hiện tại từ Identity
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            // [THAY ĐỔI IDENTITY] Lấy EmployeeId từ Service dựa trên UserId
            var employeeId = await _jobService.GetCurrentEmployeeIdAsync(user.Id);

            if (employeeId == null) return View("Error");

            // Gọi Service lấy chi tiết
            var vm = await _jobService.GetJobDetailAsync(id, employeeId.Value);

            if (vm == null)
            {
                return NotFound("No jobs found, or you haven't been assigned any.");
            }

            return View(vm);
        }

        // POST: Xử lý nút bấm (Check-in, Check-out, Cancel)
        [HttpPost]
        [ValidateAntiForgeryToken] // Bảo mật form
        public async Task<IActionResult> UpdateStatus(int id, string actionType, decimal? tipAmount)
        {
            // [THAY ĐỔI IDENTITY] Lấy User hiện tại
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            // [THAY ĐỔI IDENTITY] Lấy EmployeeId thật
            var employeeId = await _jobService.GetCurrentEmployeeIdAsync(user.Id);

            if (employeeId == null) return View("Error");

            // Xác định trạng thái mới dựa trên nút bấm
            string newStatus = "";
            string message = "";

            switch (actionType)
            {
                case "CheckIn":
                    newStatus = "InProgress";
                    message = "Started providing the service!";
                    break;
                case "CheckOut":
                    newStatus = "Completed";
                    message = "Service completed!";
                    break;
                case "Cancel":
                    newStatus = "Cancelled";
                    message = "Service has been cancelled.";
                    break;
                default:
                    return BadRequest("Invalid action!");
            }

            // Gọi Service cập nhật DB
            try
            {
                // Gọi Service (Giờ đây Service sẽ ném Exception nếu có lỗi logic)
                var result = await _jobService.ChangeJobStatusAsync(id, employeeId.Value, newStatus, tipAmount);

                if (result)
                {
                    TempData["SuccessMessage"] = message;
                    if (tipAmount > 0 && newStatus == "Completed")
                        TempData["SuccessMessage"] += $" (Tip: {tipAmount:N0}đ)";
                }
            }
            catch (Exception ex)
            {
                // Bắt lỗi từ Service (VD: Lễ tân chưa check-in) và hiện ra màn hình
                TempData["ErrorMessage"] = ex.Message;
            }

            // Load lại trang chi tiết để thấy sự thay đổi
            return RedirectToAction("Detail", new { id = id });
        }

        // GET: Lấy dữ liệu tiêu hao (Gọi bằng Ajax khi trang load)
        [HttpGet]
        public async Task<IActionResult> GetConsumables(int id)
        {
            var data = await _jobService.GetConsumablesAsync(id);
            return Json(data);
        }

        // GET: Lấy danh sách sản phẩm để đổ vào Dropdown
        [HttpGet]
        public async Task<IActionResult> GetProductList()
        {
            var products = await _jobService.GetAvailableProductsAsync();
            // Chỉ lấy trường cần thiết để giảm dung lượng
            var simpleList = products.Select(p => new {
                id = p.ProductId,
                name = p.ProductName,
                unit = p.Unit?.UnitName ?? "Cái"
            });
            return Json(simpleList);
        }

        // POST: Lưu dữ liệu
        [HttpPost]
        public async Task<IActionResult> SaveConsumables(int id, [FromBody] List<ConsumableItemVM> items)
        {
            try
            {
                await _jobService.SaveConsumablesAsync(id, items);
                return Ok(new { success = true, message = "Saved successfully!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }


    }
}
