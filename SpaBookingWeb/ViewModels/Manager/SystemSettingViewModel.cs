using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering; // Cần cho SelectList
using SpaBookingWeb.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class SystemSettingViewModel
    {
        // --- 1. CẤU HÌNH CHUNG SPA (Map từ bảng SystemSettings) ---
        // Không cần ID vì dùng SettingKey làm chuẩn
        
        [Display(Name = "Spa Name")]
        [Required(ErrorMessage = "Please enter Spa name")]
        public string SpaName { get; set; }

        [Display(Name = "Phone Number")]
        [Required(ErrorMessage = "Please enter phone number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Contact Email")]
        [EmailAddress(ErrorMessage = "Invalid Email")]
        public string Email { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Spa Logo")]
        public string LogoUrl { get; set; }

        [Display(Name = "Select New Logo")]
        public IFormFile LogoFile { get; set; }

        [Display(Name = "Facebook Link")]
        public string FacebookUrl { get; set; }

        [Display(Name = "Opening Time")]
        public TimeSpan OpenTime { get; set; }

        [Display(Name = "Closing Time")]
        public TimeSpan CloseTime { get; set; }

        // --- 2. QUẢN LÝ DANH SÁCH (Hiển thị) ---
        public List<Unit> Units { get; set; } = new List<Unit>();
        public List<DepositRule> DepositRules { get; set; } = new List<DepositRule>();

        // --- 3. THUỘC TÍNH ĐỂ TẠO MỚI (Input Fields) ---

        // A. Tạo Unit mới
        [Display(Name = "New Unit Name")]
        public string NewUnitName { get; set; }


        // C. Tạo DepositRule mới (Các thuộc tính ban đầu cần thiết)
        [Display(Name = "Rule Name")]
        public string NewRuleName { get; set; }

        [Display(Name = "Apply To")]
        public string NewApplyToType { get; set; } = "OrderTotal"; // Mặc định: Tổng đơn hàng

        [Display(Name = "Min Order Value")]
        public decimal? NewMinOrderValue { get; set; }

        [Display(Name = "Applicable Service")]
        public int? NewTargetServiceId { get; set; }

        [Display(Name = "Applicable Membership Tier")]
        public int? NewTargetMembershipTypeId { get; set; }

        [Display(Name = "Deposit Type")]
        public string NewDepositType { get; set; } = "Percent"; // Mặc định: Phần trăm

        [Display(Name = "Deposit Value")]
        public decimal NewDepositValue { get; set; }

        public int DepositPercentage { get; set; }

        // --- 4. DỮ LIỆU HỖ TRỢ DROPDOWN (Select Lists) ---
        public IEnumerable<SelectListItem> AvailableServices { get; set; }
        public IEnumerable<SelectListItem> AvailableMembershipTypes { get; set; }
        
        // Danh sách cứng cho các loại ApplyToType và DepositType
        public List<SelectListItem> ApplyToTypes { get; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "OrderTotal", Text = "Total Order Value" },
            new SelectListItem { Value = "SpecificService", Text = "Specific Service" },
            new SelectListItem { Value = "MembershipType", Text = "Membership Tier" }
        };

        public List<SelectListItem> DepositTypes { get; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "Percent", Text = "Percent (%)" },
            new SelectListItem { Value = "FixedAmount", Text = "Fixed Amount (VND)" }
        };

        // --- 5. DANH SÁCH KHÁCH HÀNG (Cho tab Nâng quyền) ---
        public List<CustomerViewModel> AllCustomers { get; set; } = new List<CustomerViewModel>();
    }

    public class CustomerViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public int Point { get; set; }
    }
}