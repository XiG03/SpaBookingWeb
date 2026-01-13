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

        [Display(Name = "Tên sản phẩm")]
        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [StringLength(200, ErrorMessage = "Tên sản phẩm không quá 200 ký tự")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Danh mục")]
        public int? CategoryId { get; set; }

        [Display(Name = "Đơn vị tính")]
        public int? UnitId { get; set; }

        [Display(Name = "Giá nhập")]
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0")]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "Giá bán")]
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0")]
        public decimal SalePrice { get; set; }

        [Display(Name = "Số lượng tồn")]
        [Required]
        public int StockQuantity { get; set; }

        [Display(Name = "Sản phẩm bán lẻ?")]
        public bool IsForSale { get; set; }

        // Dữ liệu cho Dropdown
        public IEnumerable<SelectListItem>? Categories { get; set; }
        public IEnumerable<SelectListItem>? Units { get; set; }
    }
}