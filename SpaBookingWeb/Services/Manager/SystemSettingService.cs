using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity; 
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; 
using System.Threading.Tasks;



namespace SpaBookingWeb.Services.Manager
{
    public class SystemSettingService : ISystemSettingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public SystemSettingService(ApplicationDbContext context, 
                                    IWebHostEnvironment webHostEnvironment,
                                    RoleManager<IdentityRole> roleManager,
                                    UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // --- 1. GENERAL CONFIGURATION LOGIC ---
        public async Task<SystemSettingViewModel> GetCurrentSettingsAsync()
        {
            var settings = await _context.SystemSettings.ToListAsync();
            var settingsDict = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue);

            string GetValue(string key, string defaultValue = "") => 
                settingsDict.ContainsKey(key) ? settingsDict[key] : defaultValue;

            var model = new SystemSettingViewModel
            {
                SpaName = GetValue("SpaName", "Spa System"),
                PhoneNumber = GetValue("PhoneNumber"),
                Email = GetValue("Email"),
                Address = GetValue("Address"),
                LogoUrl = GetValue("LogoUrl"),
                FacebookUrl = GetValue("FacebookUrl"),
                OpenTime = TimeSpan.TryParse(GetValue("OpenTime"), out var open) ? open : new TimeSpan(8, 0, 0),
                CloseTime = TimeSpan.TryParse(GetValue("CloseTime"), out var close) ? close : new TimeSpan(22, 0, 0),
                DepositPercentage = int.TryParse(GetValue("DepositPercentage"), out var dep) ? dep : 30
            };

            model.Units = await _context.Units.ToListAsync();

            model.DepositRules = await _context.DepositRules
                .Include(d => d.TargetService)
                .Include(d => d.TargetMembershipType)
                .ToListAsync();

            // Load Customer List for Promotion Tab
            var customers = await _context.Customers.OrderBy(c => c.FullName).ToListAsync();
            model.AllCustomers = customers.Select(c => new CustomerViewModel
            {
                CustomerId = c.CustomerId,
                FullName = c.FullName,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                Point = c.Point
            }).ToList();

            // Load Dropdown Data
            var services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            model.AvailableServices = services.Select(s => new SelectListItem
            {
                Value = s.ServiceId.ToString(),
                Text = $"{s.ServiceName} ({s.Price:N0}đ)"
            });

            var membershipTypes = await _context.MembershipTypes.ToListAsync();
            model.AvailableMembershipTypes = membershipTypes.Select(m => new SelectListItem
            {
                Value = m.MembershipTypeId.ToString(),
                Text = m.TypeName
            });

            return model;
        }

        public async Task UpdateSettingsAsync(SystemSettingViewModel model)
        {
            string logoPath = model.LogoUrl;
            
            // Handle image upload if new file exists
            if (model.LogoFile != null)
            {
                logoPath = await SaveImageAsync(model.LogoFile);
            }

            var valuesToUpdate = new Dictionary<string, string>
            {
                { "SpaName", model.SpaName },
                { "PhoneNumber", model.PhoneNumber },
                { "Email", model.Email },
                { "Address", model.Address },
                { "LogoUrl", logoPath }, // Save image path
                { "FacebookUrl", model.FacebookUrl },
                { "OpenTime", model.OpenTime.ToString() },
                { "CloseTime", model.CloseTime.ToString() },
                { "DepositPercentage", model.DepositPercentage.ToString() }
            };

            bool hasChanges = false;
            var changedKeys = new List<string>();

            foreach (var kvp in valuesToUpdate)
            {
                // Find setting by Key (use AsNoTracking to avoid conflict if any)
                var setting = await _context.SystemSettings.FindAsync(kvp.Key);
                
                if (setting == null)
                {
                    // If not exists -> Create new (Add)
                    setting = new SystemSetting 
                    { 
                        SettingKey = kvp.Key, 
                        SettingValue = kvp.Value ?? "", 
                        Description = $"Setting {kvp.Key}" 
                    };
                    _context.SystemSettings.Add(setting);
                    hasChanges = true;
                    changedKeys.Add(kvp.Key + "(New)");
                }
                else
                {
                    // If exists -> Check difference before Update
                    if (setting.SettingValue != (kvp.Value ?? ""))
                    {
                        setting.SettingValue = kvp.Value ?? "";
                        _context.SystemSettings.Update(setting); // Mark update
                        hasChanges = true;
                        changedKeys.Add(kvp.Key);
                    }
                }
            }
            
            if (hasChanges)
            {
                // Record Activity Log for tracking
                var log = new ActivityLog
                {
                    Action = "Update",
                    EntityName = "SystemSettings",
                    EntityId = "Global",
                    Description = "General settings updated: " + string.Join(", ", changedKeys),
                    AffectedColumns = "[]" // Or serialize changedKeys if detail needed
                };
                _context.ActivityLogs.Add(log);

                // Save all to DB
                await _context.SaveChangesAsync();
            }
        }



