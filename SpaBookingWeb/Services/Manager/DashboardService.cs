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
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(DateTime date)
        {
            var viewModel = new DashboardViewModel();
            var targetDate = date.Date;

            // 1. Appointment Statistics
            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Where(a => a.StartTime.Date == targetDate && !a.IsDeleted)
                .ToListAsync();

            viewModel.TotalAppointmentsToday = appointments.Count;
            viewModel.PendingAppointments = appointments.Count(a => a.Status == "Pending" || a.Status == "Confirmed");
            viewModel.CompletedAppointments = appointments.Count(a => a.Status == "Completed");
            viewModel.CancelledAppointments = appointments.Count(a => a.Status == "Cancelled");

            // Calculate estimated revenue (based on non-cancelled orders)
            viewModel.EstimatedRevenueToday = appointments
                .Where(a => a.Status != "Cancelled")
                .Sum(a => a.AppointmentDetails.Sum(ad => ad.PriceAtBooking));

            // Map to detailed ViewModel
            viewModel.TodayAppointments = appointments.Select(a => new AppointmentItemViewModel
            {
                AppointmentId = a.AppointmentId,
                CustomerName = a.Customer?.FullName ?? "Walk-in Customer",
                EmployeeName = a.Employee?.FullName ?? "Unassigned",
                StartTime = a.StartTime,
                EndTime = a.EndTime ?? a.StartTime.AddMinutes(60), // Fallback nếu null
                ServiceNames = string.Join(", ", a.AppointmentDetails.Select(ad => ad.Service?.ServiceName ?? "Other Service")),
                Status = a.Status,
                StatusColor = GetStatusColor(a.Status)
            }).OrderBy(a => a.StartTime).ToList();

            // 2. Attendance Statistics
            var shifts = await _context.Shifts.ToListAsync();
            var workSchedules = await _context.WorkSchedules
                .Include(ws => ws.Employee)
                .Where(ws => ws.WorkDate.Date == targetDate && !ws.IsDeleted)
                .ToListAsync();

            foreach (var shift in shifts)
            {
                var shiftVm = new ShiftAttendanceViewModel
                {
                    ShiftName = shift.ShiftName,
                    TimeRange = $"{shift.StartTime:hh\\:mm} - {shift.EndTime:hh\\:mm}",
                    Employees = workSchedules
                        .Where(ws => ws.ShiftId == shift.ShiftId)
                        .Select(ws => new EmployeeAttendanceStatus
                        {
                            FullName = ws.Employee.FullName,
                            Avatar = ws.Employee.Avatar ?? "/img/default-avatar.png",
                            IsPresent = ws.IsCheckIn,
                            CheckInTime = ws.CheckInTime
                        }).ToList()
                };
                viewModel.ShiftAttendances.Add(shiftVm);
            }

            return viewModel;
        }

        public async Task<List<CalendarEventViewModel>> GetCalendarEventsAsync(DateTime start, DateTime end)
        {
            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Where(a => a.StartTime >= start && a.StartTime <= end && !a.IsDeleted && a.Status != "Cancelled")
                .ToListAsync();

            return appointments.Select(a => new CalendarEventViewModel
            {
                id = a.AppointmentId,
                title = $"{a.Customer?.FullName} - {string.Join(", ", a.AppointmentDetails.Select(ad => ad.Service?.ServiceName))}",
                start = a.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = (a.EndTime ?? a.StartTime.AddHours(1)).ToString("yyyy-MM-ddTHH:mm:ss"),
                className = GetEventClass(a.Status),
                description = $"Customer: {a.Customer?.PhoneNumber}<br/>Note: {a.Notes ?? "None"}",
                url = $"/Manager/Appointments/Details/{a.AppointmentId}" // Detail link
            }).ToList();
        }

        // NEW: Implement method to get detail
        public async Task<AppointmentDetailViewModel> GetAppointmentDetailAsync(int id)
        {
            var app = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice) // Include Invoice to check PaymentStatus
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (app == null) return null;

            return new AppointmentDetailViewModel
            {
                AppointmentId = app.AppointmentId,
                CustomerName = app.Customer?.FullName ?? "Walk-in Customer",
                CustomerPhone = app.Customer?.PhoneNumber ?? "N/A",
                StartTime = app.StartTime,
                EndTime = app.EndTime ?? app.StartTime.AddHours(1),
                Status = app.Status,
                Note = app.Notes,
                TotalAmount = app.AppointmentDetails.Sum(ad => ad.PriceAtBooking),
                IsDepositPaid = app.IsDepositPaid,
                DepositAmount = app.DepositAmount,
                // Check linked Invoice (if any)
                IsPaidFull = app.Invoice != null && app.Invoice.PaymentStatus == "Paid",
                Services = app.AppointmentDetails.Select(ad => new ServiceDetailDto
                {
                    ServiceName = ad.Service?.ServiceName ?? "Other Service",
                    TechnicianName = ad.Technician?.FullName ?? "Unassigned",
                    Price = ad.PriceAtBooking
                }).ToList()
            };
        }

        // Helper methods
        private string GetStatusColor(string status)
        {
            return status switch
            {
                "Pending" => "warning",
                "Confirmed" => "primary",
                "Completed" => "success",
                "Cancelled" => "danger",
                _ => "secondary"
            };
        }

        private string GetEventClass(string status)
        {
            return status switch
            {
                "Pending" => "bg-warning",
                "Confirmed" => "bg-primary text-white",
                "Completed" => "bg-success text-white",
                _ => "bg-info"
            };
        }
    }
}