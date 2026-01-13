using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    // ViewModel cho trang danh sách (Index)
    public class ProductDashboardViewModel
    {
        public List<ProductDto> Products { get; set; } = new List<ProductDto>();
    }

    public class ProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public int StockQuantity { get; set; }
        public bool IsForSale { get; set; } // Có bán lẻ không

        // --- THÊM CÁC TRƯỜNG THỐNG KÊ ---
        public int UsedInServiceCount { get; set; } // Số lượng đã dùng trong dịch vụ (Consumable)
        public int SoldCount { get; set; }          // Số lượng đã bán lẻ (Retail)
        public string TopServiceUsage { get; set; } = "Chưa sử dụng"; // Tên dịch vụ dùng sản phẩm này nhiều nhất
    }

    // ViewModel cho Thêm/Sửa (Giữ nguyên)
    public class ProductViewModel
    {
        public int ProductId { get; set; }

        [Display(Name = "Product Name")]
        [Required(ErrorMessage = "Please enter product name")]
        [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Display(Name = "Unit")]
        public int? UnitId { get; set; }

        [Display(Name = "Purchase Price")]
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Purchase price must be greater than or equal to 0")]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "Sale Price")]
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Sale price must be greater than or equal to 0")]
        public decimal SalePrice { get; set; }

        [Display(Name = "Stock Quantity")]
        [Required]
        public int StockQuantity { get; set; }

        [Display(Name = "Is for sale?")]
        public bool IsForSale { get; set; }

        // Dữ liệu cho Dropdown
        public IEnumerable<SelectListItem>? Categories { get; set; }
        public IEnumerable<SelectListItem>? Units { get; set; }
    }
}