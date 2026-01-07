using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Client
{
    // ==========================================
    // 1. SESSION MODEL (Lưu trạng thái đặt lịch)
    // ==========================================
    public class BookingSessionModel
    {
        public bool IsGroupBooking { get; set; }
        public List<BookingMember> Members { get; set; } = new List<BookingMember>();

        // Thông tin chung
        public DateTime? SelectedDate { get; set; }
        public TimeSpan? SelectedTime { get; set; }
        public CustomerInfo CustomerInfo { get; set; } = new CustomerInfo();

        // Tài chính
        public decimal TotalAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public int DepositPercentage { get; set; }
    }

    public class BookingMember
    {
        public int MemberIndex { get; set; }
        public string Name { get; set; }
        public List<int> SelectedServiceIds { get; set; } = new List<int>();

        // [MỚI] Lưu nhân viên theo từng dịch vụ: Key = ServiceId, Value = StaffId (null = Random)
        public Dictionary<int, int?> ServiceStaffMap { get; set; } = new Dictionary<int, int?>();

        // Dữ liệu chi tiết để hiển thị lên View (Tên, Giá...)
        public List<ServiceItemViewModel> SelectedServices { get; set; } = new List<ServiceItemViewModel>();
    }

    public class CustomerInfo
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; }
        public string Note { get; set; }
    }

    // ==========================================
    // 2. VIEW MODELS CHO TỪNG BƯỚC (Hiển thị)
    // ==========================================

    public class ServiceItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public string Type { get; set; } // "Service" hoặc "Combo"
        public bool IsSelected { get; set; }
    }

    public class ServiceCategoryGroupViewModel
    {
        public string CategoryName { get; set; }
        public List<ServiceItemViewModel> Services { get; set; } = new List<ServiceItemViewModel>();
    }

    public class StaffViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Avatar { get; set; }
    }

    public class BookingPageViewModel // Dùng chung để load data
    {
        public List<ServiceCategoryGroupViewModel> ServiceCategories { get; set; } = new List<ServiceCategoryGroupViewModel>();
        public List<StaffViewModel> Staffs { get; set; } = new List<StaffViewModel>();
    }

    // ViewModel cụ thể cho Step 2
    public class Step2ViewModel
    {
        public bool IsGroup { get; set; }
        public List<ServiceCategoryGroupViewModel> ServiceCategories { get; set; }
        public BookingSessionModel CurrentSession { get; set; }
    }

    // ViewModel cụ thể cho Step 3
    public class Step3ViewModel
    {
        public BookingSessionModel CurrentSession { get; set; }
        public List<StaffViewModel> Staffs { get; set; }
        // Dùng để hiển thị giá/tên dịch vụ trong Sidebar
        public decimal TotalAmount { get; set; }
        public int TotalDuration { get; set; }
    }

    // ViewModel cụ thể cho Step 4
    public class Step4ViewModel
    {
        public BookingSessionModel CurrentSession { get; set; }
        public List<string> AvailableTimeSlots { get; set; }

        // [MỚI] Dùng để tra cứu tên nhân viên hiển thị ở Sidebar
        public List<StaffViewModel> Staffs { get; set; }

        // [MỚI] Thông tin tổng hợp
        public decimal TotalAmount { get; set; }
        public int TotalDuration { get; set; }

        // [MỚI] Giờ mở/đóng cửa để hiển thị UI nếu cần
        public string OpenTimeStr { get; set; }
        public string CloseTimeStr { get; set; }
    }

    // ViewModel cụ thể cho Step 5
    public class Step5ViewModel
    {
        public BookingSessionModel CurrentSession { get; set; }
        public int DepositPercent { get; set; }
        public decimal DepositAmount { get; set; }
        public List<StaffViewModel> Staffs { get; set; }
    }
}