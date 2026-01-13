using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public class EmployeeService : IEmployeeService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeeService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ====================================================================
        // 1. QUẢN LÝ NHÂN VIÊN
        // ====================================================================

        public async Task<List<EmployeeListViewModel>> GetAllEmployeesAsync()
        {
            var employees = await _context.Employees
                .Include(e => e.ApplicationUser)
                .Where(e => !EF.Property<bool>(e, "IsDeleted")) // Lọc xóa mềm
                .ToListAsync();

            var viewModels = new List<EmployeeListViewModel>();

            foreach (var emp in employees)
            {
                // Lấy Role của User
                var roles = emp.ApplicationUser != null 
                    ? await _userManager.GetRolesAsync(emp.ApplicationUser) 
                    : new List<string>();
                // Tính rating trung bình
                var reviews = await _context.Appointments
                    .Where(a => a.EmployeeId == emp.EmployeeId && a.Status == "Completed")
                    .Join(_context.Reviews, a => a.AppointmentId, r => r.AppointmentId, (a, r) => r)
                    .ToListAsync();

                double avgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 5.0;

                viewModels.Add(new EmployeeListViewModel
                {
                    EmployeeId = emp.EmployeeId,
                    FullName = emp.FullName,
                    Email = emp.ApplicationUser?.Email ?? "N/A",
                    PhoneNumber = emp.ApplicationUser?.PhoneNumber ?? "N/A",
                    Position = roles.FirstOrDefault() ?? "N/A", 
                    Avatar = emp.Avatar,
                    IsActive = emp.IsActive,
                    AverageRating = Math.Round(avgRating, 1),
                    TotalAppointments = await _context.Appointments.CountAsync(a => a.EmployeeId == emp.EmployeeId && a.Status == "Completed")
                });
            }

            return viewModels;
        }

        public async Task<EmployeeViewModel?> GetEmployeeByIdAsync(int id)
        {
            var emp = await _context.Employees
                .Include(e => e.ApplicationUser)
                .Include(e => e.TechnicianServices) // Include dịch vụ
                .FirstOrDefaultAsync(e => e.EmployeeId == id);
            
            if (emp == null) return null;

            var model = new EmployeeViewModel
            {
                EmployeeId = emp.EmployeeId,
                FullName = emp.FullName,
                Email = emp.ApplicationUser?.Email ?? "",
                PhoneNumber = emp.ApplicationUser?.PhoneNumber ?? "",
                Address = emp.Address,
                Gender = emp.Gender,
                DateOfBirth = emp.DateOfBirth,
                BaseSalary = emp.BaseSalary,
                HireDate = emp.HireDate,
                IsActive = emp.IsActive,
                // Load Services đã chọn
                SelectedServiceIds = emp.TechnicianServices.Select(ts => ts.ServiceId).ToList()
            };

            // Load Role đã chọn
            if (emp.ApplicationUser != null)
            {
                var userRoles = await _userManager.GetRolesAsync(emp.ApplicationUser);
                var roleName = userRoles.FirstOrDefault();
                if (roleName != null)
                {
                    var role = await _roleManager.FindByNameAsync(roleName);
                    model.SelectedRoleId = role?.Id;
                }
            }

            return model;
        }

        public async Task CreateEmployeeAsync(EmployeeViewModel model)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                Address = model.Address,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, "Spa@123456");
            if (!result.Succeeded)
            {
                throw new Exception("Lỗi tạo tài khoản: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            // 1. Gán Role
            if (!string.IsNullOrEmpty(model.SelectedRoleId))
            {
                var role = await _roleManager.FindByIdAsync(model.SelectedRoleId);
                if (role != null)
                {
                    await _userManager.AddToRoleAsync(user, role.Name);
                }
            }

            var employee = new Employee
            {
                IdentityUserId = user.Id,
                FullName = model.FullName,
                Gender = model.Gender,
                DateOfBirth = model.DateOfBirth,
                Address = model.Address,
                HireDate = model.HireDate,
                BaseSalary = model.BaseSalary,
                Avatar = "/ManagerAssets/assets/avatars/face-1.jpg",
                IsActive = true
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(); // Lưu để có EmployeeId

            // 2. Lưu Dịch vụ (Nếu là Technician và có chọn dịch vụ)
            // Giả sử role Technician có tên là "Technician" hoặc "Kỹ thuật viên"
            // Hoặc đơn giản là cứ có chọn dịch vụ thì lưu
            if (model.SelectedServiceIds != null && model.SelectedServiceIds.Any())
            {
                foreach (var serviceId in model.SelectedServiceIds)
                {
                    _context.TechnicianServices.Add(new TechnicianService
                    {
                        EmployeeId = employee.EmployeeId,
                        ServiceId = serviceId
                    });
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateEmployeeAsync(EmployeeViewModel model)
        {
            var emp = await _context.Employees
                .Include(e => e.ApplicationUser)
                .Include(e => e.TechnicianServices)
                .FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId);

            if (emp == null) throw new Exception("Không tìm thấy nhân viên");

            emp.FullName = model.FullName;
            emp.BaseSalary = model.BaseSalary;
            emp.Address = model.Address;
            emp.DateOfBirth = model.DateOfBirth;
            emp.Gender = model.Gender;
            emp.IsActive = model.IsActive;

            if (emp.ApplicationUser != null)
            {
                emp.ApplicationUser.Email = model.Email;
                emp.ApplicationUser.UserName = model.Email;
                emp.ApplicationUser.PhoneNumber = model.PhoneNumber;
                await _userManager.UpdateAsync(emp.ApplicationUser);

                // 1. Cập nhật Role
                var currentRoles = await _userManager.GetRolesAsync(emp.ApplicationUser);
                await _userManager.RemoveFromRolesAsync(emp.ApplicationUser, currentRoles);

                if (!string.IsNullOrEmpty(model.SelectedRoleId))
                {
                    var role = await _roleManager.FindByIdAsync(model.SelectedRoleId);
                    if (role != null)
                    {
                        await _userManager.AddToRoleAsync(emp.ApplicationUser, role.Name);
                    }
                }
            }

            // 2. Cập nhật Dịch vụ (Logic Smart Merge để tránh lỗi Tracking và xử lý Soft Delete)
            var currentServiceIds = model.SelectedServiceIds ?? new List<int>();
            
            // Lấy tất cả TechnicianServices (bao gồm cả đã xóa mềm) để xử lý
            var allExistingServices = await _context.TechnicianServices
                .IgnoreQueryFilters()
                .Where(ts => ts.EmployeeId == emp.EmployeeId)
                .ToListAsync();

            foreach (var existing in allExistingServices)
            {
                if (currentServiceIds.Contains(existing.ServiceId))
                {
                    // Nếu đang chọn: Đảm bảo nó Active (Khôi phục nếu cần)
                    // Dùng Entry để set IsDeleted = false vì có thể model không expose trực tiếp hoặc để chắc chắn
                    var entry = _context.Entry(existing);
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "IsDeleted"))
                    {
                        entry.CurrentValues["IsDeleted"] = false;
                    }
                }
                else
                {
                    // Nếu không chọn nữa: Xóa (Soft Delete)
                    _context.TechnicianServices.Remove(existing);
                }
            }

            // Thêm mới các dịch vụ chưa từng tồn tại
            var existingIds = allExistingServices.Select(x => x.ServiceId).ToList();
            var newIds = currentServiceIds.Except(existingIds);

            foreach (var newId in newIds)
            {
                _context.TechnicianServices.Add(new TechnicianService
                {
                    EmployeeId = emp.EmployeeId,
                    ServiceId = newId
                });
            }

            _context.Employees.Update(emp);
            await _context.SaveChangesAsync();
        }

        public async Task<List<SelectListItem>> GetRolesSelectListAsync()
        {
            return await _roleManager.Roles
                .Select(r => new SelectListItem { Value = r.Id, Text = r.Name })
                .ToListAsync();
        }

        public async Task<List<SelectListItem>> GetServicesSelectListAsync()
        {
            return await _context.Services
                .Where(s => !s.IsDeleted && s.IsActive)
                .Select(s => new SelectListItem { Value = s.ServiceId.ToString(), Text = s.ServiceName })
                .ToListAsync();
        }

        public async Task DeleteEmployeeAsync(int id)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp != null)
            {
                // Soft delete logic (đã được cấu hình trong DbContext)
                emp.IsActive = false;
                _context.Employees.Remove(emp); // DbContext sẽ tự chuyển thành Soft Delete
                await _context.SaveChangesAsync();
            }
        }

        // ====================================================================
        // 2. LỊCH LÀM VIỆC & ĐIỂM DANH
        // ====================================================================

        public async Task<List<ShiftViewModel>> GetAllShiftsAsync()
        {
            var shifts = await _context.Shifts
                .Select(s => new ShiftViewModel 
                { 
                    ShiftId = s.ShiftId, 
                    ShiftName = s.ShiftName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                })
                .ToListAsync();
            
            // Đảm bảo không trả về null
            return shifts ?? new List<ShiftViewModel>();
        }

        public async Task CreateShiftAsync(string shiftName, TimeSpan startTime, TimeSpan endTime)
        {
            var shift = new Shift
            {
                ShiftName = shiftName,
                StartTime = startTime,
                EndTime = endTime
            };
            _context.Shifts.Add(shift);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteShiftAsync(int shiftId) 
        {
            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift != null)
            {
                // Kiểm tra xem có lịch làm việc nào đang dùng shift này không?
                var isUsed = await _context.WorkSchedules.AnyAsync(ws => ws.ShiftId == shiftId);
                if (isUsed) throw new Exception("Không thể xóa ca làm việc đang được sử dụng trong lịch làm việc của nhân viên.");

                _context.Shifts.Remove(shift);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<DailyScheduleViewModel> GetDailyScheduleAsync(DateTime date)
        {
            var shifts = await _context.Shifts.ToListAsync();
            
            // Lấy lịch làm việc của ngày đó
            var schedules = await _context.WorkSchedules
                .Include(ws => ws.Employee)
                .Where(ws => ws.WorkDate.Date == date.Date)
                .ToListAsync();

            var viewModel = new DailyScheduleViewModel
            {
                Date = date,
                Shifts = new List<ShiftAssignmentDto>()
            };

            foreach (var shift in shifts)
            {
                var shiftDto = new ShiftAssignmentDto
                {
                    ShiftId = shift.ShiftId,
                    ShiftName = shift.ShiftName,
                    TimeRange = $"{shift.StartTime:hh\\:mm} - {shift.EndTime:hh\\:mm}",
                    
                    // Map sang WorkScheduleViewModel để có thông tin điểm danh
                    Schedules = schedules
                        .Where(s => s.ShiftId == shift.ShiftId)
                        .Select(s => new WorkScheduleViewModel
                        {
                            ScheduleId = s.ScheduleId, // Map với ScheduleId trong Model WorkSchedule
                            EmployeeId = s.EmployeeId,
                            EmployeeName = s.Employee.FullName,
                            Position = "Kỹ thuật viên",
                            IsPresent = s.IsCheckIn, 
                            Note = "" 
                        }).ToList()
                };
                viewModel.Shifts.Add(shiftDto);
            }

            return viewModel;
        }

        public async Task<List<WorkSchedule>> GetWorkSchedulesInRangeAsync(DateTime fromDate, DateTime toDate)
        {
            return await _context.WorkSchedules
                .Include(ws => ws.Employee)
                .Include(ws => ws.Shift)
                .Where(ws => ws.WorkDate >= fromDate && ws.WorkDate <= toDate)
                .ToListAsync();
        }

        public async Task AddWorkScheduleAsync(int employeeId, int shiftId, DateTime date)
        {
            var exists = await _context.WorkSchedules
                .AnyAsync(ws => ws.EmployeeId == employeeId && ws.ShiftId == shiftId && ws.WorkDate.Date == date.Date);

            if (exists) throw new Exception("Nhân viên đã được xếp vào ca này rồi.");

            var schedule = new WorkSchedule
            {
                EmployeeId = employeeId,
                ShiftId = shiftId,
                WorkDate = date,
                IsCheckIn = false // Mặc định chưa điểm danh
            };
            _context.WorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteWorkScheduleAsync(int scheduleId)
        {
            var schedule = await _context.WorkSchedules.FindAsync(scheduleId);
            if (schedule != null)
            {
                _context.WorkSchedules.Remove(schedule);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateAttendanceStatusAsync(int scheduleId, bool isPresent, string note, bool isOnBreak, TimeSpan? breakStartTime)
        {
            var schedule = await _context.WorkSchedules.FindAsync(scheduleId);
            if (schedule == null) throw new Exception("Lịch làm việc không tồn tại.");

            schedule.IsCheckIn = isPresent;
            schedule.Note = note ?? ""; 
            schedule.IsOnBreak = isOnBreak;
            schedule.BreakStartTime = breakStartTime;
            
            _context.WorkSchedules.Update(schedule);
            await _context.SaveChangesAsync();
        }

        // ====================================================================
        // 3. QUẢN LÝ TIỀN TIP
        // ====================================================================

        public async Task<List<DailyTipViewModel>> GetDailyTipsAsync(DateTime date)
        {
            // LƯU Ý: Đoạn này giả định bạn đã thêm TipAmount và IsTipPaid vào Appointment
            // Nếu Model chưa có, bạn cần thêm vào Entity Appointment
            var tips = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                // Giả định TipAmount có trong DB. Nếu chưa có, code sẽ lỗi biên dịch tại đây.
                // .Where(a => a.CreatedDate.Date == date.Date && a.TipAmount > 0) 
                // Tạm thời comment logic TipAmount để code chạy được với Model hiện tại
                .Where(a => a.CreatedDate.Date == date.Date) 
                .Select(a => new DailyTipViewModel
                {
                    TipId = a.AppointmentId,
                    CreatedDate = a.CreatedDate,
                    CustomerName = a.Customer.FullName,
                    EmployeeName = a.Employee != null ? a.Employee.FullName : "N/A",
                    Amount = 0, // Thay bằng a.TipAmount khi đã update DB
                    IsDistributed = false // Thay bằng a.IsTipPaid khi đã update DB
                })
                .ToListAsync();

            return tips;
        }

        public async Task<decimal> GetTotalTipsAmountAsync(DateTime date)
        {
            // Tạm thời trả về 0 vì chưa có cột TipAmount
            return 0;
            // return await _context.Appointments
            //    .Where(a => a.CreatedDate.Date == date.Date)
            //    .SumAsync(a => a.TipAmount ?? 0);
        }

        public async Task ConfirmTipSentToEmployeeAsync(int tipId)
        {
            var appointment = await _context.Appointments.FindAsync(tipId);
            if (appointment == null) throw new Exception("Không tìm thấy đơn hàng.");

            // appointment.IsTipPaid = true; // Cần update DB Appointment
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
        }

        public async Task ConfirmAllTipsForDateAsync(DateTime date)
        {
            // Logic chờ update DB Appointment
            await Task.CompletedTask;
        }

        // ====================================================================
        // 4. TÍNH LƯƠNG (PAYROLL)
        // ====================================================================

        public async Task<List<SalaryPayrollViewModel>> GeneratePayrollAsync(int month, int year, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Determine Date Range
            DateTime start, end;
            
            // 1. Check if ANY salary record exists for this month/year that specifies a range
            var existingSalarySample = await _context.Salaries
                .FirstOrDefaultAsync(s => s.Month == month && s.Year == year);

            if (existingSalarySample != null && existingSalarySample.FromDate != DateTime.MinValue)
            {
                start = existingSalarySample.FromDate;
                end = existingSalarySample.ToDate;
            }
            else
            {
                if (fromDate.HasValue && toDate.HasValue)
                {
                    start = fromDate.Value;
                    end = toDate.Value;
                }
                else
                {
                    start = new DateTime(year, month, 1);
                    end = start.AddMonths(1).AddDays(-1);
                }
            }

            var employees = await _context.Employees.Where(e => e.IsActive).ToListAsync();
            var payrolls = new List<SalaryPayrollViewModel>();

            foreach (var emp in employees)
            {
                var existingSalary = await _context.Salaries
                    .FirstOrDefaultAsync(s => s.EmployeeId == emp.EmployeeId && s.Month == month && s.Year == year);

                if (existingSalary != null)
                {
                    payrolls.Add(new SalaryPayrollViewModel
                    {
                        SalaryId = existingSalary.SalaryId,
                        EmployeeId = existingSalary.EmployeeId,
                        EmployeeName = emp.FullName,
                        Month = existingSalary.Month,
                        Year = existingSalary.Year,
                        TotalWorkHours = existingSalary.TotalWorkHours,
                        BaseSalary = emp.BaseSalary,
                        TotalCommission = existingSalary.TotalCommission,
                        Bonus = existingSalary.Bonus,
                        Deduction = existingSalary.Deduction,
                        FinalSalary = existingSalary.TotalSalary,
                        Status = existingSalary.Status,
                        FromDate = existingSalary.FromDate, 
                        ToDate = existingSalary.ToDate
                    });
                }
                else
                {
                    // Calculate Draft based on Range (start -> end)
                    
                    // 1. Work Hours
                    // 1. Work Hours
                    var workSchedules = await _context.WorkSchedules
                        .Include(ws => ws.Shift)
                        .Where(ws => ws.EmployeeId == emp.EmployeeId 
                                     && ws.WorkDate.Date >= start.Date 
                                     && ws.WorkDate.Date <= end.Date
                                     && ws.IsCheckIn) 
                        .ToListAsync();

                    double totalHours = workSchedules.Sum(ws => 
                        ws.Shift != null ? (ws.Shift.EndTime - ws.Shift.StartTime).TotalHours : 0); 

                    // 2. Commission
                    var completedServices = await _context.Appointments
                        .Include(a => a.AppointmentDetails)
                        .Where(a => a.EmployeeId == emp.EmployeeId 
                                    && a.Status == "Completed" 
                                    && a.CreatedDate.Date >= start.Date 
                                    && a.CreatedDate.Date <= end.Date)
                        .ToListAsync();
                    
                    decimal totalCommission = completedServices.Sum(a => a.AppointmentDetails.Sum(ad => ad.PriceAtBooking)) * 0.1m;

                    decimal hourlyRate = emp.BaseSalary / 26 / 8;
                    decimal salaryByHours = hourlyRate * (decimal)totalHours;
                    
                    decimal finalSalary = salaryByHours + totalCommission;

                    payrolls.Add(new SalaryPayrollViewModel
                    {
                        EmployeeId = emp.EmployeeId,
                        EmployeeName = emp.FullName,
                        Month = month,
                        Year = year,
                        TotalWorkHours = totalHours,
                        BaseSalary = emp.BaseSalary,
                        TotalCommission = totalCommission,
                        Bonus = 0,
                        Deduction = 0,
                        FinalSalary = Math.Round(finalSalary, 0),
                        Status = "Draft",
                        FromDate = start, 
                        ToDate = end
                    });
                }
            }
            return payrolls;
        }


        public async Task ConfirmPayrollAsync(int employeeId, int month, int year, decimal finalAmount, DateTime fromDate, DateTime toDate, decimal bonus, decimal deduction)
        {
            var salary = await _context.Salaries
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.Month == month && s.Year == year);

            if (salary == null)
            {
                // Calculate again to be safe or trust passed amount?
                // Trust passed amount for now, but ideally re-calculate.
                // Re-calculating using Range:
                var workSchedules = await _context.WorkSchedules
                        .Include(ws => ws.Shift)
                        .Where(ws => ws.EmployeeId == employeeId 
                                     && ws.WorkDate.Date >= fromDate.Date 
                                     && ws.WorkDate.Date <= toDate.Date 
                                     && ws.IsCheckIn)
                        .ToListAsync();

                double totalHours = workSchedules.Sum(ws => 
                    ws.Shift != null ? (ws.Shift.EndTime - ws.Shift.StartTime).TotalHours : 0);

                var completedServices = await _context.Appointments
                        .Include(a => a.AppointmentDetails)
                        .Where(a => a.EmployeeId == employeeId 
                                    && a.Status == "Completed" 
                                    && a.CreatedDate.Date >= fromDate.Date 
                                    && a.CreatedDate.Date <= toDate.Date)
                        .ToListAsync();
                decimal totalCommission = completedServices.Sum(a => a.AppointmentDetails.Sum(ad => ad.PriceAtBooking)) * 0.1m;
                
                // Recalculate Final Salary with Bonus/Deduction
                // Final = BaseSalary (by hours) + Commission + Bonus - Deduction
                // Need Base Salary info
                var emp = await _context.Employees.FindAsync(employeeId);
                decimal baseSalary = emp?.BaseSalary ?? 0;
                decimal hourlyRate = baseSalary / 26 / 8;
                decimal salaryByHours = hourlyRate * (decimal)totalHours;
                
                decimal calculatedFinal = salaryByHours + totalCommission + bonus - deduction;

                salary = new Salary
                {
                    EmployeeId = employeeId,
                    Month = month,
                    Year = year,
                    TotalWorkHours = totalHours,
                    TotalCommission = totalCommission,
                    Bonus = bonus,
                    Deduction = deduction,
                    TotalSalary = Math.Round(calculatedFinal, 0),
                    Status = "ManagerConfirmed",
                    FromDate = fromDate, // SAVE RANGE
                    ToDate = toDate
                };
                _context.Salaries.Add(salary);
            }
            else
            {
                // If exists, update status and amount and bonus/deduction
                // Note: We should probably re-calculate the final amount here too to be safe, 
                // but if we trust the inputs (or if the caller passed the *new* final amount which includes bonus/deduction)
                // Actually, the caller (UI) might just pass the "Base + Commission" part as FinalAmount and we add Bonus/Deduction here?
                // OR the Caller passes the *Final* amount.
                // Let's assume the caller passes the components or we stick to re-calculation.
                // Re-calculation is safer.
                
                // Let's just update the fields provided.
                salary.Status = "ManagerConfirmed";
                salary.Bonus = bonus;
                salary.Deduction = deduction;
                // Re-calculate Final Amount: 
                // Original Final (without bonus/deduction) ?? 
                // To keep it simple: Let's assume 'finalAmount' passed into this function is NOT including the new bonus/deduction yet if we just typed it in?
                // OR we re-calculate everything. 
                
                // Safest: Calculate SalaryByHours + Commission again.
                // Copy-paste logic from above? Or refactor?
                // For now, let's copy-paste for speed.
                 var workSchedules = await _context.WorkSchedules
                        .Include(ws => ws.Shift)
                        .Where(ws => ws.EmployeeId == employeeId 
                                     && ws.WorkDate.Date >= fromDate.Date 
                                     && ws.WorkDate.Date <= toDate.Date 
                                     && ws.IsCheckIn)
                        .ToListAsync();
                double totalHours = workSchedules.Sum(ws => 
                     ws.Shift != null ? (ws.Shift.EndTime - ws.Shift.StartTime).TotalHours : 0);

                var completedServices = await _context.Appointments
                        .Include(a => a.AppointmentDetails)
                        .Where(a => a.EmployeeId == employeeId 
                                    && a.Status == "Completed" 
                                    && a.CreatedDate.Date >= fromDate.Date 
                                    && a.CreatedDate.Date <= toDate.Date)
                        .ToListAsync();
                decimal totalCommission = completedServices.Sum(a => a.AppointmentDetails.Sum(ad => ad.PriceAtBooking)) * 0.1m;
                
                var emp = await _context.Employees.FindAsync(employeeId);
                decimal baseSalary = emp?.BaseSalary ?? 0;
                decimal hourlyRate = baseSalary / 26 / 8;
                decimal salaryByHours = hourlyRate * (decimal)totalHours;

                salary.TotalSalary = Math.Round(salaryByHours + totalCommission + bonus - deduction, 0);                 
                
                salary.FromDate = fromDate; 
                salary.ToDate = toDate;
                _context.Salaries.Update(salary);
            }

            await _context.SaveChangesAsync();
        }

        public async Task ConfirmSalaryByEmployeeAsync(int salaryId)
        {
            var salary = await _context.Salaries.FindAsync(salaryId);
            if (salary == null) throw new Exception("Không tìm thấy bảng lương.");

            // Chỉ cho phép xác nhận khi Manager đã gửi
            if (salary.Status != "ManagerConfirmed") 
            {
                throw new Exception("Bảng lương chưa được quản lý chốt hoặc đã hoàn tất.");
            }

            salary.Status = "Completed"; // Trạng thái cuối cùng
            _context.Salaries.Update(salary);
            await _context.SaveChangesAsync();
        }

        public async Task<(DateTime? FromDate, DateTime? ToDate)> GetLatestPayrollPeriodAsync()
        {
            var latestSalary = await _context.Salaries
                .Where(s => s.Status == "ManagerConfirmed" || s.Status == "Completed")
                .OrderByDescending(s => s.ToDate)
                .FirstOrDefaultAsync();

            if (latestSalary != null)
            {
                return (latestSalary.FromDate, latestSalary.ToDate);
            }
            return (null, null);
        }
    }
}