using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SpaBookingWeb.Services.Interfaces;
using SpaBookingWeb.ViewModels.BlogPosts;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    // [Route("manager/[controller]/[action]")]
    public class BlogPostsController : Controller
    {
        private readonly IBlogPostService _blogService;

        public BlogPostsController(IBlogPostService blogService)
        {
            _blogService = blogService;
        }

        // 1. List: Requires VIEW permission
        [HttpGet]

        public async Task<IActionResult> Index()
        {
            var posts = await _blogService.GetAllAsync();
            return View(posts);
        }

        // 2. Create: Requires CREATE permission
        [HttpGet]

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBlogPostRequest request)
        {
            if (!ModelState.IsValid) return View(request);

            // Get logged in user ID to assign author
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            await _blogService.CreateAsync(request, userId);
            
            TempData["Success"] = "Post created successfully!";
            return RedirectToAction(nameof(Index));
        }

        // 3. Update: Requires EDIT permission
        [HttpGet]

        public async Task<IActionResult> Edit(int id)
        {
            var post = await _blogService.GetByIdAsync(id);
            if (post == null) return NotFound();

            // Map from View ViewModel to Update ViewModel
            var updateRequest = new UpdateBlogPostRequest
            {
                Id = post.Id,
                Title = post.Title,
                Content = post.Content,
                IsPublished = post.IsPublished,
                ExistingImageUrl = post.ImageUrl
            };

            return View(updateRequest);
        }

        [HttpPost]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateBlogPostRequest request)
        {
            if (!ModelState.IsValid) return View(request);

            try 
            {
                await _blogService.UpdateAsync(request);
                TempData["Success"] = "Updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["Error"] = "An error occurred while updating.";
                return View(request);
            }
        }

        // 4. Delete: Requires DELETE permission
        [HttpPost]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _blogService.DeleteAsync(id);
            TempData["Success"] = "Post deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}