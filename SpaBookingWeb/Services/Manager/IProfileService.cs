using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IProfileService
    {
        Task<ProfileViewModel> GetUserProfileAsync(string userId);
        Task UpdateUserProfileAsync(string userId, ProfileViewModel model);
    }
}