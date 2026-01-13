using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services;
using SpaBookingWeb.Services.Client;

namespace SpaBookingWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly IClientHomeService _clientHomeService;
        private readonly MomoService _momoService;

        public HomeController(ILogger<HomeController> logger, IClientHomeService clientHomeService, MomoService momoService)
        {
            _logger = logger;
            _clientHomeService = clientHomeService;
            _momoService = momoService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> HomeClient()
        {
            var model = await _clientHomeService.GetHomeDataAsync();
            return View(model);
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        [HttpGet]
        public async Task<IActionResult> TestMomo()
        {
            // Test data
            var orderId = $"ORDER_{DateTime.Now.Ticks}";
            long amount = 10000; // 10.000 VNĐ
            var orderInfo = "Test MoMo Payment SpaBooking";

            // URL return & IPN
            var redirectUrl = "http://localhost:5329/Payment/PaymentReturn";
            var ipnUrl = "http://localhost:5329/Payment/Notify";

            var payUrl = await _momoService.CreatePaymentAsync(
                orderId,
                amount,
                orderInfo,
                redirectUrl,
                ipnUrl
            );

            // Redirect sang MoMo
            return Redirect(payUrl);
        }

        /// <summary>
        /// MoMo redirects back after payment
        /// </summary>
        [HttpGet]
        public IActionResult PaymentReturn()
        {
            // MoMo will send query strings here
            var query = Request.Query;

            return View(query); // or return Json(query);
        }

        /// <summary>
        /// MoMo IPN background call (server to server)
        /// </summary>
        [HttpPost]
        public IActionResult Notify()
        {
            var body = Request.Form;
            return Ok();
        }
    }
}
