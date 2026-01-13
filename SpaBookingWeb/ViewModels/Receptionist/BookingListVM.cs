namespace SpaBookingWeb.ViewModels.Receptionist
{
    public class BookingListItemVM
    {
        public int AppointmentId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime BookingTime { get; set; } // Ngày giờ hẹn
        public string ServiceSummary { get; set; } // Ví dụ: "Cắt tóc + 2 dịch vụ"
        public decimal TotalAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; } // Người tạo đơn
    }
}
