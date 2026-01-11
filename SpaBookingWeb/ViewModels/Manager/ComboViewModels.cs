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

        [Display(Name = "Tên Combo")]
        [Required(ErrorMessage = "Vui lòng nhập tên combo")]
        public string ComboName { get; set; } = string.Empty;

        [Display(Name = "Giá Combo (VNĐ)")]
        [Required(ErrorMessage = "Vui lòng nhập giá combo")]
        public decimal Price { get; set; }

        [Display(Name = "Mô tả")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Hình ảnh")]
        public IFormFile? ImageFile { get; set; }
        public string? ExistingImage { get; set; }

        // --- Phần quan trọng: Chọn Dịch vụ ---
        [Display(Name = "Các dịch vụ trong Combo")]
        [Required(ErrorMessage = "Vui lòng chọn ít nhất một dịch vụ")]
        public List<int> SelectedServiceIds { get; set; } = new List<int>();

        // Danh sách để đổ dữ liệu vào Dropdown (Select2)
        public IEnumerable<SelectListItem>? AvailableServices { get; set; }

        // Map: ServiceId -> List of Consumables (dùng để JS tính toán hiển thị)
        public Dictionary<int, List<ServiceConsumableDto>> ServiceConsumablesMap { get; set; } = new Dictionary<int, List<ServiceConsumableDto>>();
    }
}