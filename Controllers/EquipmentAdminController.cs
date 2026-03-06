using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models.MasterData;
using MaintenanceSandbox.Services;
using MaintenanceSandbox.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Controllers
{
    [Authorize(Roles = "Supervisor,MaintenanceAdmin")]
    [ServiceFilter(typeof(RequireTenantFilter))]
    public sealed class EquipmentAdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ITenantProvider _tenantProvider;

        public EquipmentAdminController(AppDbContext db, ITenantProvider tenantProvider)
        {
            _db = db;
            _tenantProvider = tenantProvider;
        }

        // GET: /EquipmentAdmin?workCenterId=123
        [HttpGet]
        public async Task<IActionResult> Index(int? workCenterId)
        {
            var tenantId = _tenantProvider.GetTenantId();

            var workCenters = await _db.WorkCenters.AsNoTracking()
                .OrderBy(w => w.Code)
                .ToListAsync();

            // Default to first WC if none supplied (optional)
            if (!workCenterId.HasValue && workCenters.Count > 0)
                workCenterId = workCenters[0].Id;

            var equipment = new List<Equipment>();
            if (workCenterId.HasValue)
            {
                equipment = await _db.Equipment.AsNoTracking()
                    .Where(e => e.TenantId == tenantId && e.WorkCenterId == workCenterId.Value)
                    .OrderBy(e => e.Code)
                    .ToListAsync();
            }

            ViewBag.WorkCenters = workCenters;
            ViewBag.WorkCenterId = workCenterId;

            return View(equipment); // You’ll create Views/EquipmentAdmin/Index.cshtml
        }

        [HttpGet]
        public IActionResult Create(int workCenterId)
        {
            return View(new EquipmentCreateVm { WorkCenterId = workCenterId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EquipmentCreateVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var tenantId = _tenantProvider.GetTenantId();
            var code = (vm.Code ?? "").Trim().ToUpperInvariant();

            var exists = await _db.Equipment.AnyAsync(e =>
                e.TenantId == tenantId &&
                e.WorkCenterId == vm.WorkCenterId &&
                e.Code == code);

            if (exists)
            {
                ModelState.AddModelError(nameof(vm.Code), "Equipment code already exists for this work center.");
                return View(vm);
            }

            var eq = new Equipment
            {
                TenantId = tenantId,
                WorkCenterId = vm.WorkCenterId,
                Code = code,
                DisplayName = string.IsNullOrWhiteSpace(vm.DisplayName) ? null : vm.DisplayName.Trim(),
                IsActive = true
            };

            _db.Equipment.Add(eq);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { workCenterId = vm.WorkCenterId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id, int? workCenterId)
        {
            var tenantId = _tenantProvider.GetTenantId();

            var eq = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
            if (eq == null) return NotFound();

            eq.IsActive = false;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { workCenterId = workCenterId ?? eq.WorkCenterId });
        }
    }
}
