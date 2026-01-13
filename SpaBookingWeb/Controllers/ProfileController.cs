using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Controllers
{
    [Authorize] // Requires login
    public class ProfileController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(IBookingService bookingService, UserManager<ApplicationUser> userManager)
        {
            _bookingService = bookingService;
            _userManager = userManager;
        }

        // Action to display appointment list (Default profile page)
        public async Task<IActionResult> Appointments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var history = await _bookingService.GetBookingHistoryAsync(user.Email);

            ViewData["UserName"] = user.FullName ?? user.UserName;
            ViewData["UserEmail"] = user.Email;

            return View(history);
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointmentDetail(int id)
        {
            var detail = await _bookingService.GetAppointmentDetailAsync(id);
            if (detail == null) return NotFound();
            return Json(detail);
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var history = await _bookingService.GetBookingHistoryArchiveAsync(user.Email);

            ViewData["UserName"] = user.FullName ?? user.UserName;
            ViewData["UserEmail"] = user.Email;

            return View(history);
        }

        // [OLD] Rebook from start (Step 2)
        [HttpPost]
        public async Task<IActionResult> Rebook(int id)
        {
            var result = await _bookingService.RebookAsync(id);
            if (result)
            {
                return RedirectToAction("Step2_Services", "Booking");
            }
            return RedirectToAction("History");
        }

        // [NEW] Continue payment (Step 5) for Pending order
        [HttpPost]
        public async Task<IActionResult> ContinueBooking(int id)
        {
            // Call Resume function to load old data into Session
            var result = await _bookingService.ResumeBookingAsync(id);
            if (result)
            {
                // Redirect straight to confirmation/payment step
                return RedirectToAction("Step5_Confirm", "Booking");
            }
            // If error, return to list page
            return RedirectToAction("Appointments");
        }
    }
}