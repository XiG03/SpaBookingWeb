namespace SpaBookingWeb.ViewModels.Technician
{
    public class ConsumableItemVM
    {
        public int UsageId { get; set; } // = 0 nếu là thêm mới
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string UnitName { get; set; }

        public int StandardQuantity { get; set; } // Định mức (để tham khảo)
        public int ActualQuantity { get; set; }   // Thực tế dùng
        public string Reason { get; set; }        // Lý do chênh lệch
    }
}
