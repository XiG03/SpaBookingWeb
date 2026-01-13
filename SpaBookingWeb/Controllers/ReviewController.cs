using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Client;
using SpaBookingWeb.ViewModels.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Controllers
{
    [Authorize] // Requires login
    public class ReviewController : Controller
    {
        private readonly IReviewClientService _reviewService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewController(IReviewClientService reviewService, UserManager<ApplicationUser> userManager)
        {
            _reviewService = reviewService;
            _userManager = userManager;
        }

        // GET: /Review/Index/{appointmentId}
        [HttpGet]
        public async Task<IActionResult> Index(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var model = await _reviewService.GetReviewPageDataAsync(id, user.Email);
            
            if (model == null)
            {
                TempData["ErrorMessage"] = "Appointment not found or you do not have permission to review.";
                return RedirectToAction("History", "Profile");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Submit(SubmitReviewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload data if validation error
                var user = await _userManager.GetUserAsync(User);
                var viewModel = await _reviewService.GetReviewPageDataAsync(model.AppointmentId, user.Email);
                return View("Index", viewModel);
            }

            var result = await _reviewService.SubmitReviewAsync(model);
            if (result)
            {
                TempData["SuccessMessage"] = "Thank you for sending your review!";
                return RedirectToAction("History", "Profile");
            }
            else
            {
                TempData["ErrorMessage"] = "An error occurred or you have already reviewed this order.";
                return RedirectToAction("History", "Profile");
            }
        }
    }
}