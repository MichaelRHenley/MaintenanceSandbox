using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models.MasterData;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Controllers
{
    [Authorize(Roles = "Supervisor,MaintenanceAdmin")]
    [ServiceFilter(typeof(RequireTenantFilter))]
    public sealed class MasterDataAdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ITenantProvider _tenantProvider;

        public MasterDataAdminController(AppDbContext db, ITenantProvider tenantProvider)
        {
            _db = db;
            _tenantProvider = tenantProvider;
        }

        // ── Sites ─────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tenantId = _tenantProvider.GetTenantId();
            var sites = await _db.Sites.AsNoTracking()
                .Include(s => s.Areas)
                .Where(s => s.TenantId == tenantId)
                .OrderBy(s => s.Name)
                .ToListAsync();
            return View(sites);
        }

        [HttpGet]
        public IActionResult CreateSite() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSite(string name)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("name", "Site name is required.");
                return View();
            }

            var tenantId = _tenantProvider.GetTenantId();
            _db.Sites.Add(new Site { TenantId = tenantId, Name = name });
            await _db.SaveChangesAsync();

            TempData["ok"] = $"Site \"{name}\" created.";
            return RedirectToAction(nameof(Index));
        }

        // ── Areas ─────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Areas(int siteId)
        {
            var tenantId = _tenantProvider.GetTenantId();
            var site = await _db.Sites.AsNoTracking()
                .Include(s => s.Areas)
                    .ThenInclude(a => a.WorkCenters)
                .FirstOrDefaultAsync(s => s.Id == siteId && s.TenantId == tenantId);

            if (site == null) return NotFound();

            ViewBag.Site = site;
            return View(site.Areas.OrderBy(a => a.Name).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateArea(int siteId, string name)
        {
            name = (name ?? "").Trim();
            var tenantId = _tenantProvider.GetTenantId();

            var siteExists = await _db.Sites.AnyAsync(s => s.Id == siteId && s.TenantId == tenantId);
            if (!siteExists) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["err"] = "Area name is required.";
                return RedirectToAction(nameof(Areas), new { siteId });
            }

            _db.Areas.Add(new Area { TenantId = tenantId, SiteId = siteId, Name = name });
            await _db.SaveChangesAsync();

            TempData["ok"] = $"Area \"{name}\" created.";
            return RedirectToAction(nameof(Areas), new { siteId });
        }

        // ── Work Centers ──────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> WorkCenters(int areaId)
        {
            var tenantId = _tenantProvider.GetTenantId();
            var area = await _db.Areas.AsNoTracking()
                .Include(a => a.Site)
                .Include(a => a.WorkCenters)
                    .ThenInclude(wc => wc.Equipment)
                .FirstOrDefaultAsync(a => a.Id == areaId && a.TenantId == tenantId);

            if (area == null) return NotFound();

            ViewBag.Area = area;
            return View(area.WorkCenters.OrderBy(wc => wc.Code).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWorkCenter(int areaId, string code, string? displayName)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            var tenantId = _tenantProvider.GetTenantId();

            var area = await _db.Areas.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == areaId && a.TenantId == tenantId);
            if (area == null) return NotFound();

            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["err"] = "Work center code is required.";
                return RedirectToAction(nameof(WorkCenters), new { areaId });
            }

            var exists = await _db.WorkCenters.AnyAsync(w => w.TenantId == tenantId && w.AreaId == areaId && w.Code == code);
            if (exists)
            {
                TempData["err"] = $"Work center \"{code}\" already exists in this area.";
                return RedirectToAction(nameof(WorkCenters), new { areaId });
            }

            _db.WorkCenters.Add(new WorkCenter
            {
                TenantId = tenantId,
                AreaId = areaId,
                Code = code,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim()
            });
            await _db.SaveChangesAsync();

            TempData["ok"] = $"Work center \"{code}\" created.";
            return RedirectToAction(nameof(WorkCenters), new { areaId });
        }
    }
}
