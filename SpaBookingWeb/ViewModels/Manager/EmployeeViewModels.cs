using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SpaBookingWeb.ViewModels.Manager
{
    // 1. ViewModel cho Danh sách nhân viên (CRUD)
    public class EmployeeListViewModel
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Position { get; set; } = "Technician";
        public string Avatar { get; set; } = string.Empty;
        
        public double AverageRating { get; set; }
        public int TotalAppointments { get; set; }
        public bool IsActive { get; set; }
    }

    // 2. ViewModel cho Form Thêm/Sửa nhân viên (CRUD)
    public class EmployeeViewModel
    {
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string Gender { get; set; } = "Nữ";

        [Required]
        public decimal BaseSalary { get; set; }

        public DateTime HireDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        // --- MỚI: PHÂN QUYỀN & DỊCH VỤ ---
        
        [Display(Name = "Vai trò hệ thống")]
        public string SelectedRoleId { get; set; }
        public List<SelectListItem> Roles { get; set; } = new List<SelectListItem>();

        [Display(Name = "Dịch vụ đảm nhận (Dành cho KTV)")]
        public List<int> SelectedServiceIds { get; set; } = new List<int>();
        public List<SelectListItem> Services { get; set; } = new List<SelectListItem>();
    }

    // 3. ViewModel cho Lịch làm việc & Điểm danh
    public class DailyScheduleViewModel
    {
        public DateTime Date { get; set; }
        // Danh sách các ca làm việc trong ngày
        public List<ShiftAssignmentDto> Shifts { get; set; } = new List<ShiftAssignmentDto>();
    }

    public class ShiftAssignmentDto
    {
        public int ShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        
        // Danh sách nhân viên được phân vào ca này (Kèm trạng thái điểm danh)
        public List<WorkScheduleViewModel> Schedules { get; set; } = new List<WorkScheduleViewModel>();
    }

    // Chi tiết một phân công làm việc (để hiển thị trạng thái điểm danh)
    public class WorkScheduleViewModel
    {
        public int ScheduleId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Position { get; set; } = "KTV";
        
        // Trạng thái điểm danh
        public bool IsPresent { get; set; }
        public string Note { get; set; } = string.Empty; // Ghi chú (đến muộn, về sớm...)
    }

    // Dùng cho Dropdown chọn ca làm việc
    public class ShiftViewModel
    {
        public int ShiftId { get; set; }
        public string ShiftName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    // 4. ViewModel cho Quản lý Tiền Tip
    public class DailyTipViewModel
    {
        public int TipId { get; set; } // Có thể là AppointmentId
        public DateTime CreatedDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsDistributed { get; set; } // Đã trả cho nhân viên chưa
    }

    // 5. ViewModel cho Lương
    
}