using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Client
{
    public class AppointmentHistoryViewModel
    {
        public int AppointmentId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        
        // Trạng thái: Pending (Chờ xác nhận), Confirmed (Đã xác nhận), Cancelled (Đã hủy), Completed (Hoàn thành)
        public string Status { get; set; } 
        public bool IsDepositPaid { get; set; }
        
        public decimal TotalAmount { get; set; }
        public decimal DepositAmount { get; set; }

        public string SpaName { get; set; } = "Lotus Spa"; // Mặc định hoặc lấy từ Setting
        public string SpaAddress { get; set; } = "123 Đường Nguyễn Huệ, Quận 1";

        public List<ServiceDetailViewModel> Services { get; set; } = new List<ServiceDetailViewModel>();
    }

    public class ServiceDetailViewModel
    {
        public string ServiceName { get; set; }
        public int Duration { get; set; }
        public string StaffName { get; set; }
        public decimal Price { get; set; }
        public string StaffAvatar { get; set; }
    }
}