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
        // 1. EMPLOYEE MANAGEMENT
        // ====================================================================

        public async Task<List<EmployeeListViewModel>> GetAllEmployeesAsync()
        {
            var employees = await _context.Employees
                .Include(e => e.ApplicationUser)
                .Where(e => !EF.Property<bool>(e, "IsDeleted")) // Soft delete filter
                .ToListAsync();

            var viewModels = new List<EmployeeListViewModel>();

            foreach (var emp in employees)
            {
                // Get User Role
                var roles = emp.ApplicationUser != null 
                    ? await _userManager.GetRolesAsync(emp.ApplicationUser) 
                    : new List<string>();
                // Calculate average rating
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
                    Position = roles.FirstOrDefault(r => r != "Customer") ?? "Customer", 
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
                .Include(e => e.TechnicianServices) // Include services
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
                // Load Selected Services
                SelectedServiceIds = emp.TechnicianServices.Select(ts => ts.ServiceId).ToList()
            };

            // Load Selected Role
            if (emp.ApplicationUser != null)
            {
                var userRoles = await _userManager.GetRolesAsync(emp.ApplicationUser);
                // Fix: Ignore "Customer" role to pick the actual functional role
                var roleName = userRoles.FirstOrDefault(r => r != "Customer");
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
                throw new Exception("Account creation error: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            // 1. Assign Role
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
            await _context.SaveChangesAsync(); // Save to get EmployeeId

            // 2. Save Services (If Technician and services selected)
            // Assume Technician role is named "Technician"
            // Or simply save if services are selected
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
            // Check if this is an actual employee or just a user with employee role
            var emp = await _context.Employees
                .Include(e => e.TechnicianServices)
                .FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId);

            bool hasEmployeeRecord = (emp != null);
            
            System.Diagnostics.Debug.WriteLine($"[UPDATE EMPLOYEE] EmployeeId: {model.EmployeeId}, Has Employee Record: {hasEmployeeRecord}");

            // Find the user - either through Employee record or by email
            ApplicationUser user;
            if (hasEmployeeRecord)
            {
                user = await _userManager.FindByIdAsync(emp.IdentityUserId);
            }
            else
            {
                // No employee record, find by email
                user = await _userManager.FindByEmailAsync(model.Email);
            }
            
            if (user == null)
            {
                throw new Exception($"User not found for email: {model.Email}");
            }

            // 1. Update Identity User Info
            System.Diagnostics.Debug.WriteLine($"[UPDATE EMPLOYEE] Updating user: {user.Email}");
            
            // Update User Details
            user.Email = model.Email;
            user.UserName = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.FullName = model.FullName;
            user.Address = model.Address;
            
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new Exception($"User update failed: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
            }

            // 2. Update Role - Direct database approach to prevent duplicate key errors
            System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] Starting role update for user: {user.Email}");
            
            // Get the new role (if selected)
            string newRoleId = model.SelectedRoleId;
            string newRoleName = null;
            if (!string.IsNullOrEmpty(newRoleId))
            {
                var newRole = await _roleManager.FindByIdAsync(newRoleId);
                if (newRole != null)
                {
                    newRoleName = newRole.Name;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] New Role: {newRoleName ?? "NONE"}");
            
            // CRITICAL FIX: Directly clean up AspNetUserRoles table to prevent duplicates
            // Get all non-Customer roles for this user from database
            var allRoles = await _roleManager.Roles.ToListAsync();
            
            // Execute raw SQL to remove all employee role assignments (except Customer)
            var employeeRoleIds = allRoles
                .Where(r => r.Name != "Customer")
                .Select(r => r.Id)
                .ToList();
            
            if (employeeRoleIds.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] Cleaning up existing role assignments...");
                
                // Use raw SQL to delete from AspNetUserRoles
                var deleteQuery = $@"
                    DELETE FROM AspNetUserRoles 
                    WHERE UserId = @p0 
                    AND RoleId IN ({string.Join(",", employeeRoleIds.Select((_, i) => $"@p{i + 1}"))})";
                
                var parameters = new List<object> { user.Id };
                parameters.AddRange(employeeRoleIds.Cast<object>());
                
                await _context.Database.ExecuteSqlRawAsync(deleteQuery, parameters.ToArray());
                System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] Cleaned up existing employee roles from database");
            }
            
            // Now add the new role if specified
            if (!string.IsNullOrEmpty(newRoleId))
            {
                System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] Adding new role: {newRoleName}");
                
                // Use raw SQL to insert into AspNetUserRoles
                var insertQuery = "INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@p0, @p1)";
                
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(insertQuery, user.Id, newRoleId);
                    System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] Successfully added role via direct SQL");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] ERROR: {ex.Message}");
                    // Role might already exist - that's OK, continue
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ROLE UPDATE] Role update completed");
            
            // Detach ApplicationUser to prevent tracking conflicts
            var userEntry = _context.Entry(user);
            if (userEntry.State != EntityState.Detached)
            {
                userEntry.State = EntityState.Detached;
            }

            // 3. Update Employee Details (only if employee record exists)
            if (hasEmployeeRecord)
            {
                System.Diagnostics.Debug.WriteLine($"[UPDATE EMPLOYEE] Updating Employee record fields");
                
                emp.FullName = model.FullName;
                emp.BaseSalary = model.BaseSalary;
                emp.Address = model.Address;
                emp.DateOfBirth = model.DateOfBirth;
                emp.Gender = model.Gender;
                emp.IsActive = model.IsActive;

                // 4. Update Services
                var currentServiceIds = model.SelectedServiceIds ?? new List<int>();
                
                var allExistingServices = await _context.TechnicianServices
                    .IgnoreQueryFilters()
                    .Where(ts => ts.EmployeeId == emp.EmployeeId)
                    .ToListAsync();

                foreach (var existing in allExistingServices)
                {
                    if (currentServiceIds.Contains(existing.ServiceId))
                    {
                        var entry = _context.Entry(existing);
                        if (entry.CurrentValues.Properties.Any(p => p.Name == "IsDeleted"))
                        {
                            entry.CurrentValues["IsDeleted"] = false;
                        }
                    }
                    else
                    {
                        _context.TechnicianServices.Remove(existing);
                    }
                }

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

                // Save employee changes
                try
                {
                    await _context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine($"[UPDATE EMPLOYEE] Employee record saved successfully");
                }
                catch (DbUpdateException ex)
                {
                    // Capture inner exception details for debugging
                    var innerMessage = ex.InnerException?.Message ?? ex.Message;
                    var fullMessage = $"Database update error: {innerMessage}";
                    
                    // Log additional details if available
                    if (ex.InnerException?.InnerException != null)
                    {
                        fullMessage += $" | Inner: {ex.InnerException.InnerException.Message}";
                    }
                    
                    throw new Exception(fullMessage, ex);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[UPDATE EMPLOYEE] No Employee record - skipping Employee table updates");
            }
        }

        public async Task<List<SelectListItem>> GetRolesSelectListAsync()
        {
            return await _roleManager.Roles
                .Where(r => r.Name != "Customer") // Exclude Customer role from dropdown
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
                // Soft delete logic (configured in DbContext)
                emp.IsActive = false;
                _context.Employees.Remove(emp); // DbContext will automatically convert to Soft Delete
                await _context.SaveChangesAsync();
            }
        }

        // ====================================================================
        // 2. WORK SCHEDULE & ATTENDANCE
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
            
            // Ensure not return null
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
                // Check if any work schedule is using this shift?
                var isUsed = await _context.WorkSchedules.AnyAsync(ws => ws.ShiftId == shiftId);
                if (isUsed) throw new Exception("Cannot delete shift currently used in employee schedule.");

                _context.Shifts.Remove(shift);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<DailyScheduleViewModel> GetDailyScheduleAsync(DateTime date)
        {
            var shifts = await _context.Shifts.ToListAsync();
            
            // Get work schedule for that day
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
                    
                    // Map to WorkScheduleViewModel to get attendance info
                    Schedules = schedules
                        .Where(s => s.ShiftId == shift.ShiftId)
                        .Select(s => new WorkScheduleViewModel
                        {
                            ScheduleId = s.ScheduleId, // Map with ScheduleId in WorkSchedule Model
                            EmployeeId = s.EmployeeId,
                            EmployeeName = s.Employee.FullName,
                            Position = "Technician",
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

            if (exists) throw new Exception("Employee is already assigned to this shift.");

            var schedule = new WorkSchedule
            {
                EmployeeId = employeeId,
                ShiftId = shiftId,
                WorkDate = date,
                IsCheckIn = false, // Default not checked in
                Note = string.Empty // Fix: Note column does not allow NULL
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
            if (schedule == null) throw new Exception("Work schedule not found.");

            schedule.IsCheckIn = isPresent;
            schedule.Note = note ?? ""; 
            schedule.IsOnBreak = isOnBreak;
            schedule.BreakStartTime = breakStartTime;
            
            _context.WorkSchedules.Update(schedule);
            await _context.SaveChangesAsync();
        }

        // ====================================================================
        // 3. TIP MANAGEMENT
        // ====================================================================

        public async Task<List<DailyTipViewModel>> GetDailyTipsAsync(DateTime date)
        {
            // NOTE: Assumes you added TipAmount and IsTipPaid to Appointment
            // If Model doesn't have it, you need to add to Appointment Entity
            var tips = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                // Assume TipAmount exists in DB. If not, code will compile error here.
                // .Where(a => a.CreatedDate.Date == date.Date && a.TipAmount > 0) 
                // Temporarily comment TipAmount logic to make code run with current Model
                .Where(a => a.CreatedDate.Date == date.Date) 
                .Select(a => new DailyTipViewModel
                {
                    TipId = a.AppointmentId,
                    CreatedDate = a.CreatedDate,
                    CustomerName = a.Customer.FullName,
                    EmployeeName = a.Employee != null ? a.Employee.FullName : "N/A",
                    Amount = 0, // Replace with a.TipAmount when DB updated
                    IsDistributed = false // Replace with a.IsTipPaid when DB updated
                })
                .ToListAsync();

            return tips;
        }

        public async Task<decimal> GetTotalTipsAmountAsync(DateTime date)
        {
            // Temporarily return 0 because no TipAmount column
            return 0;
            // return await _context.Appointments
            //    .Where(a => a.CreatedDate.Date == date.Date)
            //    .SumAsync(a => a.TipAmount ?? 0);
        }

        public async Task ConfirmTipSentToEmployeeAsync(int tipId)
        {
            var appointment = await _context.Appointments.FindAsync(tipId);
            if (appointment == null) throw new Exception("Order not found.");

            // appointment.IsTipPaid = true; // Need to update Appointment DB
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
        }

        public async Task ConfirmAllTipsForDateAsync(DateTime date)
        {
            // Logic waiting for Appointment DB update
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
            if (salary == null) throw new Exception("Payroll not found.");

            // Only allow confirmation when Manager has sent
            if (salary.Status != "ManagerConfirmed") 
            {
                throw new Exception("Payroll not finalized by manager or already completed.");
            }

            salary.Status = "Completed"; // Final status
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