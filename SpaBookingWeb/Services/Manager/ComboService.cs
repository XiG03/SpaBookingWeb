using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
    public class ComboService : IComboService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ComboService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<ComboDashboardViewModel> GetAllCombosAsync()
        {
            // Lấy Combo kèm theo chi tiết dịch vụ
            // Global Query Filter sẽ tự động loại bỏ các Combo và ComboDetails đã bị xóa mềm
            var combos = await _context.Combos
                .Include(c => c.ComboDetails)
                .ThenInclude(cd => cd.Service) 
                .ToListAsync();

            var comboDtos = combos.Select(c => new ComboStatisticDto
            {
                ComboId = c.ComboId,
                ComboName = c.ComboName,
                Price = c.Price,
                Image = c.Image,
                ServiceCount = c.ComboDetails.Count,
                // Nối tên các dịch vụ thành chuỗi: "Massage, Xông hơi"
                ServiceNames = string.Join(", ", c.ComboDetails.Select(cd => cd.Service?.ServiceName))
            }).ToList();

            return new ComboDashboardViewModel { Combos = comboDtos };
        }

        public async Task<ComboViewModel> GetComboForCreateAsync()
        {
            // Lấy danh sách dịch vụ (Global Filter tự động lọc Active) và kèm theo Consumables
            var services = await _context.Services
                .Include(s => s.ServiceConsumables)
                .ThenInclude(sc => sc.Product)
                .ThenInclude(p => p.Unit)
                .Where(s => s.IsActive).ToListAsync();

            // Build Map
            var map = services.ToDictionary(
                s => s.ServiceId,
                s => s.ServiceConsumables.Select(sc => new ServiceConsumableDto
                {
                    ProductId = sc.ProductId,
                    Quantity = sc.Quantity,
                    ProductName = sc.Product.ProductName,
                    UnitName = sc.Product.Unit?.UnitName
                }).ToList()
            );

            return new ComboViewModel
            {
                AvailableServices = services.Select(s => new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = $"{s.ServiceName} ({s.Price:N0}đ)"
                }),
                ServiceConsumablesMap = map
            };
        }

        public async Task<ComboViewModel?> GetComboForEditAsync(int id)
        {
            var combo = await _context.Combos
                .Include(c => c.ComboDetails)
                .FirstOrDefaultAsync(c => c.ComboId == id);

            if (combo == null) return null;

            var services = await _context.Services
                .Include(s => s.ServiceConsumables)
                .ThenInclude(sc => sc.Product)
                .ThenInclude(p => p.Unit)
                .Where(s => s.IsActive).ToListAsync();

            var map = services.ToDictionary(
                s => s.ServiceId,
                s => s.ServiceConsumables.Select(sc => new ServiceConsumableDto
                {
                    ProductId = sc.ProductId,
                    Quantity = sc.Quantity,
                    ProductName = sc.Product.ProductName,
                    UnitName = sc.Product.Unit?.UnitName
                }).ToList()
            );

            return new ComboViewModel
            {
                ComboId = combo.ComboId,
                ComboName = combo.ComboName,
                Price = combo.Price,
                Description = combo.Description,
                ExistingImage = combo.Image,
                SelectedServiceIds = combo.ComboDetails.Select(cd => cd.ServiceId).ToList(),
                AvailableServices = services.Select(s => new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = $"{s.ServiceName} ({s.Price:N0}đ)"
                }),
                ServiceConsumablesMap = map
            };
        }

        public async Task<Combo?> GetComboByIdAsync(int id)
        {
            return await _context.Combos
                .Include(c => c.ComboDetails)
                .ThenInclude(cd => cd.Service)
                .FirstOrDefaultAsync(c => c.ComboId == id);
        }

        public async Task CreateComboAsync(ComboViewModel model)
        {
            string imagePath = "/images/default-combo.jpg";
            if (model.ImageFile != null) imagePath = await SaveImageAsync(model.ImageFile);

            // 1. Tạo Combo
            var combo = new Combo
            {
                ComboName = model.ComboName,
                Price = model.Price,
                Description = model.Description ?? "",
                Image = imagePath
            };

            _context.Combos.Add(combo);
            await _context.SaveChangesAsync(); // Lưu để lấy ComboId

            // 2. Tạo ComboDetails
            if (model.SelectedServiceIds != null && model.SelectedServiceIds.Any())
            {
                foreach (var serviceId in model.SelectedServiceIds)
                {
                    _context.ComboDetails.Add(new ComboDetail
                    {
                        ComboId = combo.ComboId,
                        ServiceId = serviceId
                    });
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateComboAsync(ComboViewModel model)
        {
            var combo = await _context.Combos.FindAsync(model.ComboId);
            if (combo == null) throw new Exception("Combo không tồn tại");

            if (model.ImageFile != null) combo.Image = await SaveImageAsync(model.ImageFile);

            combo.ComboName = model.ComboName;
            combo.Price = model.Price;
            combo.Description = model.Description ?? "";

            // --- LOGIC CẬP NHẬT THÔNG MINH (SMART MERGE) CHO XÓA MỀM ---
            
            // 1. Lấy TẤT CẢ chi tiết (bao gồm cả đã xóa mềm) để quyết định Khôi phục hay Thêm mới
            var allExistingDetails = await _context.ComboDetails
                .IgnoreQueryFilters() // <--- Quan trọng: Bỏ qua bộ lọc để thấy dòng đã xóa
                .Where(cd => cd.ComboId == model.ComboId)
                .ToListAsync();

            var newServiceIds = model.SelectedServiceIds ?? new List<int>();

            // 2. Xử lý các dòng đã có trong DB
            foreach (var detail in allExistingDetails)
            {
                if (newServiceIds.Contains(detail.ServiceId))
                {
                    // Trường hợp A: Dịch vụ được chọn có trong DB -> Đảm bảo nó Active (Khôi phục nếu cần)
                    // Vì interface ISoftDelete có IsDeleted, ta gán thủ công để chắc chắn
                    // Sử dụng Reflection hoặc ép kiểu nếu model có interface, ở đây gán qua Entry
                    var entry = _context.Entry(detail);
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "IsDeleted"))
                    {
                        entry.CurrentValues["IsDeleted"] = false; // Khôi phục
                    }
                }
                else
                {
                    // Trường hợp B: Dịch vụ không được chọn nữa -> Xóa mềm
                    // Gọi Remove() sẽ kích hoạt logic Soft Delete trong ApplicationDbContext
                    _context.ComboDetails.Remove(detail);
                }
            }

            // 3. Xử lý các dòng chưa từng có trong DB -> Thêm mới hoàn toàn
            var existingServiceIds = allExistingDetails.Select(x => x.ServiceId).ToList();
            var idsToAdd = newServiceIds.Except(existingServiceIds);

            foreach (var id in idsToAdd)
            {
                _context.ComboDetails.Add(new ComboDetail
                {
                    ComboId = combo.ComboId,
                    ServiceId = id
                });
            }

            _context.Combos.Update(combo);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteComboAsync(int id)
        {
            // Lấy Combo bao gồm cả chi tiết để xóa mềm cả con
            var combo = await _context.Combos
                .Include(c => c.ComboDetails)
                .FirstOrDefaultAsync(c => c.ComboId == id);

            if (combo != null)
            {
                // 1. Xóa mềm các chi tiết con trước
                // Khi gọi Remove, ApplicationDbContext sẽ chặn lại và chuyển thành IsDeleted = true
                foreach (var detail in combo.ComboDetails)
                {
                    _context.ComboDetails.Remove(detail);
                }

                // 2. Xóa mềm Combo cha
                _context.Combos.Remove(combo);
                
                await _context.SaveChangesAsync();
            }
        }

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
            string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "combos");
            if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);
            string filePath = Path.Combine(uploadFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            return "/images/combos/" + uniqueFileName;
        }
    }
}