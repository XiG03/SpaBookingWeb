using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = "Manager,Admin")]
    public class ServiceController : Controller
    {
        private readonly IServiceService _serviceService;

        public ServiceController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string searchName)
        {
            ViewData["CurrentFilter"] = searchName;
            var viewModel = await _serviceService.GetServiceDashboardAsync(searchName);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Filter(string searchName)
        {
            var viewModel = await _serviceService.GetServiceDashboardAsync(searchName);
            return PartialView("_ServiceTableRows", viewModel.Services);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = await _serviceService.GetServiceForCreateAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _serviceService.CreateServiceAsync(model);
                TempData["SuccessMessage"] = "Service added successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _serviceService.GetServiceForEditAsync(id);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ServiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _serviceService.UpdateServiceAsync(model);
                TempData["SuccessMessage"] = "Service updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // --- UPDATE DELETE SECTION ---

        // GET: Show delete confirmation page
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _serviceService.GetServiceByIdAsync(id);
            if (service == null) return NotFound();
            return View(service);
        }

        // POST: Execute Delete (Soft Delete)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _serviceService.DeleteServiceAsync(id);
            TempData["SuccessMessage"] = "Service deactivated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}