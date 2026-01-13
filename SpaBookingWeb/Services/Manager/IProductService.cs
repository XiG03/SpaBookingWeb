using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IProductService
    {
        Task<ProductDashboardViewModel> GetAllProductsAsync();
        
        // This function prepares data for Create form (including loading dropdowns)
        Task<ProductViewModel> GetProductForCreateAsync();
        
        Task<ProductViewModel?> GetProductForEditAsync(int id);
        
        Task<Product?> GetProductByIdAsync(int id);

        Task CreateProductAsync(ProductViewModel model);
        
        Task UpdateProductAsync(ProductViewModel model);
        
        Task DeleteProductAsync(int id);
    }
}