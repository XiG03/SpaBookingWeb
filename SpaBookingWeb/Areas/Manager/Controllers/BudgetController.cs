using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class BudgetController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BudgetController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Manager/Budget?month=12&year=2025
        public async Task<IActionResult> Index(int? month, int? year)
        {
            int currentMonth = month ?? DateTime.Now.Month;
            int currentYear = year ?? DateTime.Now.Year;

            // 1. Lấy ngân sách: Include TransactionCategory thay vì Category
            var budgets = await _context.Budgets
               .Include(b => b.TransactionCategory) 
               .Where(b => b.Month == currentMonth && b.Year == currentYear)
               .ToListAsync();

            // 2. Lấy chi phí thực tế: Group theo TransactionCategoryId
            var expenses = await _context.Transactions
               .Where(t => !t.IsIncome && t.Date.Month == currentMonth && t.Date.Year == currentYear && t.TransactionCategoryId != null)
               .GroupBy(t => t.TransactionCategoryId)
               .Select(g => new { TransactionCategoryId = g.Key, TotalSpent = g.Sum(t => t.Amount) })
               .ToListAsync();

            // 3. Join dữ liệu
            var budgetItems = from b in budgets
                              join e in expenses on b.TransactionCategoryId equals e.TransactionCategoryId into gj
                              from subExpense in gj.DefaultIfEmpty()
                              select new BudgetItemViewModel
                              {
                                  Id = b.Id,
                                  CategoryName = b.TransactionCategory.Name, // Lấy tên từ TransactionCategory
                                  LimitAmount = b.LimitAmount,
                                  ActualAmount = subExpense?.TotalSpent ?? 0
                              };
            
            var budgetItemsList = budgetItems.ToList();

            var model = new BudgetDashboardViewModel
            {
                Month = currentMonth,
                Year = currentYear,
                BudgetItems = budgetItemsList,
                TotalBudget = budgetItemsList.Sum(x => x.LimitAmount),
                TotalSpent = budgetItemsList.Sum(x => x.ActualAmount)
            };

            return View(model);
        }
    }
}