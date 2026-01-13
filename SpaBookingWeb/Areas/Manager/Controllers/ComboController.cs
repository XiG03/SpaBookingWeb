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
                TempData["SuccessMessage"] = "Thêm Combo thành công!";
                return RedirectToAction(nameof(Index));
            }
            // Nếu lỗi, load lại danh sách services để không bị mất dropdown
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
                TempData["SuccessMessage"] = "Cập nhật Combo thành công!";
                return RedirectToAction(nameof(Index));
            }
            // Load lại dropdown nếu lỗi
            var loadedModel = await _comboService.GetComboForCreateAsync(); // Reuse hàm lấy list
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
            TempData["SuccessMessage"] = "Xóa Combo thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}