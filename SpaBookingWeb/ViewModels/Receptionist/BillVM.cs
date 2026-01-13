namespace SpaBookingWeb.ViewModels.Receptionist
{
    // Dùng để hiển thị lên Modal
    public class BillVM
    {
        public int AppointmentId { get; set; }
        public string CustomerName { get; set; }
        public string MembershipLevel { get; set; }
        public double MembershipDiscountPercent { get; set; } // VD: 10%
        public decimal DepositAmount { get; set; } // Tiền đã cọc
        public string VoucherCode { get; set; }
        public List<BillItemVM> Items { get; set; } = new List<BillItemVM>();
    }

    public class BillItemVM
    {
        public int DetailId { get; set; }
        public string ServiceName { get; set; }
        public string TechName { get; set; }
        public decimal Price { get; set; }
        public decimal TipAmount { get; set; } // Tip nhập vào
        public bool IsSelected { get; set; } = true; // Mặc định chọn
    }

    // Dùng để nhận dữ liệu tính toán & thanh toán từ Client gửi lên
    public class CheckoutRequestVM
    {
        public int AppointmentId { get; set; }
        public string VoucherCode { get; set; }
        public string PaymentMethod { get; set; } // "Cash" hoặc "Momo"
        public List<BillItemVM> Items { get; set; } // Danh sách tick chọn và tip
    }

    // Kết quả tính toán trả về cho JS
    public class BillCalculationResult
    {
        public decimal SubTotal { get; set; }       // Tổng tiền hàng
        public decimal MemberDiscount { get; set; } // Tiền giảm thành viên
        public decimal VoucherDiscount { get; set; } // Tiền giảm voucher
        public string VoucherMessage { get; set; }   // Thông báo voucher (VD: "Giảm 50k")
        public bool IsVoucherValid { get; set; }
        public decimal TotalTip { get; set; }       // Tổng tip
        public decimal Deposit { get; set; }        // Cọc
        public decimal FinalAmount { get; set; }    // KHÁCH CẦN TRẢ
    }
}
