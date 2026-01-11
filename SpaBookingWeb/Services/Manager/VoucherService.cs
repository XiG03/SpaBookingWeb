using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.ViewModels.Manager;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;

namespace SpaBookingWeb.Services.Manager
{
    public class VoucherService : IVoucherService
    {
        private readonly ApplicationDbContext _context;

        public VoucherService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<VoucherDashboardViewModel> GetAllVouchersAsync()
        {
            var vouchers = await _context.Vouchers
                .Where(d => !d.IsDeleted)
                .OrderByDescending(d => d.IsActive).ThenByDescending(d => d.StartDate)
                .ToListAsync();

            var list = vouchers.Select(d => new VoucherViewModel
            {
                VoucherId = d.VoucherId,
                Name = d.Name,
                Code = d.Code.ToUpper(),
                ValueDisplay = d.DiscountType == "Percent" 
                    ? $"{d.DiscountValue:0.#}% (Max {d.MaxDiscountAmount?.ToString("N0") ?? "∞"})" 
                    : $"{d.DiscountValue:N0} đ",
                MinSpendDisplay = $"{d.MinSpend:N0} đ",
                DateRange = $"{d.StartDate:dd/MM} - {d.EndDate:dd/MM/yyyy}",
                UsageStatus = d.UsageLimit > 0 ? $"{d.UsageCount}/{d.UsageLimit}" : $"{d.UsageCount} (∞)",
                IsActive = d.IsActive && d.EndDate >= DateTime.Today && (d.UsageLimit == 0 || d.UsageCount < d.UsageLimit)
            }).ToList();

            return new VoucherDashboardViewModel
            {
                Vouchers = list,
                ActiveCount = list.Count(x => x.IsActive),
                TotalUsed = vouchers.Sum(v => v.UsageCount)
            };
        }

        public async Task<Voucher> GetVoucherByIdAsync(int id)
        {
            return await _context.Vouchers
                .FirstOrDefaultAsync(d => d.VoucherId == id && !d.IsDeleted);
        }

        public async Task CreateVoucherAsync(CreateVoucherViewModel model)
        {
            if (model.StartDate.Date < DateTime.Today)
                throw new ArgumentException("Ngày bắt đầu không được nhỏ hơn ngày hiện tại.");

            if (model.EndDate.Date < model.StartDate.Date)
                throw new ArgumentException("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");

            // Kiểm tra trùng mã (Case-insensitive) - Bao gồm cả mã đã xóa để tránh conflict logic
            var codeToCheck = model.Code.Trim().ToUpper();
            var exists = await _context.Vouchers.AnyAsync(d => d.Code == codeToCheck);
            if (exists) throw new ArgumentException($"Mã voucher '{model.Code}' đã được sử dụng trong hệ thống (bao gồm cả mã cũ). Vui lòng chọn mã khác.");

            var voucher = new Voucher
            {
                Name = model.Name,
                Code = model.Code.ToUpper().Trim(),
                Description = model.Description,
                DiscountType = model.DiscountType,
                DiscountValue = model.DiscountValue,
                MaxDiscountAmount = model.DiscountType == "Percent" ? model.MaxDiscountAmount : null,
                MinSpend = model.MinSpend,
                UsageLimit = model.UsageLimit,
                UsageCount = 0,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                IsActive = model.IsActive,
                IsDeleted = false
            };

            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateVoucherAsync(CreateVoucherViewModel model)
        {
            var voucher = await _context.Vouchers.FindAsync(model.VoucherId);
            if (voucher == null) return;

            if (model.EndDate.Date < model.StartDate.Date)
                throw new ArgumentException("Ngày kết thúc không hợp lệ.");

            // Kiểm tra trùng mã khi update (loại trừ chính nó)
            var codeToCheck = model.Code.Trim().ToUpper();
            var exists = await _context.Vouchers.AnyAsync(d => d.Code == codeToCheck && d.VoucherId != model.VoucherId);
            if (exists) throw new ArgumentException($"Mã voucher '{model.Code}' đã được sử dụng bởi một chiến dịch khác.");

            voucher.Name = model.Name;
            voucher.Code = codeToCheck; // Đảm bảo lưu UpperCase
            voucher.Description = model.Description;
            voucher.DiscountType = model.DiscountType;
            voucher.DiscountValue = model.DiscountValue;
            voucher.MaxDiscountAmount = model.DiscountType == "Percent" ? model.MaxDiscountAmount : null;
            voucher.MinSpend = model.MinSpend;
            voucher.UsageLimit = model.UsageLimit;
            voucher.StartDate = model.StartDate;
            voucher.EndDate = model.EndDate;
            voucher.IsActive = model.IsActive;

            _context.Vouchers.Update(voucher);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteVoucherAsync(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher != null)
            {
                voucher.IsDeleted = true;
                voucher.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}


