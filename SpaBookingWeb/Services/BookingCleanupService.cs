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

                // Run every 1 hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            _logger.LogInformation("Booking Cleanup Service is stopping.");
        }

        private async Task CancelExpiredBookingsAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Time threshold: Over 2 days
                var thresholdDate = DateTime.Now.AddDays(-2);

                // Find Pending appointments, unpaid deposit and expired
                var expiredBookings = await context.Appointments
                    .Where(a => a.Status == "Pending" 
                             && !a.IsDepositPaid 
                             && a.CreatedDate < thresholdDate)
                    .ToListAsync();

                if (expiredBookings.Any())
                {
                    _logger.LogInformation($"Found {expiredBookings.Count} expired bookings. Cancelling...");

                    foreach (var booking in expiredBookings)
                    {
                        booking.Status = "Cancelled";
                        // Add cancellation reason note
                        booking.Notes = string.IsNullOrEmpty(booking.Notes) 
                            ? "Automatically cancelled due to overdue deposit." 
                            : booking.Notes + " | Automatically cancelled due to overdue deposit.";
                    }

                    await context.SaveChangesAsync();
                    _logger.LogInformation("Expired bookings cancelled successfully.");
                }
            }
        }
    }
}
