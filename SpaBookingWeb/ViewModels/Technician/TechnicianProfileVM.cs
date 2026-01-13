namespace SpaBookingWeb.ViewModels.Technician
{
    public class TechnicianProfileVM
    {
        // 1. Thông tin cá nhân
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string AvatarUrl { get; set; } // Ảnh giả lập

        // 2. Thông tin hợp đồng
        public DateTime HireDate { get; set; }
        public decimal BaseSalary { get; set; } // Lương cứng
        public bool IsActive { get; set; }

        // 3. Thống kê Tháng hiện tại (Dashboard)
        public int ServedCustomers { get; set; }    // Số khách đã làm
        public decimal TotalTip { get; set; }       // Tổng tiền Tip
        public decimal TodayTip { get; set; }       // <--- THÊM DÒNG NÀY (Tổng ngày)
        public int WorkDays { get; set; }           // Số ngày đi làm
        public double TotalWorkHours { get; set; }  // Tổng giờ làm
    }
}
