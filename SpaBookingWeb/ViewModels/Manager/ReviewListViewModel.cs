using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class ReviewListViewModel
    {
        public int ReviewId { get; set; }
        public int AppointmentId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty; // Dịch vụ chính hoặc "Nhiều dịch vụ"
        public string EmployeeName { get; set; } = string.Empty; // Nhân viên phục vụ
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class ReviewDashboardViewModel
    {
        public List<ReviewListViewModel> Reviews { get; set; } = new List<ReviewListViewModel>();
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int FiveStarCount { get; set; }
        public int OneStarCount { get; set; }
    }
}