using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;
using SpaBookingWeb.Models;

namespace SpaBookingWeb.Services.Manager
{
    public interface IServiceService
    {
        // Get aggregate data for Service list page
        Task<ServiceDashboardViewModel> GetServiceDashboardAsync(string searchName = null);

        // Get details to display Edit form
        Task<ServiceViewModel?> GetServiceForEditAsync(int id);

        // Create new service
        Task CreateServiceAsync(ServiceViewModel model);

        // Update service
        Task UpdateServiceAsync(ServiceViewModel model);

        // Soft delete (Change status to false or delete if IsDeleted column exists)
        Task DeleteServiceAsync(int id);

        // Helper for Create page
        Task<ServiceViewModel> GetServiceForCreateAsync();

        Task<Service?> GetServiceByIdAsync(int id);
    }
}