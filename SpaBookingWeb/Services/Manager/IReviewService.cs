using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IReviewService
    {
        // Lấy danh sách review và thống kê
        Task<ReviewDashboardViewModel> GetReviewDashboardAsync();
        
        // Xóa review (nếu vi phạm)
        Task DeleteReviewAsync(int id);
    }
}