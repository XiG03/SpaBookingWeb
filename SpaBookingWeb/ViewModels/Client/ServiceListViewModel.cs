using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Client
{
    public class ServiceListViewModel
    {
        // Danh sách danh mục để hiển thị ở sidebar và bộ lọc mobile
        public List<ClientCategoryViewModel> Categories { get; set; } = new List<ClientCategoryViewModel>();

        // Danh sách dịch vụ (đã lọc và phân trang)
        public List<ClientServiceItemViewModel> Services { get; set; } = new List<ClientServiceItemViewModel>();

        // Danh sách Combo (hiển thị riêng nếu cần, hoặc gộp chung)
        public List<ClientComboItemViewModel> Combos { get; set; } = new List<ClientComboItemViewModel>();

        // Thông tin tìm kiếm & lọc hiện tại
        public string CurrentSearch { get; set; }
        public int? CurrentCategoryId { get; set; }
        public string SortOrder { get; set; } // "popular", "price_asc", "price_desc"
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // Thông tin phân trang
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
    }

    public class ClientCategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; } // URL ảnh đại diện
        public bool IsSelected { get; set; }
    }

    public class ClientServiceItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public string ImageUrl { get; set; }
        public double Rating { get; set; } = 5.0;
        public int DiscountPercent { get; set; } // Nếu có giảm giá
        public bool IsHot { get; set; }
    }

    public class ClientComboItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } // Liệt kê các dịch vụ bên trong
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public int DurationMinutes { get; set; }
        public string ImageUrl { get; set; }
        public string StatusText { get; set; } // "Còn chỗ", "Hot deal"...
        public bool IsBestSeller { get; set; }
    }
}