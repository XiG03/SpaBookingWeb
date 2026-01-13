using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface ISystemSettingService
    {
        // 1. General Configuration
        Task<SystemSettingViewModel> GetCurrentSettingsAsync();
        Task UpdateSettingsAsync(SystemSettingViewModel model);

        // 2. Unit Management
        Task AddUnitAsync(string unitName);
        Task DeleteUnitAsync(int id);
        
        // 3. Role Management




        // 4. Deposit Rule Management
        Task AddDepositRuleAsync(SystemSettingViewModel model); 
        Task DeleteDepositRuleAsync(int id);

        // 5. Promote Customer -> Employee
        Task PromoteCustomerAsync(int customerId);
    }
}