using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class RevenueReportViewModel
    {
        // --- CHỈ SỐ THÁNG NAY ---
        public decimal CurrentRevenue { get; set; }
        public int CurrentOrders { get; set; }
        public int ProductsSold { get; set; }
        public decimal AverageOrderValue => CurrentOrders > 0 ? CurrentRevenue / CurrentOrders : 0;

        // --- CHỈ SỐ THÁNG TRƯỚC (ĐỂ SO SÁNH) ---
        public decimal LastMonthRevenue { get; set; }
        public int LastMonthOrders { get; set; }

        public decimal TotalExpenses { get; set; } // Tổng chi phí trong tháng hiện tại

        // --- TÍNH TOÁN TĂNG TRƯỞNG (%) ---
        public double RevenueGrowth
        {
            get
            {
                if (LastMonthRevenue == 0) return 100; // Nếu tháng trước = 0 thì tăng trưởng 100%
                return (double)((CurrentRevenue - LastMonthRevenue) / LastMonthRevenue) * 100;
            }
        }

        public double OrderGrowth
        {
            get
            {
                if (LastMonthOrders == 0) return 100;
                return (double)((CurrentOrders - LastMonthOrders) / (double)LastMonthOrders) * 100;
            }
        }

        // --- DỮ LIỆU CHI TIẾT ---
        public List<DailyStat> DailyStats { get; set; } = new List<DailyStat>();
        public List<TopProductStat> TopProducts { get; set; } = new List<TopProductStat>();

        // --- FILTER & CHART DATA ---
        public string FilterType { get; set; } = "Date"; // Date, Service, Employee, Customer
        public DateTime? FilterFromDate { get; set; }
        public DateTime? FilterToDate { get; set; }

        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartData { get; set; } = new List<decimal>();

        // Breakdown stats
        public List<ServiceRevenueStat> ServiceStats { get; set; } = new List<ServiceRevenueStat>();
        public List<EmployeeRevenueStat> EmployeeStats { get; set; } = new List<EmployeeRevenueStat>();
        public List<CustomerRevenueStat> CustomerStats { get; set; } = new List<CustomerRevenueStat>();
        public List<VoucherRevenueStat> VoucherStats { get; set; } = new List<VoucherRevenueStat>();
        
        // --- TIP EXPENSE ---
        public decimal TotalTipAmount { get; set; }
        public List<TipExpenseStat> TipStats { get; set; } = new List<TipExpenseStat>();
    }

    public class DailyStat
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    public class TopProductStat
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ServiceRevenueStat
    {
        public string ServiceName { get; set; }
        public int UsageCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class EmployeeRevenueStat
    {
        public string EmployeeName { get; set; }
        public int JobCount { get; set; }
        public decimal TotalRevenueGenerated { get; set; }
    }

    public class CustomerRevenueStat
    {
        public string CustomerName { get; set; }
        public int BookingCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class VoucherRevenueStat
    {
        public string VoucherCode { get; set; }
        public string VoucherName { get; set; }
        public int UsageCount { get; set; }
        public decimal TotalOriginalAmount { get; set; } // Tổng tiền trước giảm
        public decimal TotalDiscountAmount { get; set; } // Tổng tiền giảm
        public decimal TotalFinalAmount { get; set; }    // Thực thu
    }

    public class TipExpenseStat
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}