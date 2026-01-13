using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IDashboardService
    {
        // Lấy dữ liệu tổng quan cho trang Dashboard
        Task<DashboardViewModel> GetDashboardDataAsync(DateTime date);

        // Lấy dữ liệu cho FullCalendar (theo khoảng thời gian start-end)
        Task<List<CalendarEventViewModel>> GetCalendarEventsAsync(DateTime start, DateTime end);

        // MỚI: Lấy chi tiết lịch hẹn cho Modal
        Task<AppointmentDetailViewModel> GetAppointmentDetailAsync(int id);
    }
}