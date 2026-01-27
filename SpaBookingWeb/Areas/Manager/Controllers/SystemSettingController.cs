using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = "Manager,Admin")]
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
            // Ignore validate new creation fields (as they can be null when just saving settings)
            ModelState.Remove("NewUnitName");

            ModelState.Remove("NewRuleName");

            ModelState.Remove("NewApplyToType");
            ModelState.Remove("NewDepositType");
            ModelState.Remove("NewDepositValue");
            ModelState.Remove("NewMinOrderValue");
            ModelState.Remove("NewTargetServiceId");
            ModelState.Remove("NewTargetMembershipTypeId");
            
            // LogoFile and LogoUrl are not required to have new values when updating
            ModelState.Remove("LogoFile");
            ModelState.Remove("LogoUrl");

            // --- FIX LIST VALIDATION ERROR ---
            // These lists are only for display (View), not submitted back so they are null -> Remove error
            ModelState.Remove("AvailableServices");
            ModelState.Remove("AvailableMembershipTypes");
            ModelState.Remove("Units");
            ModelState.Remove("DepositRules");



            if (ModelState.IsValid)
            {
                await _systemSettingService.UpdateSettingsAsync(model);
                TempData["SuccessMessage"] = "General settings saved!";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                var errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList();

                // Assign to TempData to display on View (or use ViewBag)
                TempData["ErrorMessage"] = "Validation error: " + string.Join(" | ", errors);
            }

            // Reload if error
            var reloadModel = await _systemSettingService.GetCurrentSettingsAsync();
            // Merge form data to re-display
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
            TempData["SuccessMessage"] = "Unit added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUnit(int id)
        {
            await _systemSettingService.DeleteUnitAsync(id);
            TempData["SuccessMessage"] = "Unit deleted.";
            return RedirectToAction(nameof(Index));
        }



        // --- DEPOSIT RULE ACTIONS ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDepositRule(SystemSettingViewModel model)
        {
            await _systemSettingService.AddDepositRuleAsync(model);
            TempData["SuccessMessage"] = "New deposit rule added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDepositRule(int id)
        {
            await _systemSettingService.DeleteDepositRuleAsync(id);
            TempData["SuccessMessage"] = "Deposit rule deleted.";
            return RedirectToAction(nameof(Index));
        }

        // --- PROMOTION ACTIONS ---
        [HttpPost]
        public async Task<IActionResult> PromoteCustomer(int customerId, string roleName)
        {
            try
            {
                await _systemSettingService.PromoteCustomerAsync(customerId, roleName);
                TempData["SuccessMessage"] = $"Customer promoted to {roleName} successfully. Default account: Email/Phone, Pass: Password123!";
            }
            catch (System.Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null)
                {
                    msg += " | Inner: " + ex.InnerException.Message;
                }
                TempData["ErrorMessage"] = "Error: " + msg;
            }
            return RedirectToAction(nameof(Index));
        }

    }
}