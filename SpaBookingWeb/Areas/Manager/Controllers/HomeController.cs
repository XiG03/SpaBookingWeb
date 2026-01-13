using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Services.Manager;
using System;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = "Manager,Admin")]
    public class HomeController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public HomeController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        // Dashboard Home
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var model = await _dashboardService.GetDashboardDataAsync(today);
            return View(model);
        }

        // API returns JSON for FullCalendar
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(string start, string end)
        {
            // FullCalendar sends ISO format start/end, parse to DateTime
            if (DateTime.TryParse(start, out DateTime startDate) && DateTime.TryParse(end, out DateTime endDate))
            {
                var events = await _dashboardService.GetCalendarEventsAsync(startDate, endDate);
                return Json(events);
            }
            return BadRequest();
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointmentDetails(int id)
        {
            // Call service to get details (can reuse AppointmentService or query directly if simple)
            // Assuming you have AppointmentService or quick query in DashboardService
            // For simplicity and speed, I will query via DashboardService (need to add this function to Interface)
            
            var detail = await _dashboardService.GetAppointmentDetailAsync(id);
            if (detail == null) return NotFound();

            return Json(detail);
        }
    }
}