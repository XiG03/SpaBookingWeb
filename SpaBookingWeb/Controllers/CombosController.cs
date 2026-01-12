using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Controllers
{
    [Route("Combos")]
    public class CombosController : Controller
    {
        private readonly IComboListService _comboListService;

        public CombosController(IComboListService comboListService)
        {
            _comboListService = comboListService;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(string search, int? categoryId, string sortOrder, int page = 1)
        {
            var model = await _comboListService.GetComboListAsync(search, categoryId, sortOrder, page);
            return View(model);
        }

        // Action placeholder cho trang chi tiết (để link hoạt động)
        [HttpGet]
        [Route("Detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var model = await _comboListService.GetComboDetailAsync(id);
            if (model == null)
            {
                return RedirectToAction("Index");
            }
            return View(model);
        }
    }
}