        // --- 3. HELPER CRUD METHODS (Unit, Role, DepositRule) ---
        public async Task AddUnitAsync(string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName)) return;
            var unit = new Unit { UnitName = unitName };
            _context.Units.Add(unit);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteUnitAsync(int id)
        {
            var unit = await _context.Units.FindAsync(id);
            if (unit != null)
            {
                _context.Units.Remove(unit); 
                await _context.SaveChangesAsync();
            }
        }



        // --- 4. DEPOSIT RULE LOGIC ---
        public async Task AddDepositRuleAsync(SystemSettingViewModel model)
        {
            var rule = new DepositRule
            {
                RuleName = model.NewRuleName,
                ApplyToType = model.NewApplyToType,
                MinOrderValue = model.NewMinOrderValue,
                DepositType = model.NewDepositType,
                DepositValue = model.NewDepositValue,
                IsActive = true
            };

            if (model.NewApplyToType == "SpecificService")
            {
                rule.TargetServiceId = model.NewTargetServiceId;
            }
            else if (model.NewApplyToType == "MembershipType")
            {
                rule.TargetMembershipTypeId = model.NewTargetMembershipTypeId;
            }

            _context.DepositRules.Add(rule);
            
            // Write Log
            var log = new ActivityLog 
            { 
                Action = "Create", EntityName = "DepositRules", EntityId = rule.RuleName,
                Description = $"Created deposit rule: {rule.RuleName}", AffectedColumns = "[]"
            };
            _context.ActivityLogs.Add(log);

            await _context.SaveChangesAsync();
        }

        public async Task DeleteDepositRuleAsync(int id)
        {
            var rule = await _context.DepositRules.FindAsync(id);
            if (rule != null)
            {
                _context.DepositRules.Remove(rule);
                
                // Write Log
                var log = new ActivityLog 
                { 
                    Action = "Delete", EntityName = "DepositRules", EntityId = id.ToString(),
                    Description = $"Deleted deposit rule: {rule.RuleName}", AffectedColumns = "[]"
                };
                _context.ActivityLogs.Add(log);

                await _context.SaveChangesAsync();
            }
        }

        // --- 5. PROMOTE CUSTOMER ---
        public async Task PromoteCustomerAsync(int customerId, string roleName)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) throw new Exception("Customer not found.");

            if (string.IsNullOrEmpty(roleName)) roleName = "Staff";

            // 1. Check User
            string email = customer.Email;
            string phone = customer.PhoneNumber;

            if (string.IsNullOrEmpty(email)) 
            {
                // If no email, create fake email: phone@spa.system
                email = $"{phone}@spa.system"; 
            }

            var user = await _userManager.FindByNameAsync(email) ?? await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Create new User
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = customer.FullName,
                    PhoneNumber = phone,
                    EmailConfirmed = true,
                    Address = "Not updated", 
                    CreatedDate = DateTime.Now
                };
                var result = await _userManager.CreateAsync(user, "Password123!"); // Default pass
                if (!result.Succeeded)
                {
                    throw new Exception("User account creation error: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            // 2. Create Employee
            var existingEmp = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == user.Id);
            if (existingEmp != null)
            {
                // If User is already Employee -> Skip or report error
                throw new Exception("This account (Email/Phone) is already an employee in the system.");
            }

            // Create Employee with default full info to avoid database constraint error
            var newEmployee = new Employee
            {
                IdentityUserId = user.Id,
                FullName = customer.FullName,
                Gender = "Other", // Default gender
                DateOfBirth = new DateTime(2000, 1, 1), // Default DOB
                Address = "Not updated",
                BaseSalary = 5000000, 
                HireDate = DateTime.Now,
                IsActive = true,
                Avatar = "/ManagerAssets/assets/avatars/face-1.jpg" // Default Avatar
            };

            _context.Employees.Add(newEmployee);
            await _context.SaveChangesAsync(); // Save to generate EmployeeId

            // 3. Create Default TechnicianDetail (1-1 relation, should exist to avoid errors in other modules)
            var techDetail = new TechnicianDetail
            {
                EmployeeId = newEmployee.EmployeeId,
                SkillLevel = "Junior",
                Bio = "New employee promoted from customer.",
                CommissionRate = 0,
                IsDeleted = false
            };
            _context.TechnicianDetails.Add(techDetail);
            await _context.SaveChangesAsync();

            // 4. Assign Role
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
            await _userManager.AddToRoleAsync(user, roleName);
        }

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            string uniqueFileName = "logo_" + Guid.NewGuid().ToString() + "_" + imageFile.FileName;
            // Save to wwwroot/images/system
            string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "system");
            if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);
            
            string filePath = Path.Combine(uploadFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            return "/images/system/" + uniqueFileName;
        }
    }
}