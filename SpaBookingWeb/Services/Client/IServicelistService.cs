using SpaBookingWeb.ViewModels.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public interface IServiceListService
    {
        Task<ServiceListViewModel> GetServiceListAsync(
            string search, 
            int? categoryId, 
            string sortOrder, 
            int page = 1, 
            int pageSize = 12);
    }
}