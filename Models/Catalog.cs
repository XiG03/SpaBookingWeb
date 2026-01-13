
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaBookingWeb.Models
{
    [Table("Categories")]
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        public string CategoryName { get; set; }

        [Required, StringLength(20)]
        public string Type { get; set; } // Service, Product

        public virtual ICollection<Service> Services { get; set; }
        public virtual ICollection<Product> Products { get; set; }
    }

    [Table("Units")]
    public class Unit
    {
        [Key]
        public int UnitId { get; set; }
        [Required, StringLength(50)]
        public string UnitName { get; set; }
    }

    [Table("Services")]
    
    public class Service
    {
        [Key]
        public int ServiceId { get; set; }

        [Required, StringLength(200)]
        public string ServiceName { get; set; }

        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }

        public string Description { get; set; }
        public string Image { get; set; }
        public bool IsActive { get; set; } = true;
        public bool RequiresDeposit { get; set; }

        public bool IsDeleted { get; set; } = false;

        public virtual ICollection<ServiceConsumable> ServiceConsumables { get; set; }
        public virtual ICollection<ComboDetail> ComboDetails { get; set; }
        public virtual ICollection<TechnicianService> TechnicianServices { get; set; }
    }

    [Table("Products")]
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required, StringLength(200)]
        public string ProductName { get; set; }

        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public int? UnitId { get; set; }
        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        public int StockQuantity { get; set; }
        public bool IsForSale { get; set; } = true;

        public bool IsDeleted { get; set; } = false;
    }

    // Bảng trung gian: Định mức tiêu hao
    [Table("ServiceConsumables")]
    public class ServiceConsumable
    {
        public int ServiceId { get; set; }
        public virtual Service Service { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

    [Table("Combos")]
    public class Combo
    {
        [Key]
        public int ComboId { get; set; }

        [Required, StringLength(200)]
        public string ComboName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string Description { get; set; }
        public string Image { get; set; }

        public bool IsDeleted { get; set; } = false;

        public virtual ICollection<ComboDetail> ComboDetails { get; set; }

    }

    // Bảng trung gian Combo
    [Table("ComboDetails")]
    public class ComboDetail
    {
        public int ComboId { get; set; }
        public virtual Combo Combo { get; set; }

        public int ServiceId { get; set; }
        public virtual Service Service { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

    
    // Cấu hình KTV - Dịch vụ
    [Table("TechnicianServices")]
    public class TechnicianService
    {
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public int ServiceId { get; set; }
        public virtual Service Service { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomPrice { get; set; }

        public int? CustomDuration { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}