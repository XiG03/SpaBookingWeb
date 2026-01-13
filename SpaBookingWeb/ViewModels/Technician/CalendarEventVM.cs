namespace SpaBookingWeb.ViewModels.Technician
{
    public class CalendarEventVM
    {
        public int Id { get; set; }          // ID để click vào xem chi tiết
        public string Title { get; set; }    // Tên khách hàng
        public string ServiceName { get; set; } // Tên dịch vụ hiển thị
        public string Start { get; set; }    // Thời gian bắt đầu (ISO String)
        public string ColorClass { get; set; } // Màu nền (Tailwind)
        public string SubColor { get; set; } // Màu chữ (Tailwind)
    }
}
