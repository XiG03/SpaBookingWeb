using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class DashboardViewModel
    {
        // 1. Thống kê lịch hẹn trong ngày
        public int TotalAppointmentsToday { get; set; }
        public int PendingAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CancelledAppointments { get; set; }
        
        // Doanh thu ước tính hôm nay (Optional)
        // Doanh thu ước tính hôm nay (Optional)
        public decimal EstimatedRevenueToday { get; set; }

        // 2. Danh sách lịch hẹn hôm nay (Chi tiết)
        public List<AppointmentItemViewModel> TodayAppointments { get; set; } = new List<AppointmentItemViewModel>();

        // 3. Danh sách nhân viên điểm danh theo ca
        public List<ShiftAttendanceViewModel> ShiftAttendances { get; set; } = new List<ShiftAttendanceViewModel>();
    }

    public class AppointmentItemViewModel
    {
        public int AppointmentId { get; set; }
        public string CustomerName { get; set; }
        public string EmployeeName { get; set; } // Nhân viên phụ trách chính
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ServiceNames { get; set; } // Tên các dịch vụ (nối chuỗi)
        public string Status { get; set; } // Pending, Confirmed, Completed, Cancelled
        public string StatusColor { get; set; } // Class màu sắc cho badge (success, warning...)
    }

    public class ShiftAttendanceViewModel
    {
        public string ShiftName { get; set; }
        public string TimeRange { get; set; }
        public List<EmployeeAttendanceStatus> Employees { get; set; } = new List<EmployeeAttendanceStatus>();
    }

    public class EmployeeAttendanceStatus
    {
        public string FullName { get; set; }
        public string Avatar { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? CheckInTime { get; set; }
    }

    // Dùng cho API Calendar (trả về JSON)
    public class CalendarEventViewModel
    {
        public int id { get; set; }
        public string title { get; set; } // Tên khách + Dịch vụ
        public string start { get; set; } // ISO 8601 format
        public string end { get; set; }
        public string className { get; set; } // Màu sắc event (bg-success, bg-info...)
        public string description { get; set; } // Chi tiết hiển thị khi hover/click
        public string url { get; set; } // Link chi tiết khi click
    }

    // MỚI: ViewModel chi tiết lịch hẹn
    public class AppointmentDetailViewModel
    {
        public int AppointmentId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsDepositPaid { get; set; }
        public decimal DepositAmount { get; set; }
        public bool IsPaidFull { get; set; } // Đã thanh toán hết chưa
        public List<ServiceDetailDto> Services { get; set; } = new List<ServiceDetailDto>();
    }

    public class ServiceDetailDto
    {
        public string ServiceName { get; set; }
        public string TechnicianName { get; set; }
        public decimal Price { get; set; }
    }
}