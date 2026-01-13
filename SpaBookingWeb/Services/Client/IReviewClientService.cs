using SpaBookingWeb.ViewModels.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public interface IReviewClientService
    {
        // Get info to display review form
        Task<ReviewPageViewModel> GetReviewPageDataAsync(int appointmentId, string userEmail);

        // Save review
        Task<bool> SubmitReviewAsync(SubmitReviewModel model);
    }
}