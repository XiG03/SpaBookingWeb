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

        // --- 1. LOGIC CẤU HÌNH CHUNG ---
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
            
            // Xử lý upload ảnh nếu có file mới
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
                { "LogoUrl", logoPath }, // Lưu đường dẫn ảnh
                { "FacebookUrl", model.FacebookUrl },
                { "OpenTime", model.OpenTime.ToString() },
                { "CloseTime", model.CloseTime.ToString() },
                { "DepositPercentage", model.DepositPercentage.ToString() }
            };

            bool hasChanges = false;
            var changedKeys = new List<string>();

            foreach (var kvp in valuesToUpdate)
            {
                // Tìm setting theo Key (dùng AsNoTracking để tránh xung đột nếu có)
                var setting = await _context.SystemSettings.FindAsync(kvp.Key);
                
                if (setting == null)
                {
                    // Nếu chưa có -> Tạo mới (Add)
                    setting = new SystemSetting 
                    { 
                        SettingKey = kvp.Key, 
                        SettingValue = kvp.Value ?? "", 
                        Description = $"Cấu hình {kvp.Key}" 
                    };
                    _context.SystemSettings.Add(setting);
                    hasChanges = true;
                    changedKeys.Add(kvp.Key + "(New)");
                }
                else
                {
                    // Nếu có rồi -> Kiểm tra khác biệt mới Update
                    if (setting.SettingValue != (kvp.Value ?? ""))
                    {
                        setting.SettingValue = kvp.Value ?? "";
                        _context.SystemSettings.Update(setting); // Đánh dấu update
                        hasChanges = true;
                        changedKeys.Add(kvp.Key);
                    }
                }
            }
            
            if (hasChanges)
            {
                // Ghi Activity Log để theo dõi
                var log = new ActivityLog
                {
                    Action = "Update",
                    EntityName = "SystemSettings",
                    EntityId = "Global",
                    Description = "Cập nhật cấu hình chung: " + string.Join(", ", changedKeys),
                    AffectedColumns = "[]" // Hoặc serialize changedKeys nếu muốn chi tiết
                };
                _context.ActivityLogs.Add(log);

                // Lưu tất cả vào DB
                await _context.SaveChangesAsync();
            }
        }



        // --- 3. CÁC HÀM CRUD PHỤ TRỢ (Unit, Role, DepositRule) ---
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



        // --- 4. LOGIC QUY TẮC ĐẶT CỌC ---
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
            
            // Ghi Log
            var log = new ActivityLog 
            { 
                Action = "Create", EntityName = "DepositRules", EntityId = rule.RuleName,
                Description = $"Tạo quy tắc cọc: {rule.RuleName}", AffectedColumns = "[]"
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
                
                // Ghi Log
                var log = new ActivityLog 
                { 
                    Action = "Delete", EntityName = "DepositRules", EntityId = id.ToString(),
                    Description = $"Xóa quy tắc cọc: {rule.RuleName}", AffectedColumns = "[]"
                };
                _context.ActivityLogs.Add(log);

                await _context.SaveChangesAsync();
            }
        }

        // --- 5. NÂNG QUYỀN KHÁCH HÀNG ---
        // --- 5. NÂNG QUYỀN KHÁCH HÀNG ---
        public async Task PromoteCustomerAsync(int customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) throw new Exception("Không tìm thấy khách hàng.");

            // 1. Kiểm tra User
            string email = customer.Email;
            string phone = customer.PhoneNumber;

            if (string.IsNullOrEmpty(email)) 
            {
                // Nếu không có email, tạo email giả: phone@spa.system
                email = $"{phone}@spa.system"; 
            }

            var user = await _userManager.FindByNameAsync(email) ?? await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Tạo User mới
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = customer.FullName,
                    PhoneNumber = phone,
                    EmailConfirmed = true,
                    Address = "Chưa cập nhật", 
                    CreatedDate = DateTime.Now
                };
                var result = await _userManager.CreateAsync(user, "Password123!"); // Pass mặc định
                if (!result.Succeeded)
                {
                    throw new Exception("Lỗi tạo tài khoản User: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            // 2. Tạo Employee
            var existingEmp = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == user.Id);
            if (existingEmp != null)
            {
                // Nếu User đã là Employee -> Bỏ qua hoặc báo lỗi
                throw new Exception("Tài khoản này (Email/SĐT) đã là nhân viên trong hệ thống.");
            }

            // Tạo Employee với đầy đủ thông tin mặc định để tránh lỗi database constraint
            var newEmployee = new Employee
            {
                IdentityUserId = user.Id,
                FullName = customer.FullName,
                Gender = "Khác", // Default gender
                DateOfBirth = new DateTime(2000, 1, 1), // Default DOB
                Address = "Chưa cập nhật",
                BaseSalary = 5000000, 
                HireDate = DateTime.Now,
                IsActive = true,
                Avatar = "/ManagerAssets/assets/avatars/face-1.jpg" // Default Avatar
            };

            _context.Employees.Add(newEmployee);
            await _context.SaveChangesAsync(); // Lưu để sinh EmployeeId

            // 3. Tạo TechnicianDetail mặc định (Quan hệ 1-1, nên có để tránh lỗi ở các module khác)
            var techDetail = new TechnicianDetail
            {
                EmployeeId = newEmployee.EmployeeId,
                SkillLevel = "Junior",
                Bio = "Nhân viên mới được thăng cấp từ khách hàng.",
                CommissionRate = 0,
                IsDeleted = false
            };
            _context.TechnicianDetails.Add(techDetail);
            await _context.SaveChangesAsync();

            // 4. Gán quyền "Staff"
            if (!await _roleManager.RoleExistsAsync("Staff"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Staff"));
            }
            await _userManager.AddToRoleAsync(user, "Staff");
        }

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            string uniqueFileName = "logo_" + Guid.NewGuid().ToString() + "_" + imageFile.FileName;
            // Lưu vào thư mục wwwroot/images/system
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