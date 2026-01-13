namespace SpaBookingWeb.ViewModels.Receptionist
{
    // ViewModel tổng cho trang Dashboard
    public class CalendarVM
    {
        public DateTime SelectedDate { get; set; }

        // Danh sách KTV (Đã phân nhóm để hiển thị đẹp hơn)
        public List<TechnicianGroup> TechnicianGroups { get; set; } = new List<TechnicianGroup>();

        // Các cục Lịch hẹn đã tính toán giờ giấc
        public List<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();

        // --- Dữ liệu dùng cho Modal Đặt Lịch (Sẽ dùng ở Bước sau) ---
        public List<ServiceItem> Services { get; set; } = new List<ServiceItem>();
        public List<ComboItem> Combos { get; set; } = new List<ComboItem>();

        // 1. Thêm danh sách luật đặt cọc
        public List<DepositRuleItem> DepositRules { get; set; } = new List<DepositRuleItem>();
    }

    // Nhóm KTV (Ví dụ: Nhóm Hair, Nhóm Spa)
    public class TechnicianGroup
    {
        public string GroupName { get; set; }
        public List<TechnicianResource> Technicians { get; set; } = new List<TechnicianResource>();
    }

    // Thông tin 1 KTV
    public class TechnicianResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
        public string SkillLevel { get; set; } // Hiển thị dưới tên (VD: Master, Junior)

        // --- THÊM THÔNG TIN CA LÀM ---
        // Lưu dạng số phút từ 00:00 (VD: 8h = 480) để JS dễ so sánh
        public int ShiftStartMinutes { get; set; }
        public int ShiftEndMinutes { get; set; }

        // Danh sách ID các dịch vụ mà KTV này có kỹ năng làm
        public List<int> AllowedServiceIds { get; set; } = new List<int>();
    }

    // Đại diện cho 1 khối màu trên lịch (1 AppointmentDetail)
    public class CalendarEvent
    {
        public int AppointmentDetailId { get; set; }
        public int AppointmentId { get; set; } // Để gom nhóm khi click xem chi tiết
        public int TechnicianId { get; set; }

        public string CustomerName { get; set; } // Tên người đặt chính
        public string? GuestName { get; set; }   // Tên khách đi kèm (QUAN TRỌNG)
        public string ServiceName { get; set; }

        public DateTime StartTime { get; set; } // Giờ tính toán
        public DateTime EndTime { get; set; }   // Giờ tính toán

        public string Status { get; set; } // Pending, Confirmed, InProgress...
        public string ColorClass { get; set; }
    }

    // Class phụ cho Dropdown chọn dịch vụ
    public class ServiceItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Duration { get; set; }
        public decimal Price { get; set; }
        public int? CategoryId { get; set; }
    }

    public class ComboItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        // Danh sách ServiceId con trong combo này (để JS xử lý ẩn hiện)
        public List<int> ServiceIds { get; set; } = new List<int>();

        // === CẬP NHẬT MỚI ===
        // 1. Thêm danh sách luật đặt cọc
        public List<DepositRuleItem> DepositRules { get; set; } = new List<DepositRuleItem>();

        // 2. Dữ liệu KTV cần thêm thông tin Ca làm việc (Start/End)
        public List<TechnicianGroup> TechnicianGroups { get; set; } = new List<TechnicianGroup>();
        
        // Dữ liệu bổ trợ dropdown
        public List<ServiceItem> Services { get; set; } = new List<ServiceItem>();
        public List<ComboItem> Combos { get; set; } = new List<ComboItem>();
    }

    public class DepositRuleItem
    {
        public decimal MinOrderValue { get; set; } // Đơn tối thiểu để áp dụng
        public string DepositType { get; set; } // "Percent" hoặc "Fixed"
        public decimal DepositValue { get; set; } // Giá trị (VD: 20 hoặc 50000)
    }
}
