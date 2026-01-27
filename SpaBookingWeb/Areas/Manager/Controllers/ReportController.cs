using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SpaBookingWeb.Data;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
   [Area("Manager")]
    [Authorize(Roles = "Manager,Admin")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Manager/Report
        public async Task<IActionResult> Index(string filterType = "Date", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var today = DateTime.Now;
            // Default range: Current Month
            var start = fromDate ?? new DateTime(today.Year, today.Month, 1);
            var end = toDate ?? start.AddMonths(1).AddDays(-1);
            
            // Adjust end date to cover end of day
            var queryEnd = end.Date.AddDays(1).AddTicks(-1);

            // 1. Overview KPIs (Current Month vs Last Month) - Keep existing logic mostly, but utilize the filtered range for the "Current" part?
            // User requested "Overview Chart of the Month" -> Filterable.
            // Let's make KPIs reflect the Filtered Range.
            
            // Current Range Data
            var currentTransactions = await _context.Transactions
                .Where(t => t.Date >= start && t.Date <= queryEnd)
                .ToListAsync();

            decimal currentRevenue = currentTransactions.Where(t => t.IsIncome).Sum(t => t.Amount);
            
            // Add Deposit Revenue for Pending/Confirmed
            var depositRevenue = await _context.Appointments
                .Where(a => a.StartTime >= start && a.StartTime <= queryEnd
                            && a.IsDepositPaid 
                            && a.Status != "Completed" 
                            && a.Status != "Cancelled" 
                            && !a.IsDeleted)
                .SumAsync(a => a.DepositAmount);
            currentRevenue += depositRevenue;

            decimal currentExpenses = currentTransactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
            int currentOrders = await _context.Appointments.CountAsync(a => a.StartTime >= start && a.StartTime <= queryEnd && a.Status == "Completed");

            // Compare to Previous Period (Same length)
            var duration = end - start;
            var prevStart = start.AddDays(-(duration.Days + 1));
            var prevEnd = start.AddDays(-1);

            var prevTransactions = await _context.Transactions
                .Where(t => t.Date >= prevStart && t.Date <= prevEnd && t.IsIncome)
                .ToListAsync();
            decimal lastMonthRevenue = prevTransactions.Sum(t => t.Amount);
            int lastMonthOrders = await _context.Appointments.CountAsync(a => a.StartTime >= prevStart && a.StartTime <= prevEnd && a.Status == "Completed");

             var model = new RevenueReportViewModel
            {
                CurrentRevenue = currentRevenue,
                CurrentOrders = currentOrders,
                TotalExpenses = currentExpenses, 
                ProductsSold = 0, 
                LastMonthRevenue = lastMonthRevenue,
                LastMonthOrders = lastMonthOrders,
                FilterType = filterType,
                FilterFromDate = start,
                FilterToDate = end
            };

            // 2. CHART DATA & BREAKDOWN
            
            // Base appointment query for breakdown
            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Where(a => a.StartTime >= start && a.StartTime <= queryEnd && a.Status == "Completed" && !a.IsDeleted) // Breakdown usually based on Completed
                .ToListAsync();

            if (filterType == "Date")
            {
                // Group by Day
                var grouped = appointments
                    .GroupBy(a => a.StartTime.Date)
                    .Select(g => new { 
                        Date = g.Key, 
                        Revenue = g.Sum(x => x.AppointmentDetails.Sum(ad => ad.PriceAtBooking)) 
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                // Fill missing days
                for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                {
                    model.ChartLabels.Add(d.ToString("dd/MM"));
                    var dayStat = grouped.FirstOrDefault(x => x.Date == d);
                    model.ChartData.Add(dayStat?.Revenue ?? 0);
                }
            }
            else if (filterType == "Service")
            {
                // Flatten details
                var details = appointments.SelectMany(a => a.AppointmentDetails).ToList();
                var grouped = details
                    .GroupBy(ad => ad.Service?.ServiceName ?? "Unknown")
                    .Select(g => new ServiceRevenueStat {
                        ServiceName = g.Key,
                        UsageCount = g.Count(),
                        TotalRevenue = g.Sum(x => x.PriceAtBooking)
                    })
                    .OrderByDescending(x => x.TotalRevenue)
                    .ToList();

                model.ServiceStats = grouped;
                model.ChartLabels = grouped.Select(x => x.ServiceName).ToList();
                model.ChartData = grouped.Select(x => x.TotalRevenue).ToList();
            }
            else if (filterType == "Employee")
            {
                // Group by Employee (Main or Technician in details?)
                // Let's use Main Employee for now, or better: separate technician revenue?
                // Simplest: Main Employee
                var grouped = appointments
                    .GroupBy(a => a.Employee?.FullName ?? "Unassigned")
                    .Select(g => new EmployeeRevenueStat {
                        EmployeeName = g.Key,
                        JobCount = g.Count(),
                        TotalRevenueGenerated = g.Sum(x => x.AppointmentDetails.Sum(ad => ad.PriceAtBooking))
                    })
                    .OrderByDescending(x => x.TotalRevenueGenerated)
                    .ToList();

                model.EmployeeStats = grouped;
                model.ChartLabels = grouped.Select(x => x.EmployeeName).ToList();
                model.ChartData = grouped.Select(x => x.TotalRevenueGenerated).ToList();
            }
            else if (filterType == "Customer")
            {
                var grouped = appointments
                    .GroupBy(a => a.Customer?.FullName ?? "Walk-in")
                    .Select(g => new CustomerRevenueStat {
                        CustomerName = g.Key,
                        BookingCount = g.Count(),
                        TotalSpent = g.Sum(x => x.AppointmentDetails.Sum(ad => ad.PriceAtBooking))
                    })
                    .OrderByDescending(x => x.TotalSpent)
                    .Take(10) // Top 10 Customers
                    .ToList();

                model.CustomerStats = grouped;
                model.ChartLabels = grouped.Select(x => x.CustomerName).ToList();
                model.ChartData = grouped.Select(x => x.TotalSpent).ToList();
            }

            // 3. VOUCHER REPORT STATS
            // Logic: Get Invoices with VoucherId not null in range
            // Note: 'currentTransactions' are Transactions, but Voucher info is in 'Invoice'.
            // Need to query Invoices based on CreatedDate ~ Date Range.
            
            var voucherStatsQuery = await _context.Invoices
                .Include(i => i.Voucher)
                .Where(i => i.CreatedDate >= start && i.CreatedDate <= queryEnd 
                            && i.VoucherId != null
                            && !i.IsDeleted) // Only count valid invoices
                .GroupBy(i => new { i.Voucher.Code, i.Voucher.Name })
                .Select(g => new VoucherRevenueStat
                {
                    VoucherCode = g.Key.Code,
                    VoucherName = g.Key.Name,
                    UsageCount = g.Count(),
                    TotalOriginalAmount = g.Sum(x => x.TotalAmount),
                    TotalDiscountAmount = g.Sum(x => x.DiscountAmount),
                    TotalFinalAmount = g.Sum(x => x.FinalAmount)
                })
                .OrderByDescending(x => x.TotalFinalAmount)
                .ToListAsync();

            model.VoucherStats = voucherStatsQuery;

            // 4. TIP EXPENSE REPORT
            // Logic: Find Expense Transactions with "Tip" in Description or Category Name
            var tipTransactions = await _context.Transactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.Date >= start && t.Date <= queryEnd 
                            && !t.IsIncome 
                            && !t.IsDeleted
                            && (t.Description.Contains("tip") || (t.TransactionCategory != null && t.TransactionCategory.Name.Contains("tip")))) // Case-insensitive in SQL usually, but EF Core translate depends on Provider. Typically case-insensitive.
                .OrderByDescending(t => t.Date)
                .Select(t => new TipExpenseStat
                {
                    Date = t.Date,
                    Amount = t.Amount,
                    Description = t.Description
                })
                .ToListAsync();

            model.TipStats = tipTransactions;
            model.TotalTipAmount = tipTransactions.Sum(x => x.Amount);

            return View(model);
        }

        // GET: Xuất báo cáo Excel Full
        [HttpGet]
        public async Task<IActionResult> ExportRevenueReport(DateTime? fromDate, DateTime? toDate)
        {
            var today = DateTime.Now;
            var start = fromDate ?? new DateTime(today.Year, today.Month, 1);
            var end = toDate ?? start.AddMonths(1).AddDays(-1);
            var queryEnd = end.Date.AddDays(1).AddTicks(-1);

            // 1. DATA: Doanh số (Sales) từ Appointments (Completed)
            var appointments = await _context.Appointments
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Where(a => a.StartTime >= start && a.StartTime <= queryEnd 
                            && a.Status == "Completed" 
                            && !a.IsDeleted)
                .ToListAsync();

            // 1.1 Daily Stats
            var dailyStats = appointments
                .GroupBy(a => a.StartTime.Date)
                .Select(g => new { 
                    Date = g.Key, 
                    Revenue = g.Sum(x => x.AppointmentDetails.Sum(ad => ad.PriceAtBooking)),
                    Orders = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // 1.2 Service Stats
            var serviceStats = appointments.SelectMany(a => a.AppointmentDetails)
                .GroupBy(ad => ad.Service?.ServiceName ?? "Other Service")
                .Select(g => new { Name = g.Key, Revenue = g.Sum(x => x.PriceAtBooking), Count = g.Count() })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            // 1.3 Employee Stats (Main Employee)
            var empStats = appointments
                .GroupBy(a => a.Employee?.FullName ?? "Unassigned")
                .Select(g => new { Name = g.Key, Revenue = g.Sum(x => x.AppointmentDetails.Sum(ad => ad.PriceAtBooking)), Count = g.Count() })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            // 1.4 Customer Stats
            var cusStats = appointments
                .GroupBy(a => a.Customer?.FullName ?? "Walk-in Guest")
                .Select(g => new { Name = g.Key, Revenue = g.Sum(x => x.AppointmentDetails.Sum(ad => ad.PriceAtBooking)), Count = g.Count() })
                .OrderByDescending(x => x.Revenue)
                .Take(50) // Top 50
                .ToList();

            // 2. DATA: Voucher Stats
            var voucherStats = await _context.Invoices
                .Include(i => i.Voucher)
                .Where(i => i.CreatedDate >= start && i.CreatedDate <= queryEnd 
                            && i.VoucherId != null
                            && !i.IsDeleted)
                .GroupBy(i => new { i.Voucher.Code, i.Voucher.Name })
                .Select(g => new {
                    Code = g.Key.Code,
                    Name = g.Key.Name,
                    Count = g.Count(),
                    Original = g.Sum(x => x.TotalAmount),
                    Discount = g.Sum(x => x.DiscountAmount),
                    Final = g.Sum(x => x.FinalAmount)
                })
                .OrderByDescending(x => x.Final)
                .ToListAsync();

            // 3. DATA: Tip Expenses
            var tipTransactions = await _context.Transactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.Date >= start && t.Date <= queryEnd 
                            && !t.IsIncome 
                            && !t.IsDeleted
                            && (t.Description.Contains("tip") || (t.TransactionCategory != null && t.TransactionCategory.Name.Contains("tip")))) // Case-insensitive logic varies by DB, but .Contains is standard
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            // 4. GENERATE EXCEL
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                // --- SHEET 1: TỔNG QUAN DOANH THU ---
                var ws1 = package.Workbook.Worksheets.Add("Revenue_Summary");
                
                // Header
                ws1.Cells["A1"].Value = $"REVENUE REPORT ({start:dd/MM/yyyy} - {end:dd/MM/yyyy})";
                ws1.Cells["A1:D1"].Merge = true;
                ws1.Cells["A1"].Style.Font.Size = 14;
                ws1.Cells["A1"].Style.Font.Bold = true;
                ws1.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws1.Cells["A1"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                // Table Header
                ws1.Cells["A3"].Value = "Date";
                ws1.Cells["B3"].Value = "Completed Orders";
                ws1.Cells["C3"].Value = "Revenue (VND)";
                ws1.Cells["A3:C3"].Style.Font.Bold = true;
                ws1.Cells["A3:C3"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws1.Cells["A3:C3"].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);

                int row = 4;
                foreach(var d in dailyStats)
                {
                    ws1.Cells[row, 1].Value = d.Date.ToString("dd/MM/yyyy");
                    ws1.Cells[row, 2].Value = d.Orders;
                    ws1.Cells[row, 3].Value = d.Revenue;
                    ws1.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                    row++;
                }
                
                // Total Row
                ws1.Cells[row, 1].Value = "TOTAL";
                ws1.Cells[row, 2].Formula = $"SUM(B4:B{row-1})";
                ws1.Cells[row, 3].Formula = $"SUM(C4:C{row-1})";
                ws1.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                ws1.Cells[row, 1, row, 3].Style.Font.Bold = true;

                ws1.Cells.AutoFitColumns();

                // --- SHEET 2: PHÂN TÍCH CHI TIẾT ---
                var ws2 = package.Workbook.Worksheets.Add("Detailed_Analysis");

                // Table 1: Service
                ws2.Cells["A1"].Value = "REVENUE BY SERVICE";
                ws2.Cells["A1:C1"].Merge = true;
                ws2.Cells["A1"].Style.Font.Bold = true;
                ws2.Cells["A2"].Value = "Service Name"; ws2.Cells["A2"].Style.Font.Bold = true;
                ws2.Cells["B2"].Value = "Count"; ws2.Cells["B2"].Style.Font.Bold = true;
                ws2.Cells["C2"].Value = "Revenue"; ws2.Cells["C2"].Style.Font.Bold = true;

                int rService = 3;
                foreach(var s in serviceStats)
                {
                    ws2.Cells[rService, 1].Value = s.Name;
                    ws2.Cells[rService, 2].Value = s.Count;
                    ws2.Cells[rService, 3].Value = s.Revenue;
                    ws2.Cells[rService, 3].Style.Numberformat.Format = "#,##0";
                    rService++;
                }

                // Table 2: Employee (Next to it, column E)
                ws2.Cells["E1"].Value = "REVENUE BY EMPLOYEE";
                ws2.Cells["E1:G1"].Merge = true;
                ws2.Cells["E1"].Style.Font.Bold = true;
                ws2.Cells["E2"].Value = "Employee Name"; ws2.Cells["E2"].Style.Font.Bold = true;
                ws2.Cells["F2"].Value = "Orders"; ws2.Cells["F2"].Style.Font.Bold = true;
                ws2.Cells["G2"].Value = "Revenue"; ws2.Cells["G2"].Style.Font.Bold = true;

                int rEmp = 3;
                foreach(var e in empStats)
                {
                    ws2.Cells[rEmp, 5].Value = e.Name;
                    ws2.Cells[rEmp, 6].Value = e.Count;
                    ws2.Cells[rEmp, 7].Value = e.Revenue;
                    ws2.Cells[rEmp, 7].Style.Numberformat.Format = "#,##0";
                    rEmp++;
                }

                // Table 3: Customer (Next to it, column I)
                ws2.Cells["I1"].Value = "TOP CUSTOMERS";
                ws2.Cells["I1:K1"].Merge = true;
                ws2.Cells["I1"].Style.Font.Bold = true;
                ws2.Cells["I2"].Value = "Customer"; ws2.Cells["I2"].Style.Font.Bold = true;
                ws2.Cells["J2"].Value = "Orders"; ws2.Cells["J2"].Style.Font.Bold = true;
                ws2.Cells["K2"].Value = "Spent"; ws2.Cells["K2"].Style.Font.Bold = true;

                int rCus = 3;
                foreach(var c in cusStats)
                {
                    ws2.Cells[rCus, 9].Value = c.Name;
                    ws2.Cells[rCus, 10].Value = c.Count;
                    ws2.Cells[rCus, 11].Value = c.Revenue;
                    ws2.Cells[rCus, 11].Style.Numberformat.Format = "#,##0";
                    rCus++;
                }
                
                ws2.Cells.AutoFitColumns();

                // --- SHEET 3: VOUCHER ---
                var ws3 = package.Workbook.Worksheets.Add("Voucher_Promotion");
                ws3.Cells["A1"].Value = "PROMOTION PROGRAM EFFICIENCY (VOUCHER)";
                ws3.Cells["A1:F1"].Merge = true;
                ws3.Cells["A1"].Style.Font.Bold = true;

                ws3.Cells["A2"].Value = "Voucher Code";
                ws3.Cells["B2"].Value = "Program Name";
                ws3.Cells["C2"].Value = "Usage Count";
                ws3.Cells["D2"].Value = "Original Revenue";
                ws3.Cells["E2"].Value = "Total Discount";
                ws3.Cells["F2"].Value = "Final Revenue";
                ws3.Cells["A2:F2"].Style.Font.Bold = true;
                ws3.Cells["A2:F2"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws3.Cells["A2:F2"].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);

                row = 3;
                foreach(var v in voucherStats)
                {
                    ws3.Cells[row, 1].Value = v.Code;
                    ws3.Cells[row, 2].Value = v.Name;
                    ws3.Cells[row, 3].Value = v.Count;
                    ws3.Cells[row, 4].Value = v.Original;
                    ws3.Cells[row, 4].Style.Numberformat.Format = "#,##0";
                    ws3.Cells[row, 5].Value = v.Discount;
                    ws3.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                    ws3.Cells[row, 5].Style.Font.Color.SetColor(Color.Red);
                    ws3.Cells[row, 6].Value = v.Final;
                    ws3.Cells[row, 6].Style.Numberformat.Format = "#,##0";
                    ws3.Cells[row, 6].Style.Font.Bold = true;
                    row++;
                }
                ws3.Cells.AutoFitColumns();

                // --- SHEET 4: TIP EXPENSE ---
                var ws4 = package.Workbook.Worksheets.Add("Tip_Expenses");
                 ws4.Cells["A1"].Value = "DETAILED TIP EXPENSES";
                ws4.Cells["A1:C1"].Merge = true;
                ws4.Cells["A1"].Style.Font.Bold = true;

                ws4.Cells["A2"].Value = "Date Time";
                ws4.Cells["B2"].Value = "Description / Note";
                ws4.Cells["C2"].Value = "Amount";
                ws4.Cells["A2:C2"].Style.Font.Bold = true;

                row = 3;
                foreach(var t in tipTransactions)
                {
                    ws4.Cells[row, 1].Value = t.Date.ToString("dd/MM/yyyy HH:mm");
                    ws4.Cells[row, 2].Value = t.Description;
                    ws4.Cells[row, 3].Value = t.Amount;
                    ws4.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                    row++;
                }

                ws4.Cells[row, 2].Value = "TOTAL";
                ws4.Cells[row, 3].Formula = $"SUM(C3:C{row-1})";
                ws4.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                ws4.Cells[row, 2, row, 3].Style.Font.Bold = true;
                ws4.Cells.AutoFitColumns();

                // OUTPUT
                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string excelName = $"FullReport_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
        }
    }
}