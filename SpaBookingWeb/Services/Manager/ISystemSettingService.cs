using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface ISystemSettingService
    {
        // 1. Cấu hình chung
        Task<SystemSettingViewModel> GetCurrentSettingsAsync();
        Task UpdateSettingsAsync(SystemSettingViewModel model);

        // 2. Quản lý Đơn vị tính (Units)
        Task AddUnitAsync(string unitName);
        Task DeleteUnitAsync(int id);
        
        // 3. Quản lý Vai trò (Roles)




        // 4. Quản lý Quy tắc đặt cọc (Deposit Rules)
        Task AddDepositRuleAsync(SystemSettingViewModel model); 
        Task DeleteDepositRuleAsync(int id);
    }
}