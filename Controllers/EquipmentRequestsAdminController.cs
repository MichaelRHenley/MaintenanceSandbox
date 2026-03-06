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
    public sealed class EquipmentRequestsAdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ITenantProvider _tenantProvider;

        public EquipmentRequestsAdminController(AppDbContext db, ITenantProvider tenantProvider)
        {
            _db = db;
            _tenantProvider = tenantProvider;
        }

        // GET: /EquipmentRequestsAdmin
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tenantId = _tenantProvider.GetTenantId();

            var pending = await _db.EquipmentRequests.AsNoTracking()
                .Include(r => r.WorkCenter)
                .Where(r => r.TenantId == tenantId && r.Status == "Pending")
                .OrderBy(r => r.CreatedUtc)
                .ToListAsync();

            return View(pending); // Views/EquipmentRequestsAdmin/Index.cshtml
        }

        // POST: /EquipmentRequestsAdmin/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var tenantId = _tenantProvider.GetTenantId();

            var req = await _db.EquipmentRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

            if (req == null) return NotFound();
            if (req.Status != "Pending") return RedirectToAction(nameof(Index));

            var code = (req.RequestedCode ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["err"] = "Request has no equipment code.";
                return RedirectToAction(nameof(Index));
            }

            // Check if equipment already exists for this WC
            var existing = await _db.Equipment
                .FirstOrDefaultAsync(e =>
                    e.TenantId == tenantId &&
                    e.WorkCenterId == req.WorkCenterId &&
                    e.Code == code);

            if (existing == null)
            {
                existing = new Equipment
                {
                    TenantId = tenantId,
                    WorkCenterId = req.WorkCenterId,
                    Code = code,
                    DisplayName = string.IsNullOrWhiteSpace(req.RequestedDisplayName)
                        ? null
                        : req.RequestedDisplayName.Trim(),
                    IsActive = true
                };

                _db.Equipment.Add(existing);
                await _db.SaveChangesAsync();
            }

            req.Status = "Approved";
            req.ReviewedUtc = DateTimeOffset.UtcNow;
            req.CreatedEquipmentId = existing.Id;

            await _db.SaveChangesAsync();

            TempData["ok"] = $"Approved and created/linked equipment: {existing.Code}";
            return RedirectToAction(nameof(Index));
        }

        // POST: /EquipmentRequestsAdmin/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? note)
        {
            var tenantId = _tenantProvider.GetTenantId();

            var req = await _db.EquipmentRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

            if (req == null) return NotFound();
            if (req.Status != "Pending") return RedirectToAction(nameof(Index));

            req.Status = "Rejected";
            req.ReviewedUtc = DateTimeOffset.UtcNow;
            req.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            await _db.SaveChangesAsync();

            TempData["ok"] = "Request rejected.";
            return RedirectToAction(nameof(Index));
        }
    }
}
