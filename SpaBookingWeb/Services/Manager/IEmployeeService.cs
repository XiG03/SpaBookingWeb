using Microsoft.AspNetCore.Mvc.Rendering;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpaBookingWeb.Models;


namespace SpaBookingWeb.Services.Manager
{
    public interface IEmployeeService
    {
        // 1. Employee Management (CRUD)
        Task<List<EmployeeListViewModel>> GetAllEmployeesAsync();
        Task<EmployeeViewModel?> GetEmployeeByIdAsync(int id);
        Task CreateEmployeeAsync(EmployeeViewModel model);
        Task UpdateEmployeeAsync(EmployeeViewModel model);
        Task DeleteEmployeeAsync(int id);

        // NEW: Helper to get data for Dropdown/Checkbox
        Task<List<SelectListItem>> GetRolesSelectListAsync();
        Task<List<SelectListItem>> GetServicesSelectListAsync();

        // 2. Schedule & Attendance
        Task<List<ShiftViewModel>> GetAllShiftsAsync(); // Get list of shifts to fill dropdown
        Task<DailyScheduleViewModel> GetDailyScheduleAsync(DateTime date);
        Task<List<WorkSchedule>> GetWorkSchedulesInRangeAsync(DateTime fromDate, DateTime toDate);
        Task CreateShiftAsync(string shiftName, TimeSpan startTime, TimeSpan endTime);
        Task DeleteShiftAsync(int shiftId);

        Task AddWorkScheduleAsync(int employeeId, int shiftId, DateTime date);
        Task DeleteWorkScheduleAsync(int scheduleId);
        Task UpdateAttendanceStatusAsync(int scheduleId, bool isPresent, string note, bool isOnBreak, TimeSpan? breakStartTime);

        // 3. Tip Management
        Task<List<DailyTipViewModel>> GetDailyTipsAsync(DateTime date);
        Task<decimal> GetTotalTipsAmountAsync(DateTime date);
        Task ConfirmTipSentToEmployeeAsync(int tipId);
        Task ConfirmAllTipsForDateAsync(DateTime date);

        // 4. Payroll
        Task<List<SalaryPayrollViewModel>> GeneratePayrollAsync(int month, int year, DateTime? fromDate = null, DateTime? toDate = null);
        Task ConfirmPayrollAsync(int employeeId, int month, int year, decimal finalAmount, DateTime fromDate, DateTime toDate, decimal bonus, decimal deduction);

        // NEW: Function for employee to self-confirm receipt of salary
        Task ConfirmSalaryByEmployeeAsync(int salaryId);

        // Get latest payroll period info
        Task<(DateTime? FromDate, DateTime? ToDate)> GetLatestPayrollPeriodAsync();
    }
}