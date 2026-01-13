using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class ServiceDashboardViewModel
    {
        // List of service details for display in table
        public List<ServiceStatisticDto> Services { get; set; } = new List<ServiceStatisticDto>();

        // Overview statistics
        public int TotalActiveServices { get; set; } // Number of usable services
        public int TotalServices { get; set; }

        // Chart data (Arrays for JS conversion)
        public List<string> ChartLabels { get; set; } = new List<string>(); // Service Names
        public List<int> ChartUsageCount { get; set; } = new List<int>();   // Usage Count
        public List<decimal> ChartRevenue { get; set; } = new List<decimal>(); // Revenue
    }

    // DTO class defined here instead of separate file
    public class ServiceStatisticDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int UsageCount { get; set; }      // Number of times booked
        public decimal TotalRevenue { get; set; } // Total revenue from this service
        public bool IsActive { get; set; }
    }
    public class ServiceViewModel
    {
        public int ServiceId { get; set; }

        [Display(Name = "Service Name")]
        [Required(ErrorMessage = "Please enter service name")]
        [StringLength(200, ErrorMessage = "Service name cannot exceed 200 characters")]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "Price (VND)")]
        [Required(ErrorMessage = "Please enter price")]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
        public decimal Price { get; set; }

        [Display(Name = "Duration (minutes)")]
        [Required(ErrorMessage = "Please enter duration")]
        [Range(1, 1440, ErrorMessage = "Duration must be between 1 minute and 24 hours")]
        public int DurationMinutes { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Image")]
        public IFormFile? ImageFile { get; set; } // Uploaded image file

        public string? ExistingImage { get; set; } // Old image path (used when editing)

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Requires Deposit")]
        public bool RequiresDeposit { get; set; }

        // --- NEW: Consumable Management ---
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