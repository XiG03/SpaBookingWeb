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

        // Define Role name for Customer
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

            // 1. GET CUSTOMER LIST
            // Global Query Filter in ApplicationDbContext will automatically exclude users with IsDeleted = true
            var allUsersInRole = await _userManager.GetUsersInRoleAsync(ROLE_CUSTOMER);

            // Note: Old logic used LockoutEnd to mark delete. 
            // If you want to filter banned users too, keep the line below. 
            // If only care about Soft Delete (automatically filtered), can skip LockoutEnd check.
            // Here I keep Lockout check to ensure compatibility.
            var customersList = allUsersInRole
                .Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.Now)
                .ToList();

            var totalCustomers = customersList.Count;

            // 2. New customers in month
            var newCustomersCount = customersList.Count(u => u.CreatedDate >= startOfMonth);

            // 3. Query Operations table (Booking/Transaction History)
            // Global Query Filter also applies to Operations (hide soft deleted transactions)
            var operationsThisMonth = await _context.Operations
                .Where(o => o.CreateDate >= startOfMonth)
                .Include(o => o.User)
                .ToListAsync();

            var operationsLastMonth = await _context.Operations
                .Where(o => o.CreateDate >= startOfLastMonth && o.CreateDate <= endOfLastMonth)
                .ToListAsync();

            // 4. Visit count (Status = 1: Completed)
            var customerIds = customersList.Select(c => c.Id).ToHashSet();

            var completedVisits = operationsThisMonth
                .Count(o => o.Status == 1 && customerIds.Contains(o.UserId));

            // 5. Cancel count (Status = -1)
            var cancelledOrders = operationsThisMonth
                .Count(o => o.Status == -1 && customerIds.Contains(o.UserId));

            // 6. Calculate Retention Rate
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

            // Rate last month
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

            // 7. Visit growth rate
            double growthRate = 0;
            var visitsLastMonthCount = operationsLastMonth.Count(o => o.Status == 1 && customerIds.Contains(o.UserId));

            if (visitsLastMonthCount > 0)
            {
                growthRate = ((double)(completedVisits - visitsLastMonthCount) / visitsLastMonthCount) * 100;
            }

            // Name fallback
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

                // --- Sync to Custom Customer Table ---
                var customerEntity = new Customer
                {
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    Email = user.Email,
                    Appointments = new List<Appointment>(), // Initialize if needed
                };
                
                // Note: We don't have explicit link ID here unless we add AspNetUserId to Customers table
                // Assuming Name/Phone or Manual correlation for now based on typical quick requests,
                // BUT best practice is adding ForeignKey. 
                // However, without changing DB structure request, we just save it as requested.
                
                _context.Customers.Add(customerEntity);
                await _context.SaveChangesAsync();
                // -------------------------------------

                return true;
            }
            return false;
        }

        public async Task<List<ApplicationUser>> GetAllCustomersAsync()
        {
            // Global Filter of EF Core will automatically filter out soft deleted Users
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

            // --- IMPORTANT CHANGE: USE SOFT DELETE ---
            // Instead of setting Lockout, call DeleteAsync.
            // ApplicationDbContext is configured to intercept this Delete command and convert to Soft Delete (IsDeleted = true)
            // AuditLog will record this action as "Delete".
            
            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded;
        }
        public async Task SyncCustomerAsync(string fullName, string phoneNumber, string email)
        {
            // Check if customer already exists in custom table (by Phone or Email)
            // Ideally we should use Identity UserId as Foreign Key, bu based on current schema we match by Phone/Email
            var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber || c.Email == email);
            
            if (existingCustomer == null)
            {
                var newCustomer = new Customer
                {
                    FullName = fullName,
                    PhoneNumber = phoneNumber,
                    Email = email,
                    // Default values
                    Appointments = new List<Appointment>(),
                    IsDeleted = false
                };
                _context.Customers.Add(newCustomer);
                await _context.SaveChangesAsync();
            }
        }
    }
}