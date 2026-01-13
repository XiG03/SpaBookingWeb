using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public interface IBookingService
    {
        // Manage Session
        BookingSessionModel GetSession();
        void SaveSession(BookingSessionModel session);
        void ClearSession();

        // Get display data
        Task<BookingPageViewModel> GetBookingPageDataAsync();

        // Business logic
        Task<List<string>> GetAvailableTimeSlotsAsync(DateTime date, BookingSessionModel session);
        Task<int> SaveBookingAsync(BookingSessionModel session); // Trả về AppointmentId

        // [NEW] Update deposit payment status
        Task UpdateDepositStatusAsync(int appointmentId, string transactionId);
        // [NEW] Get Appointment info to display Success page
        Task<AppointmentSuccessViewModel> GetAppointmentSuccessInfoAsync(int appointmentId);

        Task<List<AppointmentHistoryViewModel>> GetBookingHistoryAsync(string userEmail);

        Task<AppointmentHistoryViewModel> GetAppointmentDetailAsync(int appointmentId);

        // [NEW] Get history (Completed/Cancelled)
        Task<List<AppointmentHistoryViewModel>> GetBookingHistoryArchiveAsync(string userEmail);

        // [NEW] Reconstruct session from old order (Rebook)
        Task<bool> RebookAsync(int appointmentId);

        Task<VoucherCheckResult> ValidateVoucherAsync(string code, decimal orderTotal);

        Task<bool> ResumeBookingAsync(int appointmentId);


    }

    public class VoucherCheckResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public Voucher Voucher { get; set; } // Return voucher info to display if needed
    }
}