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
            // 1. Lấy Appointment và kiểm tra quyền
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Combo)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null) return null;

            // Validate email (đảm bảo đúng người review)
            if (appointment.Customer.Email != userEmail) return null;

            // Validate trạng thái (chỉ review khi đã hoàn thành)
            // if (appointment.Status != "Completed") return null; // Uncomment khi chạy thật

            var model = new ReviewPageViewModel
            {
                AppointmentId = appointment.AppointmentId,
                SpaName = "Lotus Spa & Salon" // Hoặc lấy từ SystemSettings
            };

            // 2. Map danh sách dịch vụ đã dùng
            foreach (var detail in appointment.AppointmentDetails)
            {
                if (detail.ServiceId.HasValue)
                {
                    model.UsedServices.Add(new ReviewServiceItem
                    {
                        Id = $"service_{detail.ServiceId}",
                        Name = detail.Service.ServiceName,
                        Price = detail.PriceAtBooking,
                        Type = "Dịch vụ"
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
                // Kiểm tra xem đã review chưa (nếu chỉ cho phép 1 review/appointment)
                var exists = await _context.Reviews.AnyAsync(r => r.AppointmentId == model.AppointmentId && !r.IsDeleted);
                if (exists) return false;

                // Xử lý nội dung ẩn danh
                string finalComment = model.Comment;
                if (model.IsAnonymous)
                {
                    finalComment += " (Đánh giá ẩn danh)";
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