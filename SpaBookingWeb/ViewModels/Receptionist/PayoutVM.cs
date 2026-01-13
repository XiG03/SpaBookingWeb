namespace SpaBookingWeb.ViewModels.Receptionist
{
    // Dữ liệu hiển thị cho từng KTV ở bảng tổng hợp
    public class TechPayoutSummaryVM
    {
        public int TechnicianId { get; set; }
        public string TechnicianName { get; set; }
        public string Avatar { get; set; }
        public int TotalTransactionCount { get; set; } // Số đơn
        public decimal TotalUnpaidTip { get; set; }    // Tổng tiền Tip đang giữ
    }

    // Dữ liệu chi tiết từng đơn (để hiện trong Modal đối soát)
    public class PayoutDetailItemVM
    {
        public int DetailId { get; set; }
        public DateTime Date { get; set; }
        public string CustomerName { get; set; }
        public string ServiceName { get; set; }
        public decimal TipAmount { get; set; }
    }
}
