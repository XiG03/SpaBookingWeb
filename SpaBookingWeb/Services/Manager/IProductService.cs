using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public interface IProductService
    {
        Task<ProductDashboardViewModel> GetAllProductsAsync();
        
        // Hàm này chuẩn bị dữ liệu cho form Create (bao gồm load dropdown)
        Task<ProductViewModel> GetProductForCreateAsync();
        
        Task<ProductViewModel?> GetProductForEditAsync(int id);
        
        Task<Product?> GetProductByIdAsync(int id);

        Task CreateProductAsync(ProductViewModel model);
        
        Task UpdateProductAsync(ProductViewModel model);
        
        Task DeleteProductAsync(int id);
    }
}