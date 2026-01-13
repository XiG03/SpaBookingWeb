using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IComboService
    {
        Task<ComboDashboardViewModel> GetAllCombosAsync();
        
        // This function will prepare both Combo data and list of Services to select
        Task<ComboViewModel> GetComboForCreateAsync();
        
        Task<ComboViewModel?> GetComboForEditAsync(int id);
        
        Task<Combo?> GetComboByIdAsync(int id); // For Delete/Detail page

        Task CreateComboAsync(ComboViewModel model);
        
        Task UpdateComboAsync(ComboViewModel model);
        
        Task DeleteComboAsync(int id);
    }
}