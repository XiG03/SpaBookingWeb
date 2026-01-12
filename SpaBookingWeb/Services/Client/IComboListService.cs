using SpaBookingWeb.ViewModels.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public interface IComboListService
    {
        Task<ComboListViewModel> GetComboListAsync(
            string search, 
            int? categoryId, 
            string sortOrder, 
            int page = 1, 
            int pageSize = 12);

        Task<ComboDetailViewModel> GetComboDetailAsync(int comboId);
    }
}