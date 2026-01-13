using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager; // Add this using directive
using System.Security.Claims;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize] // Anyone logged in can view their own profile
    public class ProfileController : Controller
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _profileService.GetUserProfileAsync(userId);
            
            if (profile == null) return NotFound();

            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _profileService.UpdateUserProfileAsync(userId, model);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}