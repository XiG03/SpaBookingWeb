using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Manager;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    // [Authorize(Roles = "Admin,Manager")]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        // GET: Manager/Customer
        public async Task<IActionResult> Index()
        {
            var viewModel = await _customerService.GetCustomerDashboardDataAsync();
            return View(viewModel);
        }

        // GET: Manager/Customer/Details/5
        public async Task<IActionResult> Detail(string id)
        {
            if (id == null) return NotFound();
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        public IActionResult Create()
        {
            return View();
        }

        // POST: Manager/Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email,PhoneNumber,Address")] ApplicationUser user, string Password)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(Password))
                {
                    ModelState.AddModelError("Password", "Password is required.");
                    return View(user);
                }

                var result = await _customerService.CreateCustomerAsync(user, Password);
                if (result)
                {
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", "Cannot create account. Email may already exist or password is not strong enough.");
            }
            return View(user);
        }

        // GET: Manager/Customer/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // POST: Manager/Customer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,FullName,Email,PhoneNumber")] ApplicationUser user)
        {
            if (id != user.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var result = await _customerService.UpdateCustomerAsync(user);
                if (result)
                {
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", "Cannot update customer information.");
            }
            return View(user);
        }

        // GET: Manager/Customer/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // POST: Manager/Customer/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _customerService.DeleteCustomerAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
