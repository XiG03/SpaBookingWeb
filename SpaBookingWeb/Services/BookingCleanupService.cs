using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SpaBookingWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace SpaBookingWeb.Services
{
    public class BookingCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingCleanupService> _logger;

        public BookingCleanupService(IServiceProvider serviceProvider, ILogger<BookingCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Booking Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CancelExpiredBookingsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cancelling expired bookings.");
                }

                // Chạy mỗi 1 giờ một lần
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            _logger.LogInformation("Booking Cleanup Service is stopping.");
        }

        private async Task CancelExpiredBookingsAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Ngưỡng thời gian: Quá 2 ngày
                var thresholdDate = DateTime.Now.AddDays(-2);

                // Tìm các lịch hẹn đang Pending, chưa thanh toán cọc và quá hạn
                var expiredBookings = await context.Appointments
                    .Where(a => a.Status == "Pending" 
                             && !a.IsDepositPaid 
                             && a.CreatedDate < thresholdDate)
                    .ToListAsync();

                if (expiredBookings.Any())
                {
                    _logger.LogInformation($"Tìm thấy {expiredBookings.Count} lịch hẹn quá hạn. Đang hủy...");

                    foreach (var booking in expiredBookings)
                    {
                        booking.Status = "Cancelled";
                        // Ghi chú thêm lý do hủy
                        booking.Notes = string.IsNullOrEmpty(booking.Notes) 
                            ? "Hủy tự động do quá hạn đặt cọc." 
                            : booking.Notes + " | Hủy tự động do quá hạn đặt cọc.";
                    }

                    await context.SaveChangesAsync();
                    _logger.LogInformation("Đã hủy các lịch hẹn quá hạn thành công.");
                }
            }
        }
    }
}
