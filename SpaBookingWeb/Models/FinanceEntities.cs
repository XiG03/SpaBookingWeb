using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaBookingWeb.Models
{
    // Đã đổi tên thành TransactionCategory để tránh trùng với Category của Product/Service
    [Table("TransactionCategories")]
    public class TransactionCategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Loại danh mục: true = Thu, false = Chi
        public bool IsIncomeCategory { get; set; } 

        public virtual ICollection<Transaction> Transactions { get; set; }
        public virtual ICollection<Budget> Budgets { get; set; }
    }

    // Giao dịch Thu/Chi thực tế
    [Table("Transactions")]
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required]
        public bool IsIncome { get; set; } // true = Thu, false = Chi

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public bool IsDeleted { get; set; } = false;

        public string Description { get; set; }

        [StringLength(50)]
        public string ReferenceCode { get; set; }

        // Cập nhật khóa ngoại trỏ đến TransactionCategory
        public int? TransactionCategoryId { get; set; }
        
        [ForeignKey("TransactionCategoryId")]
        public virtual TransactionCategory TransactionCategory { get; set; }

        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    // Kế hoạch Ngân sách
    [Table("Budgets")]
    public class Budget
    {
        [Key]
        public int Id { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LimitAmount { get; set; }

        // Cập nhật khóa ngoại trỏ đến TransactionCategory
        public int TransactionCategoryId { get; set; }

        [ForeignKey("TransactionCategoryId")]
        public virtual TransactionCategory TransactionCategory { get; set; }
    }
}