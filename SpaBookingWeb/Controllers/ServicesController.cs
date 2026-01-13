using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Controllers
{
    [Route("Services")]
    public class ServicesController : Controller
    {
        private readonly IServiceListService _serviceListService;

        public ServicesController(IServiceListService serviceListService)
        {
            _serviceListService = serviceListService;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(string search, int? categoryId, string sortOrder, int page = 1)
        {
            var model = await _serviceListService.GetServiceListAsync(search, categoryId, sortOrder, page);
            return View(model);
        }
    }
}