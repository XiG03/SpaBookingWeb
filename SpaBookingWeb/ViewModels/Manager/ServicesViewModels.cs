using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class ServiceDashboardViewModel
    {
        // Danh sách chi tiết các dịch vụ để hiển thị bảng
        public List<ServiceStatisticDto> Services { get; set; } = new List<ServiceStatisticDto>();

        // Thống kê tổng quan
        public int TotalActiveServices { get; set; } // Số lượng service có thể sử dụng
        public int TotalServices { get; set; }

        // Dữ liệu cho biểu đồ (Arrays để chuyển sang JS)
        public List<string> ChartLabels { get; set; } = new List<string>(); // Tên dịch vụ
        public List<int> ChartUsageCount { get; set; } = new List<int>();   // Số lượt dùng
        public List<decimal> ChartRevenue { get; set; } = new List<decimal>(); // Doanh thu
    }

    // Class DTO được định nghĩa ngay tại đây thay vì tách file
    public class ServiceStatisticDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int UsageCount { get; set; }      // Số lần được đặt
        public decimal TotalRevenue { get; set; } // Tổng doanh thu từ dịch vụ này
        public bool IsActive { get; set; }
    }
    public class ServiceViewModel
    {
        public int ServiceId { get; set; }

        [Display(Name = "Tên dịch vụ")]
        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ")]
        [StringLength(200, ErrorMessage = "Tên dịch vụ không quá 200 ký tự")]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "Giá tiền (VNĐ)")]
        [Required(ErrorMessage = "Vui lòng nhập giá tiền")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá tiền phải lớn hơn hoặc bằng 0")]
        public decimal Price { get; set; }

        [Display(Name = "Thời lượng (phút)")]
        [Required(ErrorMessage = "Vui lòng nhập thời lượng")]
        [Range(1, 1440, ErrorMessage = "Thời lượng từ 1 phút đến 24 giờ")]
        public int DurationMinutes { get; set; }

        [Display(Name = "Mô tả")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Hình ảnh")]
        public IFormFile? ImageFile { get; set; } // File ảnh upload lên

        public string? ExistingImage { get; set; } // Đường dẫn ảnh cũ (dùng khi edit)

        [Display(Name = "Trạng thái hoạt động")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Yêu cầu đặt cọc")]
        public bool RequiresDeposit { get; set; }

        // --- MỚI: Quản lý tiêu hao ---
        public List<ServiceConsumableDto> Consumables { get; set; } = new List<ServiceConsumableDto>();
        public List<SpaBookingWeb.Models.Product>? AvailableProducts { get; set; }
    }

    public class ServiceConsumableDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string? ProductName { get; set; }
        public string? UnitName { get; set; }
    }
}