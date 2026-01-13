using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public class ReviewService : IReviewService
    {
        private readonly ApplicationDbContext _context;

        public ReviewService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReviewDashboardViewModel> GetReviewDashboardAsync()
        {
            // Get all reviews with related information
            var reviews = await _context.Reviews
                .Include(r => r.Appointment)
                .ThenInclude(a => a.Customer)
                .Include(r => r.Appointment)
                .ThenInclude(a => a.Employee)
                .ThenInclude(e => e.ApplicationUser) // To get employee name if needed from User table
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            // Get additional service information (because M-N relationship via AppointmentDetails is complex)
            // For optimization, we can load AppointmentDetails separately or use Select overlay
            // Here I choose manual mapping to handle service name string

            var reviewDtos = new List<ReviewListViewModel>();

            foreach (var r in reviews)
            {
                // Get names of services in this appointment
                var serviceNames = await _context.AppointmentDetails
                    .Where(ad => ad.AppointmentId == r.AppointmentId)
                    .Include(ad => ad.Service)
                    .Select(ad => ad.Service != null ? ad.Service.ServiceName : "Other Service")
                    .ToListAsync();

                string servicesDisplay = serviceNames.Any() ? string.Join(", ", serviceNames) : "Unknown";

                reviewDtos.Add(new ReviewListViewModel
                {
                    ReviewId = r.ReviewId,
                    AppointmentId = r.AppointmentId,
                    CustomerName = r.Appointment?.Customer?.FullName ?? "Walk-in Guest",
                    EmployeeName = r.Appointment?.Employee?.FullName ?? "Unspecified",
                    ServiceName = servicesDisplay,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedDate = r.CreatedDate
                });
            }

            // Calculate Statistics
            var viewModel = new ReviewDashboardViewModel
            {
                Reviews = reviewDtos,
                TotalReviews = reviewDtos.Count,
                AverageRating = reviewDtos.Any() ? Math.Round(reviewDtos.Average(rv => rv.Rating), 1) : 0,
                FiveStarCount = reviewDtos.Count(rv => rv.Rating == 5),
                OneStarCount = reviewDtos.Count(rv => rv.Rating == 1)
            };

            return viewModel;
        }

        public async Task DeleteReviewAsync(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                _context.Reviews.Remove(review); // Hard delete because Review table usually doesn't need Soft Delete or doesn't have IsDeleted column
                await _context.SaveChangesAsync();
            }
        }
    }
}