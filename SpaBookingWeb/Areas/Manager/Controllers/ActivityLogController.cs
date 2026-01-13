using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class ActivityLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Manager/ActivityLog
        public async Task<IActionResult> Index(string searchString, string actionFilter, DateTime? fromDate, DateTime? toDate)
        {
            var logs = _context.ActivityLogs.AsQueryable();

            // 1. Lọc theo từ khóa: Cập nhật để tìm trong EntityId hoặc NewValues thay vì chỉ Description
            if (!string.IsNullOrEmpty(searchString))
            {
                logs = logs.Where(l => (l.Description != null && l.Description.Contains(searchString)) || 
                                       l.UserId.Contains(searchString) || 
                                       l.EntityName.Contains(searchString) ||
                                       l.EntityId.Contains(searchString));
            }

            // 2. Lọc theo hành động (Create/Update/Delete)
            if (!string.IsNullOrEmpty(actionFilter))
            {
                logs = logs.Where(l => l.Action == actionFilter);
            }

            // 3. Lọc theo ngày
            if (fromDate.HasValue)
            {
                logs = logs.Where(l => l.Timestamp >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                logs = logs.Where(l => l.Timestamp < toDate.Value.AddDays(1));
            }

            var result = await logs.OrderByDescending(l => l.Timestamp).Take(500).ToListAsync();

            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentAction = actionFilter;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(result);
        }
    }
}