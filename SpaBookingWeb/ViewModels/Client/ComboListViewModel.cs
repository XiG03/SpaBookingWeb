using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Client
{
    public class ComboListViewModel
    {
        // Danh sách danh mục (để lọc theo loại dịch vụ trong combo)
        public List<ClientCategoryViewModel> Categories { get; set; } = new List<ClientCategoryViewModel>();

        // Danh sách Combo hiển thị
        public List<ClientComboItemViewModel> Combos { get; set; } = new List<ClientComboItemViewModel>();

        // Bộ lọc hiện tại
        public string CurrentSearch { get; set; }
        public int? CurrentCategoryId { get; set; } // Lọc combo chứa dịch vụ thuộc category này
        public string SortOrder { get; set; } // popular, price_asc, price_desc, new

        // Phân trang
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
    }
}