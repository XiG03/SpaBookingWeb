using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Client
{
    public class ClientHomeViewModel
    {
        public List<ServiceCategoryViewModel> Categories { get; set; } = new List<ServiceCategoryViewModel>();
        public List<ComboViewModel> FeaturedCombos { get; set; } = new List<ComboViewModel>();
        public List<ServiceViewModel> FeaturedServices { get; set; } = new List<ServiceViewModel>();
        public PromotionViewModel CurrentPromotion { get; set; } // Banner ưu đãi

        public List<HomePostViewModel> LatestPosts { get; set; } = new List<HomePostViewModel>();

        public string OpenTime { get; set; }
        public string CloseTime { get; set; }
        public string FacebookUrl { get; set; }
        public string SpaName { get; set; }
        public string Address { get; set; }
        public string Hotline {get; set;}
        public string Email {get; set;}
    }

    public class ServiceCategoryViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string IconUrl { get; set; } // URL ảnh đại diện cho danh mục
    }

    public class ComboViewModel
    {
        public int ComboId { get; set; }
        public string ComboName { get; set; }
        public string Description { get; set; } // Mô tả ngắn (DS dịch vụ)
        public string ImageUrl { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; } // Giá gốc (tổng các món lẻ)
        public bool IsBestSeller { get; set; }
        public string StatusText { get; set; } // VD: "Còn chỗ hôm nay"
    }

    public class ServiceViewModel
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public double Rating { get; set; } = 5.0; // Mặc định 5 sao nếu chưa có đánh giá
    }

    public class HomePostViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Thumbnail { get; set; }
        public string PublishedDateStr { get; set; }
    }

    public class PromotionViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string PromoCode { get; set; }
        public string BackgroundImage { get; set; }
    }
}