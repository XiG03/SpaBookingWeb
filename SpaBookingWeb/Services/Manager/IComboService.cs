using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IComboService
    {
        Task<ComboDashboardViewModel> GetAllCombosAsync();
        
        // Hàm này sẽ chuẩn bị cả dữ liệu Combo và danh sách Service để chọn
        Task<ComboViewModel> GetComboForCreateAsync();
        
        Task<ComboViewModel?> GetComboForEditAsync(int id);
        
        Task<Combo?> GetComboByIdAsync(int id); // Cho trang Delete/Detail

        Task CreateComboAsync(ComboViewModel model);
        
        Task UpdateComboAsync(ComboViewModel model);
        
        Task DeleteComboAsync(int id);
    }
}