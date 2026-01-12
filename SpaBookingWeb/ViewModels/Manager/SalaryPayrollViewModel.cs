using System;
using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class SalaryPayrollViewModel
    {
        public int SalaryId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public double TotalWorkHours { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal Bonus { get; set; }
        public decimal Deduction { get; set; }
        public decimal FinalSalary { get; set; }
        public string Status { get; set; } = "Draft";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}