using System.Collections.Generic;
using System.Threading.Tasks;
using SpaBookingWeb.ViewModels.Manager;
using SpaBookingWeb.Models;



namespace SpaBookingWeb.Services.Manager
{
    public interface ICustomerService
    {
        // Get Dashboard Data
        Task<CustomerDashboardViewModel> GetCustomerDashboardDataAsync();

        // Customer CRUD
        Task<List<ApplicationUser>> GetAllCustomersAsync();
        Task<ApplicationUser> GetCustomerByIdAsync(string id);
        Task<bool> UpdateCustomerAsync(ApplicationUser user);
        Task<bool> DeleteCustomerAsync(string id); // Usually lock account (Soft Delete)
        Task<bool> CreateCustomerAsync(ApplicationUser user, string password);
        
        // Sync Identity User to Customer Table
        Task SyncCustomerAsync(string fullName, string phoneNumber, string email);
    }
}


