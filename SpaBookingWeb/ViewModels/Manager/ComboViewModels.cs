using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SpaBookingWeb.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    // ViewModel cho trang danh sách
    public class ComboDashboardViewModel
    {
        public List<ComboStatisticDto> Combos { get; set; } = new List<ComboStatisticDto>();
    }

    public class ComboStatisticDto
    {
        public int ComboId { get; set; }
        public string ComboName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Image { get; set; } = string.Empty;
        public int ServiceCount { get; set; } // Số lượng dịch vụ trong combo
        public string ServiceNames { get; set; } = string.Empty; // Tên các dịch vụ (nối chuỗi)
    }

    // ViewModel cho Thêm/Sửa
    public class ComboViewModel
    {
        public int ComboId { get; set; }

        [Display(Name = "Combo Name")]
        [Required(ErrorMessage = "Please enter combo name")]
        public string ComboName { get; set; } = string.Empty;

        [Display(Name = "Combo Price (VND)")]
        [Required(ErrorMessage = "Please enter combo price")]
        public decimal Price { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Image")]
        public IFormFile? ImageFile { get; set; }
        public string? ExistingImage { get; set; }

        // --- Phần quan trọng: Chọn Dịch vụ ---
        [Display(Name = "Services in Combo")]
        [Required(ErrorMessage = "Please select at least one service")]
        public List<int> SelectedServiceIds { get; set; } = new List<int>();

        // Danh sách để đổ dữ liệu vào Dropdown (Select2)
        public IEnumerable<SelectListItem>? AvailableServices { get; set; }

        // Map: ServiceId -> List of Consumables (dùng để JS tính toán hiển thị)
        public Dictionary<int, List<ServiceConsumableDto>> ServiceConsumablesMap { get; set; } = new Dictionary<int, List<ServiceConsumableDto>>();
    }
}