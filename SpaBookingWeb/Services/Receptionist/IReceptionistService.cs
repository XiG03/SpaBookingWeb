using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Receptionist;

namespace SpaBookingWeb.Services.Receptionist
{
    public interface IReceptionistService
    {
        // Hàm lấy toàn bộ dữ liệu cho Dashboard
        Task<CalendarVM> GetDashboardDataAsync(DateTime date);

        Task<ReceptionistProfileVM> GetReceptionistProfileAsync(string userId);

        Task<BillCalculationResult> CalculateBillAsync(CheckoutRequestVM request);

        Task<int> ProcessCheckoutAsync(CheckoutRequestVM request, bool isPaid = true);

        Task<WorkSchedule?> GetTodayScheduleAsync(int employeeId);

        Task<string> PerformAttendanceAsync(int employeeId, string clientIp);
    }
}
