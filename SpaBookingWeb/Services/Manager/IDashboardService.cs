using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IDashboardService
    {
        // Get overview data for Dashboard page
        Task<DashboardViewModel> GetDashboardDataAsync(DateTime date);

        // Get data for FullCalendar (by start-end range)
        Task<List<CalendarEventViewModel>> GetCalendarEventsAsync(DateTime start, DateTime end);

        // NEW: Get appointment detail for Modal
        Task<AppointmentDetailViewModel> GetAppointmentDetailAsync(int id);
    }
}