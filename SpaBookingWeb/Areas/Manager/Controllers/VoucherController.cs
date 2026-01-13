using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SpaBookingWeb.ViewModels.Manager;
using SpaBookingWeb.Services.Manager;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    // [Authorize(Roles = "Admin,Manager")]
    public class VoucherController : Controller
    {
        private readonly IVoucherService _voucherService;

        public VoucherController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = await _voucherService.GetAllVouchersAsync();
            return View(viewModel);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateVoucherViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _voucherService.CreateVoucherAsync(model);
                    TempData["Success"] = "Voucher created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var voucher = await _voucherService.GetVoucherByIdAsync(id);
            if (voucher == null) return NotFound();

            var model = new CreateVoucherViewModel
            {
                VoucherId = voucher.VoucherId,
                Name = voucher.Name,
                Code = voucher.Code,
                Description = voucher.Description,
                DiscountType = voucher.DiscountType,
                DiscountValue = voucher.DiscountValue,
                MaxDiscountAmount = voucher.MaxDiscountAmount,
                MinSpend = voucher.MinSpend,
                UsageLimit = voucher.UsageLimit,
                StartDate = voucher.StartDate,
                EndDate = voucher.EndDate,
                IsActive = voucher.IsActive
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreateVoucherViewModel model)
        {
            if (id != model.VoucherId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    await _voucherService.UpdateVoucherAsync(model);
                    TempData["Success"] = "Updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var voucher = await _voucherService.GetVoucherByIdAsync(id);
            if (voucher == null) return NotFound();
            return View(voucher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _voucherService.DeleteVoucherAsync(id);
            TempData["Success"] = "Voucher deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
