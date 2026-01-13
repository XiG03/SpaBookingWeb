using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using SpaBookingWeb.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class CashbookController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CashbookController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Manager/Cashbook
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            var start = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = toDate ?? start.AddMonths(1).AddDays(-1);

            // 1. Calculate opening balance
            var preIncome = await _context.Transactions
               .Where(t => t.Date < start && t.IsIncome).SumAsync(t => t.Amount);
            var preExpense = await _context.Transactions
               .Where(t => t.Date < start && !t.IsIncome).SumAsync(t => t.Amount);
            decimal openingBalance = preIncome - preExpense;

            // 2. Get data in period: Include TransactionCategory
            var query = _context.Transactions
               .Include(t => t.TransactionCategory)
               .Where(t => t.Date >= start && t.Date <= end)
               .OrderByDescending(t => t.Date);

            var transactionsList = await query.Select(t => new TransactionViewModel {
                Id = t.Id,
                Date = t.Date,
                Type = t.IsIncome ? "Income" : "Expense",
                Amount = t.Amount,
                // Get category name, handle null if transaction has no category
                CategoryName = t.TransactionCategory != null ? t.TransactionCategory.Name : "Other", 
                Description = t.Description,
                ReferenceCode = t.ReferenceCode
            }).ToListAsync();

            var model = new CashbookIndexViewModel
            {
                FromDate = start,
                ToDate = end,
                OpeningBalance = openingBalance,
                TotalIncome = transactionsList.Where(t => t.Type == "Income").Sum(t => t.Amount),
                TotalExpense = transactionsList.Where(t => t.Type == "Expense").Sum(t => t.Amount),
                Transactions = transactionsList
            };

            return View(model);
        }

        // POST: Manager/Cashbook/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTransactionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var transaction = new Transaction
                {
                    Date = model.Date,
                    IsIncome = model.IsIncome,
                    Amount = model.Amount,
                    // Map CategoryId from ViewModel to TransactionCategoryId of Entity
                    TransactionCategoryId = model.CategoryId > 0 ? model.CategoryId : (int?)null,
                    Description = model.Description,
                    CreatedBy = User.Identity.Name ?? "Admin",
                    CreatedDate = DateTime.Now
                };

                _context.Add(transaction);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Transaction created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }
    }
}