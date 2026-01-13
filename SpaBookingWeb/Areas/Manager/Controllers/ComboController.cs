using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    // [Authorize(Roles = "Admin,Manager")]
    public class ComboController : Controller
    {
        private readonly IComboService _comboService;

        public ComboController(IComboService comboService)
        {
            _comboService = comboService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _comboService.GetAllCombosAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = await _comboService.GetComboForCreateAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComboViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _comboService.CreateComboAsync(model);
                TempData["SuccessMessage"] = "Combo added successfully!";
                return RedirectToAction(nameof(Index));
            }
            // If error, reload services list to keep dropdown
            var loadedModel = await _comboService.GetComboForCreateAsync();
            model.AvailableServices = loadedModel.AvailableServices;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _comboService.GetComboForEditAsync(id);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ComboViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _comboService.UpdateComboAsync(model);
                TempData["SuccessMessage"] = "Combo updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            // Reload dropdown if error
            var loadedModel = await _comboService.GetComboForCreateAsync(); // Reuse list retrieval function
            model.AvailableServices = loadedModel.AvailableServices;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var combo = await _comboService.GetComboByIdAsync(id);
            if (combo == null) return NotFound();
            return View(combo);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _comboService.DeleteComboAsync(id);
            TempData["SuccessMessage"] = "Combo deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}