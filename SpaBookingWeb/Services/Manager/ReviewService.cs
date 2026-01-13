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
            // Lấy tất cả review kèm thông tin liên quan
            var reviews = await _context.Reviews
                .Include(r => r.Appointment)
                .ThenInclude(a => a.Customer)
                .Include(r => r.Appointment)
                .ThenInclude(a => a.Employee)
                .ThenInclude(e => e.ApplicationUser) // Để lấy tên nhân viên nếu cần từ bảng User
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            // Lấy thêm thông tin dịch vụ (vì quan hệ N-N qua AppointmentDetails nên hơi phức tạp chút)
            // Để tối ưu, ta có thể load AppointmentDetails riêng hoặc dùng Select đè lên
            // Ở đây tôi chọn cách mapping thủ công để xử lý chuỗi tên dịch vụ

            var reviewDtos = new List<ReviewListViewModel>();

            foreach (var r in reviews)
            {
                // Lấy tên các dịch vụ trong cuộc hẹn này
                var serviceNames = await _context.AppointmentDetails
                    .Where(ad => ad.AppointmentId == r.AppointmentId)
                    .Include(ad => ad.Service)
                    .Select(ad => ad.Service != null ? ad.Service.ServiceName : "Dịch vụ khác")
                    .ToListAsync();

                string servicesDisplay = serviceNames.Any() ? string.Join(", ", serviceNames) : "Không xác định";

                reviewDtos.Add(new ReviewListViewModel
                {
                    ReviewId = r.ReviewId,
                    AppointmentId = r.AppointmentId,
                    CustomerName = r.Appointment?.Customer?.FullName ?? "Khách vãng lai",
                    EmployeeName = r.Appointment?.Employee?.FullName ?? "Không chỉ định",
                    ServiceName = servicesDisplay,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedDate = r.CreatedDate
                });
            }

            // Tính toán thống kê
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
                _context.Reviews.Remove(review); // Xóa cứng vì bảng Review thường không cần Soft Delete hoặc chưa có cột IsDeleted
                await _context.SaveChangesAsync();
            }
        }
    }
}