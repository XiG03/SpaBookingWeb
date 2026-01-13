using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager; // Assuming it contains necessary ViewModels
using System;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    // [Authorize(Roles = "Manager,Admin")] // Ensure only managers can access
    public class EmployeeController : Controller
    {
        private readonly IEmployeeService _employeeService;

        public EmployeeController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        // ==================================================================================
        // 1. EMPLOYEE MANAGEMENT (CRUD)
        // ==================================================================================

        // GET: Employee List
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _employeeService.GetAllEmployeesAsync();
            return View(model);
        }

        // GET: Create Employee
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new EmployeeViewModel();
            // Load data for Dropdown and Checkbox
            model.Roles = await _employeeService.GetRolesSelectListAsync();
            model.Services = await _employeeService.GetServicesSelectListAsync();
            return View(model);
        }

        // POST: Create Employee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _employeeService.CreateEmployeeAsync(model);
                    TempData["Success"] = "Employee created successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            // If error, reload data
            model.Roles = await _employeeService.GetRolesSelectListAsync();
            model.Services = await _employeeService.GetServicesSelectListAsync();
            return View(model);
        }

        // GET: Edit Employee
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _employeeService.GetEmployeeByIdAsync(id);
            if (employee == null) return NotFound();

            // Load data
            employee.Roles = await _employeeService.GetRolesSelectListAsync();
            employee.Services = await _employeeService.GetServicesSelectListAsync();

            return View(employee);
        }

        // POST: Edit Employee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _employeeService.UpdateEmployeeAsync(model);
                    TempData["Success"] = "Employee updated successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Update error: " + ex.Message);
                }
            }
            
            // Reload data if error
            model.Roles = await _employeeService.GetRolesSelectListAsync();
            model.Services = await _employeeService.GetServicesSelectListAsync();
            return View(model);
        }

        // POST: Delete Employee
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _employeeService.DeleteEmployeeAsync(id);
                TempData["Success"] = "Employee deleted.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Cannot delete: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // ==================================================================================
        // 2. SCHEDULING & ATTENDANCE
        // ==================================================================================

        // GET: View Schedule & Attendance Status by Date
        [HttpGet]
        public async Task<IActionResult> Schedule(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            ViewBag.CurrentDate = selectedDate;

            // This ViewModel needs to contain work schedule list (WorkSchedule) 
            // and attendance status (IsPresent, CheckInTime...)
            var model = await _employeeService.GetDailyScheduleAsync(selectedDate);
            
            // Load Shift list and Employees for Modal to add schedule
             var shifts = await _employeeService.GetAllShiftsAsync() ?? new List<ShiftViewModel>();
            var employees = await _employeeService.GetAllEmployeesAsync() ?? new List<EmployeeListViewModel>();

            // Format Shift Display Text with Time Range (e.g. "Ca Sáng (08:00 - 12:00)")
            var shiftList = shifts.Select(s => new {
                ShiftId = s.ShiftId,
                DisplayText = $"{s.ShiftName} ({s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm})"
            });

            ViewBag.Shifts = new SelectList(shiftList, "ShiftId", "DisplayText");
            ViewBag.Employees = new SelectList(employees, "EmployeeId", "FullName");

            return View(model);
        }

        // GET: API returns events for FullCalendar
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
        {
            var schedules = await _employeeService.GetWorkSchedulesInRangeAsync(start, end);

            var now = DateTime.Now;

            var events = schedules.Select(s => {
                string color;
                
                // Priority color logic
                if (s.IsOnBreak)
                {
                    color = "#ffc107"; // Yellow: On Break
                }
                else if (s.IsCheckIn) 
                {
                    color = "#28a745"; // Green: Present
                }
                else if (!string.IsNullOrEmpty(s.Note))
                {
                    color = "#ffc107"; // Yellow: Absent (Excused)
                }
                else 
                {
                     color = "#007bff"; // Blue: Not checked in (Past or Future)
                }

                return new
                {
                    id = s.ScheduleId,
                    title = $"{s.Employee?.FullName ?? "Unknown"} ({s.Shift?.ShiftName})",
                    start = s.WorkDate.ToString("yyyy-MM-dd") + "T" + s.Shift?.StartTime.ToString(@"hh\:mm\:ss"),
                    end = s.WorkDate.ToString("yyyy-MM-dd") + "T" + s.Shift?.EndTime.ToString(@"hh\:mm\:ss"),
                    allDay = false,
                    color = color,
                    extendedProps = new { 
                    employeeId = s.EmployeeId,
                    shiftId = s.ShiftId,
                    isPresent = s.IsCheckIn,
                    note = s.Note,
                    isOnBreak = s.IsOnBreak,
                    breakStartTime = s.BreakStartTime
                }
            };
        });

            return Json(events);
        }

        // POST: Schedule (Add shift for employee)
        // ... (AssignShift giữ nguyên)

        // ... (Các action khác giữ nguyên)

        // POST: Check Attendance
        // This action is for Manager to confirm employee attendance/lateness
        [HttpPost]
        public async Task<IActionResult> UpdateAttendance(int scheduleId, bool isPresent, string note, bool isOnBreak, TimeSpan? breakStartTime, DateTime returnDate)
        {
            try 
            {
                // Logic: Update IsPresent column, maybe actual CheckInTime if needed
                await _employeeService.UpdateAttendanceStatusAsync(scheduleId, isPresent, note, isOnBreak, breakStartTime);
                TempData["Success"] = "Attendance updated successfully.";
            }
            catch(Exception ex)
            {
                TempData["Error"] = "Attendance error: " + ex.Message;
            }
            return RedirectToAction(nameof(Schedule), new { date = returnDate });
        }

        // ==================================================================================
        // 3. TIP MANAGEMENT
        // ==================================================================================

        // GET: Daily Tip List
        [HttpGet]
        public async Task<IActionResult> DailyTips(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            ViewBag.CurrentDate = selectedDate;

            // Get list of Tips from Invoice/Appointment
            // Model should contain: EmployeeName, CustomerName, Amount, IsDistributed
            var model = await _employeeService.GetDailyTipsAsync(selectedDate);
            
            // Calculate total
            ViewBag.TotalTips = await _employeeService.GetTotalTipsAmountAsync(selectedDate);

            return View(model);
        }

        // POST: Confirm Tip Distributed to Employee
        // Used when Manager distributes cash/transfer to Technician at end of day
        [HttpPost]
        public async Task<IActionResult> ConfirmTipDistribution(int tipId, DateTime returnDate) // tipId can be AppointmentDetail ID or separate Tip table
        {
            try
            {
                await _employeeService.ConfirmTipSentToEmployeeAsync(tipId);
                TempData["Success"] = "Tip distribution confirmed.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(DailyTips), new { date = returnDate });
        }

        // POST: Confirm ALL Tips for day (Quick Utility)
        [HttpPost]
        public async Task<IActionResult> ConfirmAllTips(DateTime date)
        {
            try
            {
                await _employeeService.ConfirmAllTipsForDateAsync(date);
                TempData["Success"] = $"Confirmed all tips for {date:dd/MM}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(DailyTips), new { date = date });
        }

        // ==================================================================================
        // 4. PAYROLL
        // ==================================================================================
        
        [HttpGet]
        public async Task<IActionResult> Payroll(int? month, int? year, DateTime? fromDate, DateTime? toDate)
        {
            // If month/year not selected, auto use end date of payroll period or current
            var m = month ?? toDate?.Month ?? DateTime.Now.Month;
            var y = year ?? toDate?.Year ?? DateTime.Now.Year;

            ViewBag.Month = m;
            ViewBag.Year = y;

            // Pass date filter parameters to Service
            var payrolls = await _employeeService.GeneratePayrollAsync(m, y, fromDate, toDate);

            // Check latest payroll period
            var latestPeriod = await _employeeService.GetLatestPayrollPeriodAsync();
            if (latestPeriod.ToDate.HasValue)
            {
                ViewBag.LatestPayrollFrom = latestPeriod.FromDate;
                ViewBag.LatestPayrollTo = latestPeriod.ToDate;
            }
            
            // Get actual date range from result (to display on UI)
            if (payrolls.Any())
            {
                ViewBag.FromDate = payrolls.First().FromDate;
                ViewBag.ToDate = payrolls.First().ToDate;
            }
            else
            {
                // Fallback if no payroll
                ViewBag.FromDate = fromDate ?? new DateTime(y, 1, 1);
                ViewBag.ToDate = toDate ?? new DateTime(y, m, 1).AddMonths(1).AddDays(-1);
            }

            return View(payrolls);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmSalary(int employeeId, int month, int year, decimal finalAmount, DateTime fromDate, DateTime toDate, decimal bonus = 0, decimal deduction = 0)
        {
            // Logic to save payroll to DB with Status = 'ManagerConfirmed'
            await _employeeService.ConfirmPayrollAsync(employeeId, month, year, finalAmount, fromDate, toDate, bonus, deduction);
            
            TempData["Success"] = "Payroll confirmed for employee.";
            return RedirectToAction(nameof(Payroll), new { month, year, fromDate, toDate });
        }
    }
}