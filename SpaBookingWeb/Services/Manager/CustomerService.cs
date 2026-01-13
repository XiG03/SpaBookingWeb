using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.ViewModels.Manager;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;

namespace SpaBookingWeb.Services.Manager
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        // Định nghĩa tên Role cho Khách hàng
        private const string ROLE_CUSTOMER = "Customer";

        public CustomerService(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<CustomerDashboardViewModel> GetCustomerDashboardDataAsync()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);

            // 1. LẤY DANH SÁCH KHÁCH HÀNG
            // Global Query Filter trong ApplicationDbContext sẽ tự động loại bỏ các User có IsDeleted = true
            var allUsersInRole = await _userManager.GetUsersInRoleAsync(ROLE_CUSTOMER);

            // Lưu ý: Logic cũ dùng LockoutEnd để đánh dấu xóa. 
            // Nếu bạn muốn lọc cả những user bị khóa (banned) vì lý do khác, hãy giữ dòng dưới. 
            // Nếu chỉ quan tâm đến Xóa Mềm (đã được lọc tự động), có thể bỏ qua check LockoutEnd.
            // Ở đây mình vẫn giữ check Lockout để đảm bảo tính tương thích.
            var customersList = allUsersInRole
                .Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.Now)
                .ToList();

            var totalCustomers = customersList.Count;

            // 2. Khách hàng mới trong tháng
            var newCustomersCount = customersList.Count(u => u.CreatedDate >= startOfMonth);

            // 3. Truy vấn bảng Operations (Lịch sử đặt/Giao dịch)
            // Global Query Filter cũng áp dụng cho Operations (ẩn các giao dịch bị xóa mềm)
            var operationsThisMonth = await _context.Operations
                .Where(o => o.CreateDate >= startOfMonth)
                .Include(o => o.User)
                .ToListAsync();

            var operationsLastMonth = await _context.Operations
                .Where(o => o.CreateDate >= startOfLastMonth && o.CreateDate <= endOfLastMonth)
                .ToListAsync();

            // 4. Số lượng lượt ghé (Status = 1: Hoàn thành)
            var customerIds = customersList.Select(c => c.Id).ToHashSet();

            var completedVisits = operationsThisMonth
                .Count(o => o.Status == 1 && customerIds.Contains(o.UserId));

            // 5. Số lượng hủy (Status = -1)
            var cancelledOrders = operationsThisMonth
                .Count(o => o.Status == -1 && customerIds.Contains(o.UserId));

            // 6. Tính tỉ lệ quay lại (Retention Rate)
            double returnRateThisMonth = 0;

            var customerVisitsThisMonth = operationsThisMonth
                .Where(o => o.Status == 1 && customerIds.Contains(o.UserId))
                .GroupBy(o => o.UserId);

            if (customerVisitsThisMonth.Any())
            {
                double returningCustomers = customerVisitsThisMonth.Count(g => g.Count() > 1);
                double totalActiveCustomers = customerVisitsThisMonth.Count();

                if (totalActiveCustomers > 0)
                {
                    returnRateThisMonth = (returningCustomers / totalActiveCustomers) * 100;
                }
            }

            // Tỉ lệ tháng trước
            double returnRateLastMonth = 0;
            var customerVisitsLastMonth = operationsLastMonth
                .Where(o => o.Status == 1 && customerIds.Contains(o.UserId))
                .GroupBy(o => o.UserId);

            if (customerVisitsLastMonth.Any())
            {
                double returningCustomersLast = customerVisitsLastMonth.Count(g => g.Count() > 1);
                double totalActiveCustomersLast = customerVisitsLastMonth.Count();

                if (totalActiveCustomersLast > 0)
                {
                    returnRateLastMonth = (returningCustomersLast / totalActiveCustomersLast) * 100;
                }
            }

            // 7. Tỉ lệ tăng trưởng visits
            double growthRate = 0;
            var visitsLastMonthCount = operationsLastMonth.Count(o => o.Status == 1 && customerIds.Contains(o.UserId));

            if (visitsLastMonthCount > 0)
            {
                growthRate = ((double)(completedVisits - visitsLastMonthCount) / visitsLastMonthCount) * 100;
            }

            // Fallback tên
            foreach (var user in customersList)
            {
                if (string.IsNullOrEmpty(user.FullName)) user.FullName = user.UserName;
            }

            return new CustomerDashboardViewModel
            {
                TotalCustomers = totalCustomers,
                NewCustomersThisMonth = newCustomersCount,
                TotalVisitsThisMonth = completedVisits,
                CancelledOrdersThisMonth = cancelledOrders,
                ReturnRateThisMonth = Math.Round(returnRateThisMonth, 2),
                ReturnRateLastMonth = Math.Round(returnRateLastMonth, 2),
                GrowthRate = Math.Round(growthRate, 2),
                Customers = customersList.OrderByDescending(u => u.CreatedDate).Take(100).ToList()
            };
        }

        public async Task<bool> CreateCustomerAsync(ApplicationUser user, string password)
        {
            if (string.IsNullOrEmpty(user.UserName)) user.UserName = user.Email;
            user.CreatedDate = DateTime.Now;
            user.EmailConfirmed = true; 

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync(ROLE_CUSTOMER))
                {
                    await _roleManager.CreateAsync(new IdentityRole(ROLE_CUSTOMER));
                }

                await _userManager.AddToRoleAsync(user, ROLE_CUSTOMER);
                return true;
            }
            return false;
        }

        public async Task<List<ApplicationUser>> GetAllCustomersAsync()
        {
            // Global Filter của EF Core sẽ tự động lọc bỏ User đã xóa mềm
            var users = await _userManager.GetUsersInRoleAsync(ROLE_CUSTOMER);
            return users.ToList();
        }

        public async Task<ApplicationUser> GetCustomerByIdAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return null;

            var isCustomer = await _userManager.IsInRoleAsync(user, ROLE_CUSTOMER);
            if (!isCustomer) return null;

            return user;
        }

        public async Task<bool> UpdateCustomerAsync(ApplicationUser user)
        {
            var existingUser = await _userManager.FindByIdAsync(user.Id);
            if (existingUser == null) return false;

            if (!await _userManager.IsInRoleAsync(existingUser, ROLE_CUSTOMER)) return false;

            existingUser.FullName = user.FullName;
            existingUser.PhoneNumber = user.PhoneNumber;
            existingUser.Email = user.Email;
            existingUser.Address = user.Address;

            var result = await _userManager.UpdateAsync(existingUser);
            return result.Succeeded;
        }

        public async Task<bool> DeleteCustomerAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return false;

            if (!await _userManager.IsInRoleAsync(user, ROLE_CUSTOMER)) return false;

            // --- THAY ĐỔI QUAN TRỌNG: SỬ DỤNG XÓA MỀM ---
            // Thay vì set Lockout, ta gọi DeleteAsync.
            // ApplicationDbContext đã được cấu hình để chặn lệnh Delete này và chuyển thành Soft Delete (IsDeleted = true)
            // AuditLog sẽ ghi nhận hành động này là "Delete".
            
            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded;
        }
    }
}