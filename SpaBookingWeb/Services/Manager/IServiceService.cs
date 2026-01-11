using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;
using SpaBookingWeb.Models;

namespace SpaBookingWeb.Services.Manager
{
    public interface IServiceService
    {
        // Lấy dữ liệu tổng hợp cho trang danh sách Service
        Task<ServiceDashboardViewModel> GetServiceDashboardAsync();

        // Lấy thông tin chi tiết để hiển thị form Sửa
        Task<ServiceViewModel?> GetServiceForEditAsync(int id);

        // Tạo mới dịch vụ
        Task CreateServiceAsync(ServiceViewModel model);

        // Cập nhật dịch vụ
        Task UpdateServiceAsync(ServiceViewModel model);

        // Xóa mềm (Chuyển trạng thái hoạt động thành false hoặc xóa nếu có cột IsDeleted)
        Task DeleteServiceAsync(int id);

        // Helper cho trang Create
        Task<ServiceViewModel> GetServiceForCreateAsync();

        Task<Service?> GetServiceByIdAsync(int id);
    }
}