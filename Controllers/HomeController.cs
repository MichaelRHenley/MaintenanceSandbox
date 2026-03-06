using MaintenanceSandbox.Data;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;



namespace MaintenanceSandbox.Controllers
{
    [ServiceFilter(typeof(RequireTenantFilter))]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;

            var baseQuery = _db.MaintenanceRequests.AsQueryable();

            // Same definition as "Active EMs" on Maintenance/Index
            var active = await baseQuery.CountAsync(r =>
                r.Status == "New" ||
                r.Status == "In Progress" ||
                r.Status == "Waiting on Parts");

            var waiting = await baseQuery.CountAsync(r =>
                r.Status == "Waiting on Parts");

            // ✅ Use ResolvedAt now, not CreatedAt
            var resolvedToday = await baseQuery.CountAsync(r =>
                r.Status == "Resolved" &&
                r.ResolvedAt.HasValue &&
                r.ResolvedAt.Value.Date == today);

            var recent = await baseQuery
                .Include(r => r.Messages)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.ActiveCount = active;
            ViewBag.WaitingOnPartsCount = waiting;
            ViewBag.ResolvedTodayCount = resolvedToday;
            ViewBag.RecentRequests = recent;

            return View();
        }


        public IActionResult Privacy()
        {
            return View();
        }
    }
}
