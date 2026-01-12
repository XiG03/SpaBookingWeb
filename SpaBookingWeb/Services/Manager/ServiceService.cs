using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Manager
{
    public class ServiceService : IServiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ServiceService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ... Các hàm cũ (GetServiceDashboardAsync, GetServiceForEditAsync, Create, Update) giữ nguyên ...
        // Tôi sẽ rút gọn các hàm cũ để tập trung vào hàm mới thêm vào

        public async Task<ServiceDashboardViewModel> GetServiceDashboardAsync()
        {
            var services = await _context.Services.ToListAsync();
            // Lấy tất cả chi tiết đơn hàng mà (Đã hoàn thành HOẶC Đã đặt cọc)
            var relevantDetails = await _context.AppointmentDetails
                .Include(ad => ad.Appointment)
                .Where(ad => ad.ServiceId != null && !ad.IsDeleted
                             && (ad.Appointment.Status == "Completed" || ad.Appointment.IsDepositPaid))
                .ToListAsync();

            var stats = new List<ServiceStatisticDto>();
            foreach (var service in services)
            {
                var detailsForService = relevantDetails.Where(x => x.ServiceId == service.ServiceId).ToList();

                // Logic:
                // 1. Lượt sử dụng: Chỉ tính những đơn đã Complete
                var usageCount = detailsForService.Count(x => x.Appointment.Status == "Completed");

                // 2. Doanh thu: Tính tổng giá trị booking của (Completed + Đã cọc)
                var revenue = detailsForService.Sum(x => x.PriceAtBooking);

                stats.Add(new ServiceStatisticDto
                {
                    ServiceId = service.ServiceId,
                    ServiceName = service.ServiceName,
                    Price = service.Price,
                    IsActive = service.IsActive,
                    UsageCount = usageCount,
                    TotalRevenue = revenue
                });
            }

            return new ServiceDashboardViewModel
            {
                Services = stats.OrderByDescending(x => x.TotalRevenue).ToList(),
                TotalServices = services.Count,
                TotalActiveServices = services.Count(x => x.IsActive),
                ChartLabels = stats.OrderByDescending(x => x.TotalRevenue).Take(10).Select(x => x.ServiceName).ToList(),
                ChartUsageCount = stats.OrderByDescending(x => x.TotalRevenue).Take(10).Select(x => x.UsageCount).ToList(),
                ChartRevenue = stats.OrderByDescending(x => x.TotalRevenue).Take(10).Select(x => x.TotalRevenue).ToList()
            };
        }

        public async Task<ServiceViewModel?> GetServiceForEditAsync(int id)
        {
            var service = await _context.Services
                .Include(s => s.ServiceConsumables).ThenInclude(sc => sc.Product).ThenInclude(p => p.Unit)
                .FirstOrDefaultAsync(s => s.ServiceId == id);

            if (service == null) return null;

            var model = new ServiceViewModel
            {
                ServiceId = service.ServiceId,
                ServiceName = service.ServiceName,
                Price = service.Price,
                DurationMinutes = service.DurationMinutes,
                Description = service.Description,
                ExistingImage = service.Image,
                IsActive = service.IsActive,
                RequiresDeposit = service.RequiresDeposit,
                Consumables = service.ServiceConsumables.Where(sc => !sc.IsDeleted).Select(sc => new ServiceConsumableDto
                {
                    ProductId = sc.ProductId,
                    Quantity = sc.Quantity,
                    ProductName = sc.Product.ProductName,
                    UnitName = sc.Product.Unit?.UnitName
                }).ToList()
            };

            // Load danh sách sản phẩm để chọn
            model.AvailableProducts = await _context.Products
                .Include(p => p.Unit)
                .Where(p => !p.IsDeleted && p.IsForSale)
                .ToListAsync();

            return model;
        }

        public async Task<ServiceViewModel> GetServiceForCreateAsync()
        {
            return new ServiceViewModel
            {
                AvailableProducts = await _context.Products
                    .Include(p => p.Unit)
                    .Where(p => !p.IsDeleted)
                    .ToListAsync()
            };
        }

        // --- HÀM MỚI ---
        public async Task<Service?> GetServiceByIdAsync(int id)
        {
            return await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id);
        }
        // ----------------

        public async Task CreateServiceAsync(ServiceViewModel model)
        {
            string imagePath = "/images/default-service.jpg";
            if (model.ImageFile != null) imagePath = await SaveImageAsync(model.ImageFile);

            var service = new Service
            {
                ServiceName = model.ServiceName,
                Price = model.Price,
                DurationMinutes = model.DurationMinutes,
                Description = model.Description ?? "",
                Image = imagePath,
                IsActive = model.IsActive,
                RequiresDeposit = model.RequiresDeposit
            };
            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            // Lưu định mức tiêu hao
            if (model.Consumables != null && model.Consumables.Any())
            {
                foreach (var item in model.Consumables)
                {
                    if (item.Quantity > 0)
                    {
                        var consumable = new ServiceConsumable
                        {
                            ServiceId = service.ServiceId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity
                        };
                        _context.ServiceConsumables.Add(consumable);
                    }
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateServiceAsync(ServiceViewModel model)
        {
            var service = await _context.Services.FindAsync(model.ServiceId);
            if (service == null) throw new Exception("Dịch vụ không tồn tại");

            if (model.ImageFile != null) service.Image = await SaveImageAsync(model.ImageFile);

            service.ServiceName = model.ServiceName;
            service.Price = model.Price;
            service.DurationMinutes = model.DurationMinutes;
            service.Description = model.Description ?? "";
            service.IsActive = model.IsActive;
            service.RequiresDeposit = model.RequiresDeposit;

            // Cập nhật ServiceConsumables: Xóa cũ (Hard delete vì đây là bảng cấu hình)
            var existingConsumables = await _context.ServiceConsumables.Where(sc => sc.ServiceId == model.ServiceId).ToListAsync();
            _context.ServiceConsumables.RemoveRange(existingConsumables);

            if (model.Consumables != null && model.Consumables.Any())
            {
                foreach (var item in model.Consumables)
                {
                    if (item.Quantity > 0)
                    {
                        _context.ServiceConsumables.Add(new ServiceConsumable
                        {
                            ServiceId = service.ServiceId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity
                        });
                    }
                }
            }

            _context.Services.Update(service);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteServiceAsync(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                service.IsActive = false;
                _context.Services.Update(service);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
            string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
            if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);
            string filePath = Path.Combine(uploadFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            return "/images/services/" + uniqueFileName;
        }
    }
}