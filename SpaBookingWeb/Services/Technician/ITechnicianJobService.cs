using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Technician;

namespace SpaBookingWeb.Services.Technictian
{
    public interface ITechnicianJobService
    {
        // 1. Lấy ID nhân viên từ User Identity
        Task<int?> GetCurrentEmployeeIdAsync(string userId);

        // 2. Lấy danh sách lịch để hiển thị (Home/Index)
        Task<List<CalendarEventVM>> GetCalendarEventsAsync(int employeeId, int month, int year);

        // 3. Lấy chi tiết công việc (Job/Detail)
        Task<JobDetailVM> GetJobDetailAsync(int appointmentDetailId, int employeeId);

        // Thêm vào trong Interface
        Task<bool> ChangeJobStatusAsync(int appointmentDetailId, int employeeId, string newStatus, decimal? tipAmount = null);

        Task<List<ConsumableItemVM>> GetConsumablesAsync(int appointmentDetailId);

        Task<List<Product>> GetAvailableProductsAsync();

        Task SaveConsumablesAsync(int appointmentDetailId, List<ConsumableItemVM> items);

        // Kiểm tra xem hôm nay đã check-in chưa (Dùng cho bộ lọc chặn)
        Task<bool> IsCheckedInTodayAsync(int employeeId);

        // Thực hiện điểm danh (trả về string lỗi hoặc null nếu thành công)
        Task<string> PerformAttendanceAsync(int employeeId, string clientIp);

        // Lấy thông tin điểm danh hôm nay (để hiển thị lên giao diện)
        Task<WorkSchedule?> GetTodayScheduleAsync(int employeeId);

        Task<TechnicianProfileVM?> GetTechnicianProfileAsync(int employeeId);
    }
}
