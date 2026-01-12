using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Client
{
    public class ComboDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        
        // Giá và Thời gian
        public decimal Price { get; set; } // Giá Combo
        public decimal OriginalPrice { get; set; } // Tổng giá các dịch vụ lẻ
        public int DurationMinutes { get; set; }
        
        // Tính toán hiển thị
        public decimal SavingAmount => OriginalPrice - Price;
        public int SavingPercent => OriginalPrice > 0 ? (int)Math.Round((OriginalPrice - Price) / OriginalPrice * 100) : 0;

        // Danh sách thành phần
        public List<ComboServiceItem> IncludedServices { get; set; } = new List<ComboServiceItem>();
        public List<ProductConsumableItem> Consumables { get; set; } = new List<ProductConsumableItem>();
        
        // Đánh giá
        public List<ReviewItem> Reviews { get; set; } = new List<ReviewItem>();
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class ComboServiceItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int DurationMinutes { get; set; }
        public string ImageUrl { get; set; }
    }

    public class ProductConsumableItem
    {
        public string ProductName { get; set; }
        public string UnitName { get; set; }
        public string UsageContext { get; set; } // Dùng cho dịch vụ nào (Ví dụ: "Chăm sóc da")
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
    }

    public class ReviewItem
    {
        public string CustomerName { get; set; }
        public string CustomerAvatar { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public string TimeAgo { get; set; } // "2 ngày trước"
    }
}