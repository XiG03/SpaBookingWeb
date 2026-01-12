using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public class ClientHomeService : IClientHomeService
    {
        private readonly ApplicationDbContext _context;

        public ClientHomeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ClientHomeViewModel> GetHomeDataAsync()
        {
            var model = new ClientHomeViewModel();

            // 1. Lấy danh mục dịch vụ
            var categories = await _context.Categories
                .Where(c => c.Type == "Service" && c.Services.Any(s => s.IsActive && !s.IsDeleted))
                .Select(c => new ServiceCategoryViewModel
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    IconUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuChowAKQh8Np34mUy3hNdqX1rQjOMLk9C5Q_vI5b62pqkcuehV6ZeCJTEazmNy7tubwWSfnZ1qKjlQxEiIiAS5v8Yr7PZwT2R0H9LZZWTxg8NG8SYPfCzwI1kK3OxX7MNgBY-WDvjdTgOR3i4FoVwCVvwQWmcFB6RmVMCgAJmnX7VRFqF6GLSGDwch3NUboe7Ytb5V9lVvdhlNsOoLMFGoTeGSs9rINvFN7U09GV4gvVHSaIRxOELgfKoexTZs5Wt6UC2YuYextLL0"
                })
                .Take(6)
                .ToListAsync();
            model.Categories = categories;

            // 2. Lấy Combo nổi bật
            var combos = await _context.Combos
                .Include(c => c.ComboDetails).ThenInclude(cd => cd.Service)
                .Where(c => !c.IsDeleted)
                .Take(3)
                .ToListAsync();

            model.FeaturedCombos = combos.Select(c => new ComboViewModel
            {
                ComboId = c.ComboId,
                ComboName = c.ComboName,
                Description = string.Join(" • ", c.ComboDetails.Select(cd => cd.Service.ServiceName)),
                ImageUrl = string.IsNullOrEmpty(c.Image) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuBOWoiZ3GQ5WjeKEosSAO4jhVKq4YDyjUKosHqWeOXFxud_ATIyG1fvTOLBYt7m3VTipdp8fBzyscW-F_3tJFiZh18KsSwxVj1EbZXNCa5CHUNq6AFZOv_nzFs-9YlzMnyaPAGyuEouKSR_aTnp4fPso4p-x5leDhjMfK9eO-UAtSZg6fZP_OwlOE33ihdTBL5RtqI79c7M42zugRJzJ4IRVBb58wGZ4op7MruYzcAnNDgdtqQfiXCoNmECcQ2Ht3aE1gjbCfB3vRc" : c.Image,
                Price = c.Price,
                OriginalPrice = c.ComboDetails.Sum(cd => cd.Service.Price),
                DurationMinutes = c.ComboDetails.Sum(cd => cd.Service.DurationMinutes),
                IsBestSeller = true,
                StatusText = "Đặt ngay hôm nay"
            }).ToList();

            // 3. Lấy Dịch vụ nổi bật
            var services = await _context.Services
                .Include(s => s.Category)
                .Where(s => s.IsActive && !s.IsDeleted)
                .OrderByDescending(s => s.ServiceId)
                .Take(4)
                .ToListAsync();

            model.FeaturedServices = services.Select(s => new ServiceViewModel
            {
                ServiceId = s.ServiceId,
                ServiceName = s.ServiceName,
                CategoryName = s.Category?.CategoryName ?? "Dịch vụ",
                Price = s.Price,
                ImageUrl = string.IsNullOrEmpty(s.Image) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuBRPqf-JGVzjlnQDY50Mknpw_BGK0hLt7hkBomlBoy2VVMjekMF1MVs4olKseiEfAVCJWp7z-5t2EZbHPBCRJurE4IUgUhiSsVcKiyuQU_VUwtLORxfStzA7JQf8c0i9Xjw6mJLVGbH9dD5iD1Np_Y4_gn6lYKViFtoKkrUkVB7A6Zj4QBBBnlbmaUWKMafzBLCZu2es8JcnjYTEVt1UWZRG9K30EyxQ9cM2vA2E_SoSmpQr0kUgBwStX2iRnuIs09ujjgwNa4fvls" : s.Image,
                Rating = 5.0
            }).ToList();

            // 4. Ưu đãi
            var activeVoucher = await _context.Vouchers
                .Where(v => v.IsActive && !v.IsDeleted && v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now)
                .OrderByDescending(v => v.DiscountValue)
                .FirstOrDefaultAsync();

            if (activeVoucher != null)
            {
                string discountText = activeVoucher.DiscountType == "Percent"
                    ? $"{activeVoucher.DiscountValue:0}%"
                    : $"{activeVoucher.DiscountValue:N0}đ";

                model.CurrentPromotion = new PromotionViewModel
                {
                    Title = activeVoucher.Name,
                    Description = $"Giảm ngay {discountText} {activeVoucher.Description ?? "cho dịch vụ của bạn."}",
                    PromoCode = activeVoucher.Code,
                    BackgroundImage = "https://lh3.googleusercontent.com/aida-public/AB6AXuBpHMYwS4e9oCrd_jAN7pXZzMAepjREtJHjK2qFEnoAiJvgFziruiMQq0jGdDKjlXtUmb4i5Qe7nsP2t46nT30jL2hRYctmxxcGR3bZutDZx7JGqf83aRRxB_NYh6-jkNqZPZi-LutywXPcuAsKQZh8h5gM6BvyZPE1TNEofy04BDfF1ag1se1iXL0lUEZM_Rrft_Rsb1TcxmpK59BcSof8eFb1iXEn26jMOa1-43UusrSFL08STbtIXP2CWCF6Bvle2Zb1V8u_0Rw"
                };
            }
            else
            {
                model.CurrentPromotion = new PromotionViewModel
                {
                    Title = "Rạng rỡ đón hè cùng MySalon",
                    Description = "Đặt lịch ngay để trải nghiệm dịch vụ tốt nhất.",
                    PromoCode = "",
                    BackgroundImage = "https://lh3.googleusercontent.com/aida-public/AB6AXuBpHMYwS4e9oCrd_jAN7pXZzMAepjREtJHjK2qFEnoAiJvgFziruiMQq0jGdDKjlXtUmb4i5Qe7nsP2t46nT30jL2hRYctmxxcGR3bZutDZx7JGqf83aRRxB_NYh6-jkNqZPZi-LutywXPcuAsKQZh8h5gM6BvyZPE1TNEofy04BDfF1ag1se1iXL0lUEZM_Rrft_Rsb1TcxmpK59BcSof8eFb1iXEn26jMOa1-43UusrSFL08STbtIXP2CWCF6Bvle2Zb1V8u_0Rw"
                };
            }

            // 5. [MỚI] Lấy Bài viết mới nhất
            var posts = await _context.Posts
                .Where(p => !p.IsDeleted && p.IsPublished)
                .OrderByDescending(p => p.PublishedDate ?? p.CreatedDate)
                .Take(3)
                .Select(p => new HomePostViewModel
                {
                    Id = p.PostId,
                    Title = p.Title,
                    Summary = p.Summary,
                    Thumbnail = string.IsNullOrEmpty(p.Thumbnail) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuCLHUwV-Z7x3Pyl8s-3qZ77YyV9-k5W7qX9zR1P3uL5mN7vJ9oK4wE8rT6yS2dF1gH0jA4bC3xQ5vM8nL2kP9oJ4hG7fD6sA1wE3rT5yU8iO9pL2kM4nJ6vH0gX3zF5cR8bA9dE7w" : p.Thumbnail,
                    PublishedDateStr = (p.PublishedDate ?? p.CreatedDate).ToString("dd/MM/yyyy")
                })
                .ToListAsync();
            model.LatestPosts = posts;

            // --- 6. Cấu hình hệ thống ---
            var settings = await _context.SystemSettings.ToListAsync();
            var dict = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue);

            // Format giờ đẹp (cắt bỏ giây nếu có) - VD: 09:00:00 -> 09:00
            string FormatTime(string timeStr)
            {
                if (TimeSpan.TryParse(timeStr, out var ts))
                    return ts.ToString(@"hh\:mm");
                return timeStr;
            }

            void ParseWorkingHours(string workingHours, out string openTime, out string closeTime) // chuyen doi workingour sang gio
            {
                openTime = "09:00";   // default
                closeTime = "20:00";  // default

                if (string.IsNullOrWhiteSpace(workingHours))
                    return;

                // VD: "8:00 - 22:00"
                var parts = workingHours.Split('-', StringSplitOptions.TrimEntries);

                if (parts.Length == 2)
                {
                    openTime = FormatTime(parts[0]);
                    closeTime = FormatTime(parts[1]);
                }
            }

            if (dict.ContainsKey("WorkingHours"))
            {
                ParseWorkingHours(dict["WorkingHours"], out var open, out var close);
                model.OpenTime = open;
                model.CloseTime = close;
            }
            else
            {
                model.OpenTime = dict.ContainsKey("OpenTime")
                    ? FormatTime(dict["OpenTime"])
                    : "09:00";

                model.CloseTime = dict.ContainsKey("CloseTime")
                    ? FormatTime(dict["CloseTime"])
                    : "20:00";
            }
            model.FacebookUrl = dict.ContainsKey("FacebookUrl") ? dict["FacebookUrl"] : "#";
            model.SpaName = dict.ContainsKey("SpaName") ? dict["SpaName"] : "MySalon";
            model.Address = dict.ContainsKey("Address") ? dict["Address"] : "Địa chỉ Spa";
            model.Hotline = dict.ContainsKey("PhoneNumber") ? dict["PhoneNumber"] : "Hotline liên hệ";
            model.Email = dict.ContainsKey("Email") ? dict["Email"] : "Email";

            return model;
        }





    }
}