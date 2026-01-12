using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public class ComboListService : IComboListService
    {
        private readonly ApplicationDbContext _context;

        public ComboListService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ComboListViewModel> GetComboListAsync(
            string search, 
            int? categoryId, 
            string sortOrder, 
            int page = 1, 
            int pageSize = 12)
        {
            var model = new ComboListViewModel
            {
                CurrentSearch = search,
                CurrentCategoryId = categoryId,
                SortOrder = sortOrder,
                CurrentPage = page
            };

            // 1. Lấy danh mục để hiển thị bộ lọc
            model.Categories = await _context.Categories
                .Where(c => c.Type == "Service" && !c.Services.All(s => s.IsDeleted)) // Chỉ lấy category có dịch vụ
                .Select(c => new ClientCategoryViewModel
                {
                    Id = c.CategoryId,
                    Name = c.CategoryName,
                    IsSelected = c.CategoryId == categoryId
                })
                .ToListAsync();

            // 2. Query Combos
            var query = _context.Combos
                .Include(c => c.ComboDetails).ThenInclude(cd => cd.Service).ThenInclude(s => s.Category)
                .Where(c => !c.IsDeleted);

            // 3. Lọc theo từ khóa
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.ComboName.Contains(search) || c.Description.Contains(search));
            }

            // 4. Lọc theo danh mục (Combo chứa dịch vụ thuộc danh mục đó)
            if (categoryId.HasValue)
            {
                query = query.Where(c => c.ComboDetails.Any(cd => cd.Service.CategoryId == categoryId));
            }

            // 5. Sắp xếp
            switch (sortOrder)
            {
                case "price_asc":
                    query = query.OrderBy(c => c.Price);
                    break;
                case "price_desc":
                    query = query.OrderByDescending(c => c.Price);
                    break;
                case "new":
                    query = query.OrderByDescending(c => c.ComboId);
                    break;
                default: // popular
                    query = query.OrderBy(c => c.ComboId); // Tạm thời default
                    break;
            }

            // 6. Phân trang & Map dữ liệu
            model.TotalItems = await query.CountAsync();
            model.TotalPages = (int)Math.Ceiling(model.TotalItems / (double)pageSize);

            model.Combos = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ClientComboItemViewModel
                {
                    Id = c.ComboId,
                    Name = c.ComboName,
                    // Tạo mô tả từ danh sách dịch vụ nếu mô tả trống
                    Description = !string.IsNullOrEmpty(c.Description) ? c.Description : string.Join(" + ", c.ComboDetails.Select(cd => cd.Service.ServiceName)),
                    Price = c.Price,
                    // Tính giá gốc từ tổng giá dịch vụ con
                    OriginalPrice = c.ComboDetails.Sum(cd => cd.Service.Price),
                    DurationMinutes = c.ComboDetails.Sum(cd => cd.Service.DurationMinutes),
                    ImageUrl = string.IsNullOrEmpty(c.Image) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuBOWoiZ3GQ5WjeKEosSAO4jhVKq4YDyjUKosHqWeOXFxud_ATIyG1fvTOLBYt7m3VTipdp8fBzyscW-F_3tJFiZh18KsSwxVj1EbZXNCa5CHUNq6AFZOv_nzFs-9YlzMnyaPAGyuEouKSR_aTnp4fPso4p-x5leDhjMfK9eO-UAtSZg6fZP_OwlOE33ihdTBL5RtqI79c7M42zugRJzJ4IRVBb58wGZ4op7MruYzcAnNDgdtqQfiXCoNmECcQ2Ht3aE1gjbCfB3vRc" : c.Image,
                    // Logic gắn nhãn (Ví dụ: Giảm giá > 20% là Hot Deal)
                    StatusText = (c.ComboDetails.Sum(cd => cd.Service.Price) - c.Price) / c.ComboDetails.Sum(cd => cd.Service.Price) > 0.2m ? "Tiết kiệm lớn" : "Phổ biến"
                })
                .ToListAsync();

            return model;
        }

        public async Task<ComboDetailViewModel> GetComboDetailAsync(int comboId)
        {
            var combo = await _context.Combos
                .Include(c => c.ComboDetails)
                    .ThenInclude(cd => cd.Service)
                        .ThenInclude(s => s.ServiceConsumables) // Include định mức tiêu hao
                            .ThenInclude(sc => sc.Product)
                                .ThenInclude(p => p.Unit)
                .Include(c => c.ComboDetails)
                    .ThenInclude(cd => cd.Service)
                        .ThenInclude(s => s.Category)
                .FirstOrDefaultAsync(c => c.ComboId == comboId && !c.IsDeleted);

            if (combo == null) return null;

            var model = new ComboDetailViewModel
            {
                Id = combo.ComboId,
                Name = combo.ComboName,
                Description = combo.Description,
                Price = combo.Price,
                ImageUrl = string.IsNullOrEmpty(combo.Image) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuBOWoiZ3GQ5WjeKEosSAO4jhVKq4YDyjUKosHqWeOXFxud_ATIyG1fvTOLBYt7m3VTipdp8fBzyscW-F_3tJFiZh18KsSwxVj1EbZXNCa5CHUNq6AFZOv_nzFs-9YlzMnyaPAGyuEouKSR_aTnp4fPso4p-x5leDhjMfK9eO-UAtSZg6fZP_OwlOE33ihdTBL5RtqI79c7M42zugRJzJ4IRVBb58wGZ4op7MruYzcAnNDgdtqQfiXCoNmECcQ2Ht3aE1gjbCfB3vRc" : combo.Image
            };

            // 1. Tính toán Dịch vụ con & Tổng giá gốc
            decimal originalPrice = 0;
            int totalDuration = 0;

            foreach (var detail in combo.ComboDetails.Where(cd => !cd.IsDeleted))
            {
                var service = detail.Service;
                originalPrice += service.Price;
                totalDuration += service.DurationMinutes;

                model.IncludedServices.Add(new ComboServiceItem
                {
                    Id = service.ServiceId,
                    Name = service.ServiceName,
                    Description = service.Description ?? $"{service.DurationMinutes} phút • Liệu trình tiêu chuẩn",
                    DurationMinutes = service.DurationMinutes,
                    ImageUrl = string.IsNullOrEmpty(service.Image) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuClZ50FHPxm-eLF4YYnQ6KjwwINbe6eNbntTCx2Gxu5KZg9XdM4tLHpTit2qkpyZMtiRbW0Cu9FOgGwygBnXQQHShpMAPW6Amt_OizoV99J7k-Fu51gQQfWaOKe5C9x3y1yrOBBRk5oEKlKUz5gBQburOfMrswY7CS4rXPEWjyn4g-G8Im20N8uvbL-Qq2m7ABi_VZ0ObK4AgD-QpoBsbXKNvTsUQNlH3k2KUZ5ga2CQ8ulfFY1OWsrd6Zqy7vGlS0yMKpEVJIFsiM" : service.Image
                });

                // 2. Tổng hợp Nguyên liệu tiêu hao (Consumables)
                if (service.ServiceConsumables != null)
                {
                    foreach (var sc in service.ServiceConsumables.Where(x => !x.IsDeleted))
                    {
                        var existing = model.Consumables.FirstOrDefault(c => c.ProductName == sc.Product.ProductName);
                        if (existing != null)
                        {
                            existing.Quantity += sc.Quantity; // Cộng dồn nếu trùng sản phẩm
                        }
                        else
                        {
                            model.Consumables.Add(new ProductConsumableItem
                            {
                                ProductName = sc.Product.ProductName,
                                UnitName = sc.Product.Unit?.UnitName ?? "đơn vị",
                                Quantity = sc.Quantity,
                                UsageContext = service.Category?.CategoryName ?? "Dịch vụ",
                                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuAPYzC1614Sa5JBWpUZAYcZ1AbECiEMTEC0l-hieHvZ5L5-W6WeuBXHfD68HXxRxo-Mik6hXQMRZQkM9-gAUiWfAPwQuB8cGQJhmJVgQR_Wz81JCWcZqBBaiIWaGpBCJSt6q8-4IJUpEn0YpsbesGfuY1OWSyluxA5kSYro8eroOhRShEZOhQM4nLdROdQCJ1M7vqLlcPP5kIAqUM9GzrE8C7AyEO9Rnlzv-ULeG0ufFQ4HBNpPN4SjHmFCm_jc0NgezguzmdF4k-o" // Placeholder
                            });
                        }
                    }
                }
            }

            model.OriginalPrice = originalPrice;
            model.DurationMinutes = totalDuration;

            // 3. Lấy Đánh giá (Tìm các Appointment có chứa Combo này)
            // Logic: Appointment -> AppointmentDetail -> ComboId
            var reviews = await _context.Reviews
                .Include(r => r.Appointment)
                    .ThenInclude(a => a.Customer)
                .Include(r => r.Appointment)
                    .ThenInclude(a => a.AppointmentDetails)
                .Where(r => !r.IsDeleted && r.Appointment.AppointmentDetails.Any(ad => ad.ComboId == comboId))
                .OrderByDescending(r => r.CreatedDate)
                .Take(5)
                .Select(r => new ReviewItem
                {
                    CustomerName = r.Appointment.Customer.FullName,
                    CustomerAvatar = "", // Có thể random màu hoặc lấy avatar thật
                    Rating = r.Rating,
                    Comment = r.Comment,
                    TimeAgo = r.CreatedDate.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            model.Reviews = reviews;
            
            // Tính rating trung bình (nếu chưa có thì fake nhẹ cho đẹp hoặc để 0)
            if (reviews.Any())
            {
                model.AverageRating = reviews.Average(r => r.Rating);
                model.TotalReviews = await _context.Reviews.CountAsync(r => !r.IsDeleted && r.Appointment.AppointmentDetails.Any(ad => ad.ComboId == comboId));
            }
            else
            {
                model.AverageRating = 5.0; // Mặc định 5 sao cho sản phẩm mới
                model.TotalReviews = 0;
            }

            return model;
        }
    }
}