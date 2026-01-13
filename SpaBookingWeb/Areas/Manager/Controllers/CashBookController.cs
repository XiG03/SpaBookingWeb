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

            // 1. Tính tồn đầu kỳ
            var preIncome = await _context.Transactions
               .Where(t => t.Date < start && t.IsIncome).SumAsync(t => t.Amount);
            var preExpense = await _context.Transactions
               .Where(t => t.Date < start && !t.IsIncome).SumAsync(t => t.Amount);
            decimal openingBalance = preIncome - preExpense;

            // 2. Lấy dữ liệu trong kỳ: Include TransactionCategory
            var query = _context.Transactions
               .Include(t => t.TransactionCategory)
               .Where(t => t.Date >= start && t.Date <= end)
               .OrderByDescending(t => t.Date);

            var transactionsList = await query.Select(t => new TransactionViewModel {
                Id = t.Id,
                Date = t.Date,
                Type = t.IsIncome ? "Thu" : "Chi",
                Amount = t.Amount,
                // Lấy tên danh mục, xử lý null nếu giao dịch không có danh mục
                CategoryName = t.TransactionCategory != null ? t.TransactionCategory.Name : "Khác", 
                Description = t.Description,
                ReferenceCode = t.ReferenceCode
            }).ToListAsync();

            var model = new CashbookIndexViewModel
            {
                FromDate = start,
                ToDate = end,
                OpeningBalance = openingBalance,
                TotalIncome = transactionsList.Where(t => t.Type == "Thu").Sum(t => t.Amount),
                TotalExpense = transactionsList.Where(t => t.Type == "Chi").Sum(t => t.Amount),
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
                    // Map CategoryId từ ViewModel sang TransactionCategoryId của Entity
                    TransactionCategoryId = model.CategoryId > 0 ? model.CategoryId : (int?)null,
                    Description = model.Description,
                    CreatedBy = User.Identity.Name ?? "Admin",
                    CreatedDate = DateTime.Now
                };

                _context.Add(transaction);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Đã tạo phiếu giao dịch thành công!";
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }
    }
}