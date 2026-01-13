namespace SpaBookingWeb.ViewModels.Receptionist
{
    public class BookingSubmissionVM
    {
        public string CustomerPhone { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public DateTime BookingDate { get; set; } // Ngày đặt (Mới thêm)
        public string StartTime { get; set; }     // Giờ bắt đầu (08:00)
        public string Notes { get; set; }
        public bool IsDepositPaid { get; set; }   // Đã thu cọc chưa

        // Danh sách khách và dịch vụ họ làm
        public List<BookingGuestVM> Guests { get; set; }
        public bool IsWalkIn { get; set; }
    }

    public class BookingGuestVM
    {
        public string GuestName { get; set; } // Tên khách (Chính hoặc phụ)
        public List<BookingItemVM> Items { get; set; }
    }

    public class BookingItemVM
    {
        public int ServiceId { get; set; }
        public int? ComboId { get; set; }      // Nếu món này thuộc combo
        public int TechnicianId { get; set; }
        public string StartTime { get; set; }  // Giờ bắt đầu service này (JS tính)
        public string EndTime { get; set; }    // Giờ kết thúc service này (JS tính)
    }
}
