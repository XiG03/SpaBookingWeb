using SpaBookingWeb.ViewModels.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public interface IReviewClientService
    {
        // Lấy thông tin để hiển thị form đánh giá
        Task<ReviewPageViewModel> GetReviewPageDataAsync(int appointmentId, string userEmail);

        // Lưu đánh giá
        Task<bool> SubmitReviewAsync(SubmitReviewModel model);
    }
}