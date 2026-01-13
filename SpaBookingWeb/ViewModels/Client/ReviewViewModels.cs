using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Client
{
    public class ReviewPageViewModel
    {
        public int AppointmentId { get; set; }
        public string SpaName { get; set; } = "Lotus Spa";
        
        // Danh sách dịch vụ/combo trong lịch hẹn này để khách chọn review (nếu muốn specific)
        public List<ReviewServiceItem> UsedServices { get; set; } = new List<ReviewServiceItem>();
    }

    public class ReviewServiceItem
    {
        public string Id { get; set; } // Format: "service_1" hoặc "combo_2"
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Type { get; set; } // Service / Combo
    }

    public class SubmitReviewModel
    {
        [Required]
        public int AppointmentId { get; set; }

        public string SelectedItemId { get; set; } // Item khách chọn trong dropdown (Optional logic)

        [Range(1, 5, ErrorMessage = "Please select a star rating (1-5).")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Please enter your review content.")]
        public string Comment { get; set; }

        public bool IsAnonymous { get; set; }
    }
}