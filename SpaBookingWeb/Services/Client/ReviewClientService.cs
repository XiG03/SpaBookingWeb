using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public class ReviewClientService : IReviewClientService
    {
        private readonly ApplicationDbContext _context;

        public ReviewClientService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReviewPageViewModel> GetReviewPageDataAsync(int appointmentId, string userEmail)
        {
            // 1. Get Appointment and check permissions
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Combo)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null) return null;

            // Validate email (ensure correct reviewer)
            if (appointment.Customer.Email != userEmail) return null;

            // Validate status (only review when completed)
            // if (appointment.Status != "Completed") return null; // Uncomment when running in production

            var model = new ReviewPageViewModel
            {
                AppointmentId = appointment.AppointmentId,
                SpaName = "Lotus Spa & Salon" // Or get from SystemSettings
            };

            // 2. Map list of used services
            foreach (var detail in appointment.AppointmentDetails)
            {
                if (detail.ServiceId.HasValue)
                {
                    model.UsedServices.Add(new ReviewServiceItem
                    {
                        Id = $"service_{detail.ServiceId}",
                        Name = detail.Service.ServiceName,
                        Price = detail.PriceAtBooking,
                        Type = "Service"
                    });
                }
                else if (detail.ComboId.HasValue)
                {
                    model.UsedServices.Add(new ReviewServiceItem
                    {
                        Id = $"combo_{detail.ComboId}",
                        Name = detail.Combo.ComboName,
                        Price = detail.PriceAtBooking,
                        Type = "Combo"
                    });
                }
            }

            return model;
        }

        public async Task<bool> SubmitReviewAsync(SubmitReviewModel model)
        {
            try
            {
                // Check if already reviewed (if only allowing 1 review/appointment)
                var exists = await _context.Reviews.AnyAsync(r => r.AppointmentId == model.AppointmentId && !r.IsDeleted);
                if (exists) return false;

                // Handle anonymous content
                string finalComment = model.Comment;
                if (model.IsAnonymous)
                {
                    finalComment += " (Anonymous Review)";
                }

                var review = new Review
                {
                    AppointmentId = model.AppointmentId,
                    Rating = model.Rating,
                    Comment = finalComment,
                    CreatedDate = DateTime.Now,
                    IsDeleted = false
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}