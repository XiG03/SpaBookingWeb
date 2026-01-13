using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Controllers
{
    [Route("Posts")]
    public class PostsController : Controller
    {
        private readonly IPostService _postService;

        public PostsController(IPostService postService)
        {
            _postService = postService;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(string search, int? categoryId, int page = 1)
        {
            var model = await _postService.GetPostListAsync(search, categoryId, page);
            return View(model);
        }

        [HttpGet]
        [Route("Detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var model = await _postService.GetPostDetailAsync(id);
            if (model == null)
            {
                return RedirectToAction("Index");
            }
            return View(model);
        }
    }
}