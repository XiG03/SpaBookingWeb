using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IReviewService
    {
        // Get review list and statistics
        Task<ReviewDashboardViewModel> GetReviewDashboardAsync();
        
        // Delete review (if violated)
        Task DeleteReviewAsync(int id);
    }
}