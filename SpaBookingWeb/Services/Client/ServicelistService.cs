using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public class ServiceListService : IServiceListService
    {
        private readonly ApplicationDbContext _context;

        public ServiceListService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceListViewModel> GetServiceListAsync(
            string search, 
            int? categoryId, 
            string sortOrder, 
            int page = 1, 
            int pageSize = 12)
        {
            var model = new ServiceListViewModel
            {
                CurrentSearch = search,
                CurrentCategoryId = categoryId,
                SortOrder = sortOrder,
                CurrentPage = page
            };

            // 1. Lấy danh sách Categories (Chỉ lấy loại Service)
            var categories = await _context.Categories
                .Where(c => c.Type == "Service" && !string.IsNullOrEmpty(c.CategoryName))
                .Select(c => new ClientCategoryViewModel
                {
                    Id = c.CategoryId,
                    Name = c.CategoryName,
                    // Dùng ảnh placeholder hoặc map từ DB nếu có cột Image
                    IconUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuChowAKQh8Np34mUy3hNdqX1rQjOMLk9C5Q_vI5b62pqkcuehV6ZeCJTEazmNy7tubwWSfnZ1qKjlQxEiIiAS5v8Yr7PZwT2R0H9LZZWTxg8NG8SYPfCzwI1kK3OxX7MNgBY-WDvjdTgOR3i4FoVwCVvwQWmcFB6RmVMCgAJmnX7VRFqF6GLSGDwch3NUboe7Ytb5V9lVvdhlNsOoLMFGoTeGSs9rINvFN7U09GV4gvVHSaIRxOELgfKoexTZs5Wt6UC2YuYextLL0",
                    IsSelected = c.CategoryId == categoryId
                })
                .ToListAsync();
            model.Categories = categories;

            // 2. Query Dịch vụ
            var servicesQuery = _context.Services
                .Include(s => s.Category)
                .Where(s => s.IsActive && !s.IsDeleted);

            // Lọc theo từ khóa
            if (!string.IsNullOrEmpty(search))
            {
                servicesQuery = servicesQuery.Where(s => s.ServiceName.Contains(search) || s.Description.Contains(search));
            }

            // Lọc theo danh mục
            if (categoryId.HasValue)
            {
                servicesQuery = servicesQuery.Where(s => s.CategoryId == categoryId.Value);
            }

            // Sắp xếp
            switch (sortOrder)
            {
                case "price_asc":
                    servicesQuery = servicesQuery.OrderBy(s => s.Price);
                    break;
                case "price_desc":
                    servicesQuery = servicesQuery.OrderByDescending(s => s.Price);
                    break;
                default: // popular (mặc định lấy mới nhất hoặc theo logic khác)
                    servicesQuery = servicesQuery.OrderByDescending(s => s.ServiceId);
                    break;
            }

            // Phân trang
            model.TotalItems = await servicesQuery.CountAsync();
            model.TotalPages = (int)Math.Ceiling(model.TotalItems / (double)pageSize);

            var services = await servicesQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new ClientServiceItemViewModel
                {
                    Id = s.ServiceId,
                    Name = s.ServiceName,
                    Description = s.Description,
                    CategoryName = s.Category != null ? s.Category.CategoryName : "Dịch vụ",
                    Price = s.Price,
                    DurationMinutes = s.DurationMinutes,
                    ImageUrl = string.IsNullOrEmpty(s.Image) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuBRPqf-JGVzjlnQDY50Mknpw_BGK0hLt7hkBomlBoy2VVMjekMF1MVs4olKseiEfAVCJWp7z-5t2EZbHPBCRJurE4IUgUhiSsVcKiyuQU_VUwtLORxfStzA7JQf8c0i9Xjw6mJLVGbH9dD5iD1Np_Y4_gn6lYKViFtoKkrUkVB7A6Zj4QBBBnlbmaUWKMafzBLCZu2es8JcnjYTEVt1UWZRG9K30EyxQ9cM2vA2E_SoSmpQr0kUgBwStX2iRnuIs09ujjgwNa4fvls" : s.Image,
                    Rating = 5.0, // Hardcode tạm
                    DiscountPercent = 0 // Có thể tính toán nếu có bảng Promotion
                })
                .ToListAsync();
            model.Services = services;

            // 3. Query Combo (Chỉ lấy nếu không filter category hoặc trang đầu tiên)
            if (!categoryId.HasValue && page == 1 && string.IsNullOrEmpty(search))
            {
                var combos = await _context.Combos
                    .Include(c => c.ComboDetails).ThenInclude(cd => cd.Service)
                    .Where(c => !c.IsDeleted)
                    .Take(3) // Lấy 3 combo nổi bật
                    .Select(c => new ClientComboItemViewModel
                    {
                        Id = c.ComboId,
                        Name = c.ComboName,
                        Description = string.Join(" • ", c.ComboDetails.Select(cd => cd.Service.ServiceName)),
                        Price = c.Price,
                        OriginalPrice = c.ComboDetails.Sum(cd => cd.Service.Price),
                        DurationMinutes = c.ComboDetails.Sum(cd => cd.Service.DurationMinutes),
                        ImageUrl = string.IsNullOrEmpty(c.Image) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuBOWoiZ3GQ5WjeKEosSAO4jhVKq4YDyjUKosHqWeOXFxud_ATIyG1fvTOLBYt7m3VTipdp8fBzyscW-F_3tJFiZh18KsSwxVj1EbZXNCa5CHUNq6AFZOv_nzFs-9YlzMnyaPAGyuEouKSR_aTnp4fPso4p-x5leDhjMfK9eO-UAtSZg6fZP_OwlOE33ihdTBL5RtqI79c7M42zugRJzJ4IRVBb58wGZ4op7MruYzcAnNDgdtqQfiXCoNmECcQ2Ht3aE1gjbCfB3vRc" : c.Image,
                        StatusText = "Ưu đãi hot",
                        IsBestSeller = true
                    })
                    .ToListAsync();
                model.Combos = combos;
            }

            return model;
        }
    }
}