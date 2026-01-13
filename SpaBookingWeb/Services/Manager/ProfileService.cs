using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ProfileService(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<ProfileViewModel> GetUserProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            
            // Tìm thông tin Employee nếu có (để lấy Avatar, lương...)
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == userId);

            return new ProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Role = roles.FirstOrDefault() ?? "N/A",
                JoinDate = user.CreatedDate,
                Avatar = employee?.Avatar ?? "/img/default-avatar.png" // Fallback avatar
            };
        }

        public async Task UpdateUserProfileAsync(string userId, ProfileViewModel model)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Exception("User not found");

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;

            await _userManager.UpdateAsync(user);

            // Cập nhật bảng Employee nếu có liên kết
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == userId);
            if (employee != null)
            {
                employee.FullName = model.FullName;
                employee.Address = model.Address;
                // employee.Avatar = ... (Xử lý upload ảnh riêng nếu cần)
                _context.Employees.Update(employee);
                await _context.SaveChangesAsync();
            }
        }
    }
}