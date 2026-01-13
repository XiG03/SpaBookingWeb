using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    // --- CASHBOOK (SỔ QUỸ) ---

    public class CashbookIndexViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        
        // Thống kê đầu trang
        public decimal OpeningBalance { get; set; } // Tồn đầu kỳ
        public decimal TotalIncome { get; set; }    // Tổng thu trong kỳ
        public decimal TotalExpense { get; set; }   // Tổng chi trong kỳ
        public decimal ClosingBalance => OpeningBalance + TotalIncome - TotalExpense; // Tồn cuối kỳ

        public List<TransactionViewModel> Transactions { get; set; }
    }

    public class TransactionViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; } // "Thu" hoặc "Chi"
        public decimal Amount { get; set; }
        public string CategoryName { get; set; }
        public string Description { get; set; }
        public string ReferenceCode { get; set; } // Mã tham chiếu (Ví dụ: Mã đơn hàng)
    }

    public class CreateTransactionViewModel
    {
        [Required]
        [Display(Name = "Ngày giao dịch")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Loại giao dịch")]
        public bool IsIncome { get; set; } // True = Thu, False = Chi

        [Required]
        [Display(Name = "Số tiền")]
        public decimal Amount { get; set; }

        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [Required]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }
    }

    // --- BUDGET (NGÂN SÁCH) ---

    public class BudgetDashboardViewModel
    {
        public int Month { get; set; }
        public int Year { get; set; }
        
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public double OverallPercentage => TotalBudget > 0 ? (double)(TotalSpent / TotalBudget) * 100 : 0;

        public List<BudgetItemViewModel> BudgetItems { get; set; }
    }

    public class BudgetItemViewModel
    {
        public int Id { get; set; }
        public string CategoryName { get; set; }
        public decimal LimitAmount { get; set; } // Hạn mức ngân sách
        public decimal ActualAmount { get; set; } // Thực tế đã chi
        public decimal RemainingAmount => LimitAmount - ActualAmount;
        
        public double UsagePercentage => LimitAmount > 0 ? (double)(ActualAmount / LimitAmount) * 100 : 0;
        public bool IsOverBudget => ActualAmount > LimitAmount;
    }
}