using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Client
{
    public class AppointmentSuccessViewModel
    {
        public int AppointmentId { get; set; }
        public string CustomerName { get; set; }
        public string TimeSlot { get; set; }
        public List<ServiceSuccessItem> Services { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public bool IsDepositPaid { get; set; }
    }

    public class ServiceSuccessItem
    {
        public string ServiceName { get; set; }
        public string StaffName { get; set; }
        public int Duration { get; set; }
        public decimal Price { get; set; }
    }
}