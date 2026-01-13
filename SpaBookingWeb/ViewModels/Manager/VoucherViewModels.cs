using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class VoucherDashboardViewModel
    {
        public List<VoucherViewModel> Vouchers { get; set; }
        public int ActiveCount { get; set; }
        public int TotalUsed { get; set; }
    }

    public class VoucherViewModel
    {
        public int VoucherId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string ValueDisplay { get; set; }
        public string MinSpendDisplay { get; set; }
        public string DateRange { get; set; }
        public string UsageStatus { get; set; } // Hiển thị dạng "10/100"
        public bool IsActive { get; set; }
    }

    public class CreateVoucherViewModel
    {
        public int VoucherId { get; set; }

        [Required(ErrorMessage = "Please enter voucher name")]
        [Display(Name = "Voucher Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter voucher code")]
        [StringLength(50, ErrorMessage = "Code max 50 characters")]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Discount Type")]
        public string DiscountType { get; set; } // Select: "Percent" hoặc "Amount"

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "Value must be greater than 0")]
        [Display(Name = "Discount Value")]
        public decimal DiscountValue { get; set; }

        [Display(Name = "Max Discount (VND)")]
        public decimal? MaxDiscountAmount { get; set; }

        [Display(Name = "Min Order (VND)")]
        public decimal MinSpend { get; set; }

        [Display(Name = "Usage Limit")]
        public int UsageLimit { get; set; } = 100;

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);

        public bool IsActive { get; set; } = true;
    }
}

