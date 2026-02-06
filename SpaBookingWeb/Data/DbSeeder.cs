using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpaBookingWeb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Data
{
    public static class DbSeeder
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. System Settings & Rules
            SeedSystemConfig(context);

            // 2. Core Data (Units, Categories, Services, Products, Combos)
            SeedCatalog(context);

            // 3. Membership Types
            SeedMembershipTypes(context);

            // 4. Identity (Roles, Users, Employees)
            await SeedRolesAsync(roleManager);
            await SeedUsersAndEmployeesAsync(userManager, context);

            // 5. Customers
            SeedCustomers(context);

            // 6. Operations (Shifts, Schedules, Vouchers)
            SeedOperationsConfig(context);

            // 7. Transaction Categories & Budgets
            SeedFinanceConfig(context);

            // 8. Dynamic Data (Appointments, Invoices, Transactions, Reviews)
            // Note: This depends on Customers, Employees, Services existing
            SeedTransactionalData(context);

            // 9. CMS (Posts)
            SeedCMS(context);
        }

        private static void SeedSystemConfig(ApplicationDbContext context)
        {
            if (!context.SystemSettings.Any())
            {
                context.SystemSettings.AddRange(
                    new SystemSetting { SettingKey = "ShopName", SettingValue = "Lotus Spa & Wellness", Description = "Tên hiển thị của Spa" },
                    new SystemSetting { SettingKey = "Address", SettingValue = "123 Đường Hoa Sen, Quận 1, TP.HCM", Description = "Địa chỉ liên hệ" },
                    new SystemSetting { SettingKey = "Hotline", SettingValue = "1900 1234", Description = "Số điện thoại tổng đài" },
                    new SystemSetting { SettingKey = "OpenTime", SettingValue = "09:00", Description = "Giờ mở cửa" },
                    new SystemSetting { SettingKey = "CloseTime", SettingValue = "22:00", Description = "Giờ đóng cửa" }
                );
                context.SaveChanges();
            }

            if (!context.DepositRules.Any())
            {
                context.DepositRules.AddRange(
                    new DepositRule { RuleName = "Đặt cọc mặc định", ApplyToType = "OrderTotal", MinOrderValue = 500000, DepositType = "Percent", DepositValue = 20, Priority = 1, IsActive = true }
                );
                context.SaveChanges();
            }
        }

        private static void SeedCatalog(ApplicationDbContext context)
        {
            // Units
            if (!context.Units.Any())
            {
                context.Units.AddRange(
                    new Unit { UnitName = "Lần" },
                    new Unit { UnitName = "Suất" },
                    new Unit { UnitName = "Gói" },
                    new Unit { UnitName = "Chai" },
                    new Unit { UnitName = "Hộp" },
                    new Unit { UnitName = "Cái" },
                    new Unit { UnitName = "ml" }
                );
                context.SaveChanges();
            }

            // Categories
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { CategoryName = "Massage Body", Type = "Service" },
                    new Category { CategoryName = "Chăm sóc da mặt (Facial)", Type = "Service" },
                    new Category { CategoryName = "Gội đầu dưỡng sinh", Type = "Service" },
                    new Category { CategoryName = "Mỹ phẩm chăm sóc da", Type = "Product" },
                    new Category { CategoryName = "Dầu gội & Dầu xả", Type = "Product" },
                    new Category { CategoryName = "Vật tư tiêu hao", Type = "Product" }
                );
                context.SaveChanges();
            }

            // Products
            if (!context.Products.Any())
            {
                var catCosmetic = context.Categories.FirstOrDefault(c => c.CategoryName == "Mỹ phẩm chăm sóc da");
                var catMaterial = context.Categories.FirstOrDefault(c => c.CategoryName == "Vật tư tiêu hao");
                var unitChai = context.Units.FirstOrDefault(u => u.UnitName == "Chai");
                var unitHop = context.Units.FirstOrDefault(u => u.UnitName == "Hộp");

                if (catCosmetic != null && unitChai != null)
                {
                    context.Products.AddRange(
                        new Product { ProductName = "Tinh dầu Lavender", CategoryId = catCosmetic.CategoryId, UnitId = unitChai.UnitId, PurchasePrice = 150000, SalePrice = 250000, StockQuantity = 50, IsForSale = true },
                        new Product { ProductName = "Kem dưỡng ẩm Aloe Vera", CategoryId = catCosmetic.CategoryId, UnitId = unitHop.UnitId, PurchasePrice = 200000, SalePrice = 350000, StockQuantity = 30, IsForSale = true }
                    );
                }
                if (catMaterial != null && unitHop != null)
                {
                    context.Products.Add(new Product { ProductName = "Khăn giấy lụa", CategoryId = catMaterial.CategoryId, UnitId = unitHop.UnitId, PurchasePrice = 10000, SalePrice = 0, StockQuantity = 100, IsForSale = false });
                }
                context.SaveChanges();
            }

            // Services & Consumables
            if (!context.Services.Any())
            {
                var catMassage = context.Categories.FirstOrDefault(c => c.CategoryName == "Massage Body");
                var catFace = context.Categories.FirstOrDefault(c => c.CategoryName.Contains("Facial"));
                
                var oilProd = context.Products.FirstOrDefault(p => p.ProductName.Contains("Lavender"));

                if (catMassage != null)
                {
                    var s1 = new Service { ServiceName = "Massage Thụy Điển", CategoryId = catMassage.CategoryId, Price = 350000, DurationMinutes = 60, Description = "Massage thư giãn toàn thân với tinh dầu tự nhiên.", Image = "/img/services/swedish.jpg", IsActive = true };
                    var s2 = new Service { ServiceName = "Massage Đá Nóng", CategoryId = catMassage.CategoryId, Price = 500000, DurationMinutes = 90, Description = "Kết hợp đá nóng bazan và tinh dầu.", Image = "/img/services/hotstone.jpg", IsActive = true };
                    
                    context.Services.AddRange(s1, s2);
                    context.SaveChanges();

                    // Seed Consumables
                    if (oilProd != null)
                    {
                        context.ServiceConsumables.AddRange(
                            new ServiceConsumable { ServiceId = s1.ServiceId, ProductId = oilProd.ProductId, Quantity = 10 }, // 10ml
                            new ServiceConsumable { ServiceId = s2.ServiceId, ProductId = oilProd.ProductId, Quantity = 15 }
                        );
                        context.SaveChanges();
                    }
                }

                if (catFace != null)
                {
                    context.Services.Add(new Service { ServiceName = "Chăm sóc da cấp ẩm", CategoryId = catFace.CategoryId, Price = 300000, DurationMinutes = 45, Description = "Liệu trình làm sạch sâu và cấp ẩm.", Image = "/img/services/hydration.jpg", IsActive = true });
                    context.SaveChanges();
                }
            }

            // Combos
            if (!context.Combos.Any())
            {
                var sMassage = context.Services.FirstOrDefault(s => s.ServiceName == "Massage Thụy Điển");
                var sFace = context.Services.FirstOrDefault(s => s.ServiceName == "Chăm sóc da cấp ẩm");

                if (sMassage != null && sFace != null)
                {
                    var combo = new Combo
                    {
                        ComboName = "Thư giãn toàn diện",
                        Price = 550000, // Discounted from 650k
                        Description = "Combo tiết kiệm Massage Body + Facial",
                        Image = "/img/combos/fullbody.jpg"
                    };
                    context.Combos.Add(combo);
                    context.SaveChanges();

                    context.ComboDetails.AddRange(
                        new ComboDetail { ComboId = combo.ComboId, ServiceId = sMassage.ServiceId },
                        new ComboDetail { ComboId = combo.ComboId, ServiceId = sFace.ServiceId }
                    );
                    context.SaveChanges();
                }
            }
        }

        private static void SeedMembershipTypes(ApplicationDbContext context)
        {
            if (!context.MembershipTypes.Any())
            {
                context.MembershipTypes.AddRange(
                    new MembershipType { TypeName = "Thành viên Mới", DiscountPercent = 0, MinSpendRequirement = 0 },
                    new MembershipType { TypeName = "Bạc", DiscountPercent = 5, MinSpendRequirement = 5000000 },
                    new MembershipType { TypeName = "Vàng", DiscountPercent = 10, MinSpendRequirement = 20000000 },
                    new MembershipType { TypeName = "Kim Cương", DiscountPercent = 15, MinSpendRequirement = 50000000 }
                );
                context.SaveChanges();
            }
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames = { "Manager", "Technician", "Receptionist", "Customer" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        private static async Task SeedUsersAndEmployeesAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            string defaultPass = "Password123!";

            // 1. Manager
            await EnsureUserAndRole(userManager, "manager@spa.com", defaultPass, "Manager", "Nguyễn Quản Lý", "Hà Nội");
            await EnsureEmployee(context, userManager, "manager@spa.com", "Nguyễn Quản Lý", 20000000, "Nam");

            // 2. Head Technician
            await EnsureUserAndRole(userManager, "tech@spa.com", defaultPass, "Technician", "Trần Kỹ Thuật", "Hồ Chí Minh");
            var techEmp = await EnsureEmployee(context, userManager, "tech@spa.com", "Trần Kỹ Thuật", 8000000, "Nữ");
            if (techEmp != null)
            {
                if (!context.TechnicianDetails.Any(td => td.EmployeeId == techEmp.EmployeeId))
                {
                    context.TechnicianDetails.Add(new TechnicianDetail { EmployeeId = techEmp.EmployeeId, SkillLevel = "Senior", Bio = "Chuyên gia massage đá nóng", CommissionRate = 5 });
                    
                    // Assign skills
                    var sMsg = context.Services.FirstOrDefault(s => s.ServiceName.Contains("Thụy Điển"));
                    if (sMsg != null) context.TechnicianServices.Add(new TechnicianService { EmployeeId = techEmp.EmployeeId, ServiceId = sMsg.ServiceId });
                    
                    await context.SaveChangesAsync();
                }
            }

            // 3. Receptionist
            await EnsureUserAndRole(userManager, "reception@spa.com", defaultPass, "Receptionist", "Lê Lễ Tân", "Đà Nẵng");
            await EnsureEmployee(context, userManager, "reception@spa.com", "Lê Lễ Tân", 7000000, "Nữ");

            // 4. Customer Login (For website login testing)
            await EnsureUserAndRole(userManager, "khach@spa.com", defaultPass, "Customer", "Chị Khách Mẫu", "Hà Nội");
        }

        private static void SeedCustomers(ApplicationDbContext context)
        {
            if (!context.Customers.Any())
            {
                var memberType = context.MembershipTypes.FirstOrDefault(m => m.TypeName == "Thành viên Mới");
                var goldType = context.MembershipTypes.FirstOrDefault(m => m.TypeName == "Vàng");

                context.Customers.AddRange(
                    new Customer { FullName = "Nguyễn Văn A", PhoneNumber = "0901234567", Email = "a@gmail.com", MembershipTypeId = memberType?.MembershipTypeId, Point = 100 },
                    new Customer { FullName = "Trần Thị B", PhoneNumber = "0909876543", Email = "b@gmail.com", MembershipTypeId = goldType?.MembershipTypeId, Point = 5000 }
                );
                context.SaveChanges();
            }
        }

        private static void SeedOperationsConfig(ApplicationDbContext context)
        {
            // Shifts
            if (!context.Shifts.Any())
            {
                context.Shifts.AddRange(
                    new Shift { ShiftName = "Ca Sáng", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0) },
                    new Shift { ShiftName = "Ca Chiều", StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(22, 0, 0) }
                );
                context.SaveChanges();
            }

            // Work Schedule (Current Week)
            if (!context.WorkSchedules.Any())
            {
                var tech = context.Employees.FirstOrDefault(e => e.FullName == "Trần Kỹ Thuật");
                var shiftS = context.Shifts.FirstOrDefault(s => s.ShiftName == "Ca Sáng");
                if (tech != null && shiftS != null)
                {
                    context.WorkSchedules.Add(new WorkSchedule { EmployeeId = tech.EmployeeId, ShiftId = shiftS.ShiftId, WorkDate = DateTime.Today, IsCheckIn = false });
                    context.WorkSchedules.Add(new WorkSchedule { EmployeeId = tech.EmployeeId, ShiftId = shiftS.ShiftId, WorkDate = DateTime.Today.AddDays(1), IsCheckIn = false });
                    context.SaveChanges();
                }
            }

            // Vouchers
            if (!context.Vouchers.Any())
            {
                context.Vouchers.AddRange(
                    new Voucher { Name = "Chào mừng bạn mới", Code = "WELCOME", Description = "Giảm 50k", DiscountType = "Fixed", DiscountValue = 50000, MinSpend = 200000, UsageLimit = 100, StartDate = DateTime.Today.AddDays(-10), EndDate = DateTime.Today.AddDays(30) },
                    new Voucher { Name = "Mùa hè rực rỡ", Code = "SUMMER", Description = "Giảm 10%", DiscountType = "Percent", DiscountValue = 10, MaxDiscountAmount = 100000, MinSpend = 500000, UsageLimit = 50, StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(15) }
                );
                context.SaveChanges();
            }
        }

        private static void SeedFinanceConfig(ApplicationDbContext context)
        {
            if (!context.TransactionCategories.Any())
            {
                context.TransactionCategories.AddRange(
                    new TransactionCategory { Name = "Doanh thu dịch vụ", IsIncomeCategory = true },
                    new TransactionCategory { Name = "Bán lẻ sản phẩm", IsIncomeCategory = true },
                    new TransactionCategory { Name = "Tiền thuê nhà", IsIncomeCategory = false },
                    new TransactionCategory { Name = "Nhập hàng", IsIncomeCategory = false },
                    new TransactionCategory { Name = "Tiền điện nước", IsIncomeCategory = false }
                );
                context.SaveChanges();
            }

            if (!context.Budgets.Any())
            {
                var electric = context.TransactionCategories.FirstOrDefault(c => c.Name == "Tiền điện nước");
                if (electric != null)
                {
                    context.Budgets.Add(new Budget { Month = DateTime.Now.Month, Year = DateTime.Now.Year, TransactionCategoryId = electric.Id, LimitAmount = 5000000 });
                    context.SaveChanges();
                }
            }
        }

        private static void SeedTransactionalData(ApplicationDbContext context)
        {
            if (!context.Appointments.Any())
            {
                var cust = context.Customers.FirstOrDefault();
                var emp = context.Employees.FirstOrDefault(e => e.FullName.Contains("Kỹ Thuật"));
                var service = context.Services.FirstOrDefault();

                if (cust != null && emp != null && service != null)
                {
                    // 1. Past Completed Appointment
                    var pastAppt = new Appointment
                    {
                        CustomerId = cust.CustomerId,
                        EmployeeId = emp.EmployeeId, // Lead consultant/scheduler
                        StartTime = DateTime.Now.AddDays(-2).AddHours(-4),
                        EndTime = DateTime.Now.AddDays(-2).AddHours(-3),
                        Status = "Completed",
                        CreatedDate = DateTime.Now.AddDays(-3),
                        DepositAmount = 0,
                        IsDepositPaid = true
                    };
                    context.Appointments.Add(pastAppt);
                    context.SaveChanges();

                    var pastDetail = new AppointmentDetail
                    {
                        AppointmentId = pastAppt.AppointmentId,
                        ServiceId = service.ServiceId,
                        TechnicianId = emp.EmployeeId,
                        PriceAtBooking = service.Price,
                        Status = "Completed"
                    };
                    context.AppointmentDetails.Add(pastDetail);
                    context.SaveChanges();

                    var invoice = new Invoice
                    {
                        AppointmentId = pastAppt.AppointmentId,
                        TotalAmount = service.Price,
                        FinalAmount = service.Price,
                        PaymentStatus = "Paid",
                        PaymentMethod = "Cash",
                        CreatedDate = pastAppt.EndTime.Value
                    };
                    context.Invoices.Add(invoice);
                    context.SaveChanges();

                    context.Payments.Add(new Payment { InvoiceId = invoice.InvoiceId, Amount = invoice.FinalAmount, PaymentMethod = "Cash", PaymentDate = invoice.CreatedDate, TransactionType = "Settlement" });
                    
                    // Transaction link
                    var saleCat = context.TransactionCategories.FirstOrDefault(c => c.Name.Contains("dịch vụ"));
                    if (saleCat != null)
                    {
                        context.Transactions.Add(new Transaction { Date = invoice.CreatedDate, IsIncome = true, Amount = invoice.FinalAmount, Description = $"Thu tiền hóa đơn #{invoice.InvoiceId}", TransactionCategoryId = saleCat.Id, CreatedBy = "System" });
                    }
                    
                    // Review
                    context.Reviews.Add(new Review { AppointmentId = pastAppt.AppointmentId, Rating = 5, Comment = "Dịch vụ rất tuyệt vời!", CreatedDate = DateTime.Now.AddDays(-1) });
                    context.SaveChanges();

                    // 2. Future Pending Appointment
                    context.Appointments.Add(new Appointment
                    {
                        CustomerId = cust.CustomerId,
                        StartTime = DateTime.Now.AddDays(1).AddHours(2),
                        EndTime = DateTime.Now.AddDays(1).AddHours(3),
                        Status = "Pending",
                        CreatedDate = DateTime.Now,
                        DepositAmount = 100000,
                        IsDepositPaid = false
                    });
                    context.SaveChanges();
                }
            }
        }

        private static void SeedCMS(ApplicationDbContext context)
        {
            if (!context.PostCategories.Any())
            {
                context.PostCategories.AddRange(
                    new PostCategory { CategoryName = "Tin tức", Slug = "tin-tuc" },
                    new PostCategory { CategoryName = "Kiến thức làm đẹp", Slug = "kien-thuc" },
                    new PostCategory { CategoryName = "Khuyến mãi", Slug = "khuyen-mai" }
                );
                context.SaveChanges();
            }

            if (!context.Posts.Any())
            {
                var catKnow = context.PostCategories.FirstOrDefault(c => c.Slug == "kien-thuc");
                var author = context.Employees.FirstOrDefault();

                if (catKnow != null && author != null)
                {
                    context.Posts.Add(new Post
                    {
                        Title = "5 Cách chăm sóc da mùa hè",
                        Slug = "5-cach-cham-soc-da-mua-he",
                        Summary = "Mùa hè nắng nóng khiến da dễ bị tổn thương...",
                        Content = "<p>Nội dung chi tiết bài viết...</p>",
                        Thumbnail = "/img/blog/summer-skin.jpg",
                        PostCategoryId = catKnow.PostCategoryId,
                        AuthorId = author.EmployeeId,
                        IsPublished = true,
                        PublishedDate = DateTime.Now.AddDays(-5)
                    });
                    context.SaveChanges();
                }
            }
        }

        private static async Task EnsureUserAndRole(UserManager<ApplicationUser> userManager, string email, string password, string role, string fullName, string address)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    Address = address,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        private static async Task<Employee?> EnsureEmployee(ApplicationDbContext context, UserManager<ApplicationUser> userManager, string email, string fullName, decimal salary, string gender)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null) return null;

            var employee = await context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == user.Id);
            if (employee == null)
            {
                employee = new Employee
                {
                    IdentityUserId = user.Id,
                    FullName = fullName,
                    BaseSalary = salary,
                    Address = user.Address ?? "",
                    Gender = gender,
                    IsActive = true
                };
                context.Employees.Add(employee);
                await context.SaveChangesAsync();
            }
            return employee;
        }
    }
}
