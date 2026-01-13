using SpaBookingWeb.Models;

namespace SpaBookingWeb.ViewModels.Technician
{
    public class JobDetailVM
    {
        public int AppointmentDetailId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public string ServiceName { get; set; }
        public int DurationMinutes { get; set; }
        public decimal TipAmount { get; set; }
        public bool IsDirectTip { get; set; }
        public DateTime? TipPayoutDate { get; set; }
        public List<AppointmentConsumable> Consumables { get; set; } = new List<AppointmentConsumable>();
    }
}
