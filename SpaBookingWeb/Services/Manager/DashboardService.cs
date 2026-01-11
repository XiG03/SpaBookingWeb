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

            // 1. Thống kê Appointment
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

            // Tính doanh thu ước tính (dựa trên các đơn chưa hủy)
            viewModel.EstimatedRevenueToday = appointments
                .Where(a => a.Status != "Cancelled")
                .Sum(a => a.AppointmentDetails.Sum(ad => ad.PriceAtBooking));

            // Map sang ViewModel chi tiết
            viewModel.TodayAppointments = appointments.Select(a => new AppointmentItemViewModel
            {
                AppointmentId = a.AppointmentId,
                CustomerName = a.Customer?.FullName ?? "Khách lẻ",
                EmployeeName = a.Employee?.FullName ?? "Chưa phân công",
                StartTime = a.StartTime,
                EndTime = a.EndTime ?? a.StartTime.AddMinutes(60), // Fallback nếu null
                ServiceNames = string.Join(", ", a.AppointmentDetails.Select(ad => ad.Service?.ServiceName ?? "Dịch vụ khác")),
                Status = a.Status,
                StatusColor = GetStatusColor(a.Status)
            }).OrderBy(a => a.StartTime).ToList();

            // 2. Thống kê Điểm danh (Attendance)
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
                description = $"Khách hàng: {a.Customer?.PhoneNumber}<br/>Ghi chú: {a.Notes ?? "Không"}",
                url = $"/Manager/Appointments/Details/{a.AppointmentId}" // Link xem chi tiết
            }).ToList();
        }

        // MỚI: Triển khai hàm lấy chi tiết
        public async Task<AppointmentDetailViewModel> GetAppointmentDetailAsync(int id)
        {
            var app = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice) // Include Invoice để check PaymentStatus
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (app == null) return null;

            return new AppointmentDetailViewModel
            {
                AppointmentId = app.AppointmentId,
                CustomerName = app.Customer?.FullName ?? "Khách lẻ",
                CustomerPhone = app.Customer?.PhoneNumber ?? "N/A",
                StartTime = app.StartTime,
                EndTime = app.EndTime ?? app.StartTime.AddHours(1),
                Status = app.Status,
                Note = app.Notes,
                TotalAmount = app.AppointmentDetails.Sum(ad => ad.PriceAtBooking),
                IsDepositPaid = app.IsDepositPaid,
                DepositAmount = app.DepositAmount,
                // Kiểm tra Invoice liên kết (nếu có)
                IsPaidFull = app.Invoice != null && app.Invoice.PaymentStatus == "Paid",
                Services = app.AppointmentDetails.Select(ad => new ServiceDetailDto
                {
                    ServiceName = ad.Service?.ServiceName ?? "Dịch vụ khác",
                    TechnicianName = ad.Technician?.FullName ?? "Chưa chỉ định",
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