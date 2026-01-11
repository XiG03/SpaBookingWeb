using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager; // Giả định chứa các ViewModel cần thiết
using System;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    // [Authorize(Roles = "Manager,Admin")] // Đảm bảo chỉ quản lý mới vào được
    public class EmployeeController : Controller
    {
        private readonly IEmployeeService _employeeService;

        public EmployeeController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        // ==================================================================================
        // 1. QUẢN LÝ NHÂN VIÊN (CRUD)
        // ==================================================================================

        // GET: Danh sách nhân viên
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _employeeService.GetAllEmployeesAsync();
            return View(model);
        }

        // GET: Tạo nhân viên
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new EmployeeViewModel();
            // Nạp dữ liệu cho Dropdown và Checkbox
            model.Roles = await _employeeService.GetRolesSelectListAsync();
            model.Services = await _employeeService.GetServicesSelectListAsync();
            return View(model);
        }

        // POST: Tạo nhân viên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _employeeService.CreateEmployeeAsync(model);
                    TempData["Success"] = "Tạo nhân viên thành công";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            // Nếu lỗi, nạp lại dữ liệu
            model.Roles = await _employeeService.GetRolesSelectListAsync();
            model.Services = await _employeeService.GetServicesSelectListAsync();
            return View(model);
        }

        // GET: Sửa nhân viên
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _employeeService.GetEmployeeByIdAsync(id);
            if (employee == null) return NotFound();

            // Nạp dữ liệu
            employee.Roles = await _employeeService.GetRolesSelectListAsync();
            employee.Services = await _employeeService.GetServicesSelectListAsync();

            return View(employee);
        }

        // POST: Sửa nhân viên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _employeeService.UpdateEmployeeAsync(model);
                    TempData["Success"] = "Cập nhật thông tin nhân viên thành công";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi cập nhật: " + ex.Message);
                }
            }
            
            // Nạp lại dữ liệu nếu lỗi
            model.Roles = await _employeeService.GetRolesSelectListAsync();
            model.Services = await _employeeService.GetServicesSelectListAsync();
            return View(model);
        }

        // POST: Xóa nhân viên
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _employeeService.DeleteEmployeeAsync(id);
                TempData["Success"] = "Đã xóa nhân viên.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể xóa: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // ==================================================================================
        // 2. LỊCH LÀM VIỆC & ĐIỂM DANH (SCHEDULING & ATTENDANCE)
        // ==================================================================================

        // GET: Xem lịch & Trạng thái điểm danh theo ngày
        [HttpGet]
        public async Task<IActionResult> Schedule(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            ViewBag.CurrentDate = selectedDate;

            // ViewModel này cần chứa danh sách lịch làm việc (WorkSchedule) 
            // và trạng thái điểm danh (IsPresent, CheckInTime...)
            var model = await _employeeService.GetDailyScheduleAsync(selectedDate);
            
            // Load danh sách ca làm việc (Shift) và Nhân viên để dùng cho Modal thêm lịch
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

        // GET: API trả về events cho FullCalendar
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
        {
            var schedules = await _employeeService.GetWorkSchedulesInRangeAsync(start, end);

            var now = DateTime.Now;

            var events = schedules.Select(s => {
                string color;
                
                // Logic màu sắc ưu tiên
                if (s.IsOnBreak)
                {
                    color = "#ffc107"; // Vàng: Nghỉ giữa ca
                }
                else if (s.IsCheckIn) 
                {
                    color = "#28a745"; // Xanh lá: Đã đi làm (Present)
                }
                else if (!string.IsNullOrEmpty(s.Note))
                {
                    color = "#ffc107"; // Vàng: Chưa đi làm nhưng có phép/lý do (Excused)
                }
                else 
                {
                     color = "#007bff"; // Xanh dương: Chưa điểm danh (Bất kể quá khứ hay tương lai)
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

        // POST: Xếp lịch (Thêm ca làm cho nhân viên)
        // ... (AssignShift giữ nguyên)

        // ... (Các action khác giữ nguyên)

        // POST: Điểm danh (Check Attendance)
        // Hành động này dùng để Manager xác nhận nhân viên có đi làm hay không/hoặc đến trễ
        [HttpPost]
        public async Task<IActionResult> UpdateAttendance(int scheduleId, bool isPresent, string note, bool isOnBreak, TimeSpan? breakStartTime, DateTime returnDate)
        {
            try 
            {
                // Logic: Cập nhật cột IsPresent, có thể là cả CheckInTime thực tế nếu cần
                await _employeeService.UpdateAttendanceStatusAsync(scheduleId, isPresent, note, isOnBreak, breakStartTime);
                TempData["Success"] = "Cập nhật điểm danh thành công.";
            }
            catch(Exception ex)
            {
                TempData["Error"] = "Lỗi điểm danh: " + ex.Message;
            }
            return RedirectToAction(nameof(Schedule), new { date = returnDate });
        }

        // ==================================================================================
        // 3. QUẢN LÝ TIỀN TIP (TIP MANAGEMENT)
        // ==================================================================================

        // GET: Danh sách tiền Tip trong ngày
        [HttpGet]
        public async Task<IActionResult> DailyTips(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            ViewBag.CurrentDate = selectedDate;

            // Lấy danh sách các khoản Tip từ Invoice/Appointment
            // Model trả về nên có: EmployeeName, CustomerName, Amount, IsDistributed (Đã đưa tiền cho NV chưa)
            var model = await _employeeService.GetDailyTipsAsync(selectedDate);
            
            // Tính tổng
            ViewBag.TotalTips = await _employeeService.GetTotalTipsAmountAsync(selectedDate);

            return View(model);
        }

        // POST: Xác nhận đã trả tiền Tip cho nhân viên
        // Dùng khi cuối ngày Manager lấy tiền mặt hoặc chuyển khoản tip cho KTV
        [HttpPost]
        public async Task<IActionResult> ConfirmTipDistribution(int tipId, DateTime returnDate) // tipId có thể là ID của AppointmentDetail hoặc bảng Tip riêng
        {
            try
            {
                await _employeeService.ConfirmTipSentToEmployeeAsync(tipId);
                TempData["Success"] = "Đã xác nhận trả Tip.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(DailyTips), new { date = returnDate });
        }

        // POST: Xác nhận trả TOÀN BỘ Tip trong ngày (Tiện ích nhanh)
        [HttpPost]
        public async Task<IActionResult> ConfirmAllTips(DateTime date)
        {
            try
            {
                await _employeeService.ConfirmAllTipsForDateAsync(date);
                TempData["Success"] = $"Đã xác nhận trả hết Tip ngày {date:dd/MM}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(DailyTips), new { date = date });
        }

        // ==================================================================================
        // 4. TÍNH LƯƠNG (PAYROLL)
        // ==================================================================================
        
        [HttpGet]
        public async Task<IActionResult> Payroll(int? month, int? year, DateTime? fromDate, DateTime? toDate)
        {
            // Nếu không chọn tháng/năm, tự động lấy theo ngày kết thúc của kỳ lương (hoặc hiện tại)
            var m = month ?? toDate?.Month ?? DateTime.Now.Month;
            var y = year ?? toDate?.Year ?? DateTime.Now.Year;

            ViewBag.Month = m;
            ViewBag.Year = y;

            // Truyền tham số lọc ngày vào Service
            var payrolls = await _employeeService.GeneratePayrollAsync(m, y, fromDate, toDate);

            // Kiểm tra kỳ lương gần nhất
            var latestPeriod = await _employeeService.GetLatestPayrollPeriodAsync();
            if (latestPeriod.ToDate.HasValue)
            {
                ViewBag.LatestPayrollFrom = latestPeriod.FromDate;
                ViewBag.LatestPayrollTo = latestPeriod.ToDate;
            }
            
            // Lấy khoảng ngày thực tế từ kết quả trả về (để hiển thị trên UI)
            if (payrolls.Any())
            {
                ViewBag.FromDate = payrolls.First().FromDate;
                ViewBag.ToDate = payrolls.First().ToDate;
            }
            else
            {
                // Fallback nếu chưa có lương
                ViewBag.FromDate = fromDate ?? new DateTime(y, 1, 1);
                ViewBag.ToDate = toDate ?? new DateTime(y, m, 1).AddMonths(1).AddDays(-1);
            }

            return View(payrolls);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmSalary(int employeeId, int month, int year, decimal finalAmount, DateTime fromDate, DateTime toDate, decimal bonus = 0, decimal deduction = 0)
        {
            // Logic lưu lương vào DB với Status = "ManagerConfirmed"
            await _employeeService.ConfirmPayrollAsync(employeeId, month, year, finalAmount, fromDate, toDate, bonus, deduction);
            
            TempData["Success"] = "Đã xác nhận bảng lương cho nhân viên.";
            return RedirectToAction(nameof(Payroll), new { month, year, fromDate, toDate });
        }
    }
}