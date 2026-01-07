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
        
        [Display(Name = "Tên Spa")]
        [Required(ErrorMessage = "Vui lòng nhập tên Spa")]
        public string SpaName { get; set; }

        [Display(Name = "Số điện thoại")]
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Email liên hệ")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        [Display(Name = "Logo Spa")]
        public string LogoUrl { get; set; }

        [Display(Name = "Chọn Logo mới")]
        public IFormFile LogoFile { get; set; }

        [Display(Name = "Link Facebook")]
        public string FacebookUrl { get; set; }

        [Display(Name = "Giờ mở cửa")]
        public TimeSpan OpenTime { get; set; }

        [Display(Name = "Giờ đóng cửa")]
        public TimeSpan CloseTime { get; set; }

        // --- 2. QUẢN LÝ DANH SÁCH (Hiển thị) ---
        public List<Unit> Units { get; set; } = new List<Unit>();
        public List<DepositRule> DepositRules { get; set; } = new List<DepositRule>();

        // --- 3. THUỘC TÍNH ĐỂ TẠO MỚI (Input Fields) ---

        // A. Tạo Unit mới
        [Display(Name = "Tên đơn vị tính mới")]
        public string NewUnitName { get; set; }


        // C. Tạo DepositRule mới (Các thuộc tính ban đầu cần thiết)
        [Display(Name = "Tên quy tắc")]
        public string NewRuleName { get; set; }

        [Display(Name = "Áp dụng cho")]
        public string NewApplyToType { get; set; } = "OrderTotal"; // Mặc định: Tổng đơn hàng

        [Display(Name = "Giá trị đơn tối thiểu")]
        public decimal? NewMinOrderValue { get; set; }

        [Display(Name = "Dịch vụ áp dụng")]
        public int? NewTargetServiceId { get; set; }

        [Display(Name = "Hạng thành viên áp dụng")]
        public int? NewTargetMembershipTypeId { get; set; }

        [Display(Name = "Loại cọc")]
        public string NewDepositType { get; set; } = "Percent"; // Mặc định: Phần trăm

        [Display(Name = "Giá trị cọc")]
        public decimal NewDepositValue { get; set; }

        public int DepositPercentage { get; set; }

        // --- 4. DỮ LIỆU HỖ TRỢ DROPDOWN (Select Lists) ---
        public IEnumerable<SelectListItem> AvailableServices { get; set; }
        public IEnumerable<SelectListItem> AvailableMembershipTypes { get; set; }
        
        // Danh sách cứng cho các loại ApplyToType và DepositType
        public List<SelectListItem> ApplyToTypes { get; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "OrderTotal", Text = "Tổng giá trị đơn hàng" },
            new SelectListItem { Value = "SpecificService", Text = "Dịch vụ cụ thể" },
            new SelectListItem { Value = "MembershipType", Text = "Hạng thành viên" }
        };

        public List<SelectListItem> DepositTypes { get; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "Percent", Text = "Phần trăm (%)" },
            new SelectListItem { Value = "FixedAmount", Text = "Số tiền cố định (VNĐ)" }
        };
    }
}