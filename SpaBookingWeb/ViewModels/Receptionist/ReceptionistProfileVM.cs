namespace SpaBookingWeb.ViewModels.Receptionist
{
    public class ReceptionistProfileVM
    {
        // === KHỐI 1: ĐỊNH DANH (IDENTITY) ===
        public string FullName { get; set; }
        public string EmployeeCode { get; set; } // Mã NV (VD: LT-001)
        public string Avatar { get; set; }
        public string JobTitle { get; set; } // Chức danh
        public bool IsActive { get; set; }

        // === KHỐI 2: THÔNG TIN NHÂN SỰ (HR INFO) ===
        // Đã bỏ "Chi nhánh" theo yêu cầu
        public DateTime HireDate { get; set; } // Ngày vào làm
        public int SeniorityMonths { get; set; } // Thâm niên (tháng)
        public decimal BaseSalary { get; set; } // Lương cứng
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }

        // === KHỐI 3: HIỆU SUẤT THÁNG NÀY (PERFORMANCE) ===
        // Đã bỏ "Hoa hồng tạm tính" theo yêu cầu
        public decimal CurrentMonthSales { get; set; } // Doanh số cá nhân
        public int CustomersServed { get; set; } // Số khách đã tiếp đón (Check-in)
        public double CancellationRate { get; set; } // Tỷ lệ hủy lịch (%)

        // === [THÊM MỚI] DANH SÁCH THỐNG KÊ 6 THÁNG ===
        public List<MonthlyStat> MonthlyStats { get; set; } = new List<MonthlyStat>();

        // === [CẬP NHẬT] CÁC KHỐI DỮ LIỆU (1, 3, 4) ===
        public AttendanceInfo Attendance { get; set; } = new AttendanceInfo();
        public QualityInfo Quality { get; set; } = new QualityInfo();

        // [MỚI] Mục 3: Nhật ký hoạt động
        public List<ActivityItem> RecentActivities { get; set; } = new List<ActivityItem>();

        // [THÊM MỚI] Danh sách lịch sử lương
        public List<SalaryHistoryItem> SalaryHistory { get; set; } = new List<SalaryHistoryItem>();
    }

    // Class con lưu dữ liệu từng tháng
    public class MonthlyStat
    {
        public string MonthLabel { get; set; } // VD: "T8", "T9"
        public decimal TotalRevenue { get; set; } // Doanh số
        public int CustomerCount { get; set; } // Số khách
    }

    // 1. Thông tin Chấm công (Mục 1)
    public class AttendanceInfo
    {
        public int WorkDaysActual { get; set; }
        public int WorkDaysStandard { get; set; }
        public int LateMinutesTotal { get; set; }
        public List<DailyLog> RecentLogs { get; set; } = new List<DailyLog>();
    }

    public class DailyLog
    {
        public DateTime Date { get; set; }
        public TimeSpan? CheckIn { get; set; }
        public TimeSpan? CheckOut { get; set; }
        public string Status { get; set; }
        public string StatusColor { get; set; }
    }

    // 3. Nhật ký hoạt động (Mục 3 - Thay thế KPI)
    public class ActivityItem
    {
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; } // "Booking", "Payment", "Cancel"
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string ColorClass { get; set; }
    }

    // 4. Thông tin Chất lượng (Mục 4)
    public class QualityInfo
    {
        public double UpsellRate { get; set; }
        public List<TopServiceItem> TopServices { get; set; } = new List<TopServiceItem>();
        // === [MỚI] THỐNG KÊ TRẠNG THÁI LỊCH HẸN ===
        public StatusBreakdown StatusStats { get; set; } = new StatusBreakdown();
    }

    public class TopServiceItem
    {
        public string ServiceName { get; set; }
        public int Quantity { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class StatusBreakdown
    {
        public int Pending { get; set; }    // Chờ khách đến
        public int InProgress { get; set; } // Đang phục vụ
        public int Completed { get; set; }  // Hoàn thành
        public int Cancelled { get; set; }  // Hủy
    }

    // [THÊM MỚI] Class con để chứa thông tin từng tháng lương
    public class SalaryHistoryItem
    {
        public int SalaryId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal TotalSalary { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal Commission { get; set; }
        public decimal Bonus { get; set; }
        public decimal Deduction { get; set; }
        public string Status { get; set; } // "Pending" hoặc "Completed"
        public DateTime? PaymentDate { get; set; } // Ngày thanh toán (nếu có cột này, tạm thời dùng CreatedDate hoặc null)
    }
}
