using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    // [Authorize(Roles = "Manager,Admin")]
    public class SystemSettingController : Controller
    {
        private readonly ISystemSettingService _systemSettingService;

        public SystemSettingController(ISystemSettingService systemSettingService)
        {
            _systemSettingService = systemSettingService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _systemSettingService.GetCurrentSettingsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SystemSettingViewModel model)
        {
            // Bỏ qua validate các field tạo mới (vì chúng có thể null khi chỉ save setting)
            ModelState.Remove("NewUnitName");

            ModelState.Remove("NewRuleName");

            ModelState.Remove("NewApplyToType");
            ModelState.Remove("NewDepositType");
            ModelState.Remove("NewDepositValue");
            ModelState.Remove("NewMinOrderValue");
            ModelState.Remove("NewTargetServiceId");
            ModelState.Remove("NewTargetMembershipTypeId");
            
            // LogoFile và LogoUrl không bắt buộc phải có giá trị mới khi update
            ModelState.Remove("LogoFile");
            ModelState.Remove("LogoUrl");

            // --- SỬA LỖI VALIDATION DANH SÁCH ---
            // Các danh sách này chỉ dùng để hiển thị (View), không submit về nên bị null -> Remove lỗi
            ModelState.Remove("AvailableServices");
            ModelState.Remove("AvailableMembershipTypes");
            ModelState.Remove("Units");
            ModelState.Remove("DepositRules");



            if (ModelState.IsValid)
            {
                await _systemSettingService.UpdateSettingsAsync(model);
                TempData["SuccessMessage"] = "Đã lưu cấu hình chung!";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                var errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList();

                // Gán vào TempData để hiển thị ra View (hoặc dùng ViewBag)
                TempData["ErrorMessage"] = "Lỗi validation: " + string.Join(" | ", errors);
            }

            // Reload nếu lỗi
            var reloadModel = await _systemSettingService.GetCurrentSettingsAsync();
            // Merge dữ liệu form để hiển thị lại
            reloadModel.SpaName = model.SpaName;
            reloadModel.PhoneNumber = model.PhoneNumber;
            reloadModel.Email = model.Email;
            reloadModel.Address = model.Address;
            reloadModel.FacebookUrl = model.FacebookUrl;
            reloadModel.OpenTime = model.OpenTime;
            reloadModel.CloseTime = model.CloseTime;
            reloadModel.DepositPercentage = model.DepositPercentage;

            return View(reloadModel);
        }

        // --- UNIT ACTIONS ---
        [HttpPost]
        public async Task<IActionResult> AddUnit(string newUnitName)
        {
            await _systemSettingService.AddUnitAsync(newUnitName);
            TempData["SuccessMessage"] = "Đã thêm đơn vị tính.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUnit(int id)
        {
            await _systemSettingService.DeleteUnitAsync(id);
            TempData["SuccessMessage"] = "Đã xóa đơn vị tính.";
            return RedirectToAction(nameof(Index));
        }



        // --- DEPOSIT RULE ACTIONS ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDepositRule(SystemSettingViewModel model)
        {
            await _systemSettingService.AddDepositRuleAsync(model);
            TempData["SuccessMessage"] = "Đã thêm quy tắc đặt cọc mới.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDepositRule(int id)
        {
            await _systemSettingService.DeleteDepositRuleAsync(id);
            TempData["SuccessMessage"] = "Đã xóa quy tắc đặt cọc.";
            return RedirectToAction(nameof(Index));
        }


    }
}