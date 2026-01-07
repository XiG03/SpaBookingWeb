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

        // 1. Danh sách: Yêu cầu quyền VIEW
        [HttpGet]

        public async Task<IActionResult> Index()
        {
            var posts = await _blogService.GetAllAsync();
            return View(posts);
        }

        // 2. Tạo mới: Yêu cầu quyền CREATE
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

            // Lấy ID người đang đăng nhập để gán tác giả
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            await _blogService.CreateAsync(request, userId);
            
            TempData["Success"] = "Tạo bài viết thành công!";
            return RedirectToAction(nameof(Index));
        }

        // 3. Chỉnh sửa: Yêu cầu quyền EDIT
        [HttpGet]

        public async Task<IActionResult> Edit(int id)
        {
            var post = await _blogService.GetByIdAsync(id);
            if (post == null) return NotFound();

            // Map từ ViewModel hiển thị sang ViewModel cập nhật
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
                TempData["Success"] = "Cập nhật thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["Error"] = "Có lỗi xảy ra khi cập nhật.";
                return View(request);
            }
        }

        // 4. Xóa: Yêu cầu quyền DELETE
        [HttpPost]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _blogService.DeleteAsync(id);
            TempData["Success"] = "Đã xóa bài viết.";
            return RedirectToAction(nameof(Index));
        }
    }
}