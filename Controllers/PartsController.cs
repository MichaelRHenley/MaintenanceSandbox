using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using MaintenanceSandbox.Models.Parts;
using MaintenanceSandbox.Services.Ai;



namespace MaintenanceSandbox.Controllers;

[Authorize]
[ServiceFilter(typeof(RequireTenantFilter))]
public class PartsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly MaintenanceAiService _ai;

    public PartsController(AppDbContext db, ITenantProvider tenantProvider, MaintenanceAiService ai)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _ai = ai;
    }
    public async Task<IActionResult> Index(string? q = null)
    {
        var query = _db.Parts
            .Include(p => p.InventoryLevels)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(p =>
                p.PartNumber.Contains(q) ||
                (p.ShortDescription != null && p.ShortDescription.Contains(q)) ||
                (p.AiCleanDescription != null && p.AiCleanDescription.Contains(q)));
        }

        var list = await query
            .OrderBy(p => p.PartNumber)
            .ToListAsync();

        ViewBag.SearchText = q;
        return View(list);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new Part { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Part model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // attach tenant
        model.TenantId = _tenantProvider.GetTenantId();

        _db.Parts.Add(model);
        await _db.SaveChangesAsync();

        // 🔹 Let the next page know something was just created
        TempData["CreatedPartId"] = model.Id;
        TempData["CreatedPartNumber"] = model.PartNumber;

        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var part = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id);
        if (part == null)
            return NotFound();

        return View(part);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Part model)
    {
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        // keep tenant id intact
        var existing = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id);
        if (existing == null)
            return NotFound();

        existing.PartNumber = model.PartNumber;
        existing.ShortDescription = model.ShortDescription;
        existing.LongDescription = model.LongDescription;
        existing.Manufacturer = model.Manufacturer;
        existing.ManufacturerPartNumber = model.ManufacturerPartNumber;
        existing.IsActive = model.IsActive;

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        // Get the main part
        var part = await _db.Parts
            .Include(p => p.InventoryLevels)
                .ThenInclude(il => il.LocationBin)
            .Include(p => p.BomItems)
                .ThenInclude(b => b.Asset)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (part == null)
            return NotFound();

        // Simple "similar parts" heuristic (same mfg, similar description)
        IQueryable<Part> simQuery = _db.Parts
            .Where(p => p.Id != part.Id); // exclude current

        if (!string.IsNullOrWhiteSpace(part.Manufacturer))
        {
            simQuery = simQuery.Where(p => p.Manufacturer == part.Manufacturer);
        }

        if (!string.IsNullOrWhiteSpace(part.ShortDescription))
        {
            // crude keyword – first word of the short description
            var firstToken = part.ShortDescription
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstToken))
            {
                simQuery = simQuery.Where(p =>
                    p.ShortDescription != null &&
                    p.ShortDescription.Contains(firstToken));
            }
        }

        var similarParts = await simQuery
            .OrderBy(p => p.PartNumber)
            .Take(5)
            .ToListAsync();

        ViewBag.SimilarParts = similarParts;

        return View(part);
    }


    [HttpGet]
    public async Task<IActionResult> AddInventory(int partId)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var part = await _db.Parts
            .FirstOrDefaultAsync(p => p.Id == partId && p.TenantId == tenantId);

        if (part == null)
            return NotFound();

        var bins = await _db.LocationBins
            .Where(b => b.TenantId == tenantId) // remove if you don't have TenantId here
            .OrderBy(b => b.Site)
            .ThenBy(b => b.Code)
            .Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = $"{b.Site} - {b.Code}"
            })
            .ToListAsync();

        var vm = new AddInventoryViewModel
        {
            PartId = part.Id,
            PartNumber = part.PartNumber,
            LocationOptions = bins
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInventory(AddInventoryViewModel model)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var part = await _db.Parts
            .FirstOrDefaultAsync(p => p.Id == model.PartId && p.TenantId == tenantId);

        if (part == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            // repopulate dropdown on error
            model.LocationOptions = await _db.LocationBins
                .Where(b => b.TenantId == tenantId)
                .OrderBy(b => b.Site).ThenBy(b => b.Code)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"{b.Site} - {b.Code}"
                })
                .ToListAsync();

            return View(model);
        }

        var inv = new InventoryLevel
        {
            PartId = part.Id,
            LocationBinId = model.LocationBinId,
            QuantityOnHand = model.QuantityOnHand,
            ReorderPoint = model.ReorderPoint,
            TargetQuantity = model.TargetQuantity,
            // if InventoryLevel has TenantId, set it here:
            // TenantId = tenantId
        };

        _db.InventoryLevels.Add(inv);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = part.Id });
    }

    [HttpGet]
    public async Task<IActionResult> AddBomItem(int partId)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var part = await _db.Parts
            .FirstOrDefaultAsync(p => p.Id == partId && p.TenantId == tenantId);

        if (part == null)
            return NotFound();

        var assets = await _db.Assets
            .Where(a => a.TenantId == tenantId) // adjust/remove if needed
            .OrderBy(a => a.WorkCenter)
            .ThenBy(a => a.AssetCode)
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.WorkCenter} - {a.AssetCode} - {a.Name}"
            })
            .ToListAsync();

        var vm = new AddBomItemViewModel
        {
            PartId = part.Id,
            PartNumber = part.PartNumber,
            QuantityPerAsset = 1,
            AssetOptions = assets
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBomItem(AddBomItemViewModel model)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var part = await _db.Parts
            .FirstOrDefaultAsync(p => p.Id == model.PartId && p.TenantId == tenantId);

        if (part == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            model.AssetOptions = await _db.Assets
                .Where(a => a.TenantId == tenantId)
                .OrderBy(a => a.WorkCenter).ThenBy(a => a.AssetCode)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.WorkCenter} - {a.AssetCode} - {a.Name}"
                })
                .ToListAsync();

            return View(model);
        }

        var bom = new BomItem
        {
            PartId = part.Id,
            AssetId = model.AssetId,
            QuantityPerAsset = model.QuantityPerAsset,
            // TenantId if you have it:
            // TenantId = tenantId
        };

        _db.BomItems.Add(bom);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = part.Id });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnhanceDescription(int id, CancellationToken ct)
    {
        var part = await _db.Parts.FindAsync(new object[] { id }, ct);
        if (part == null)
            return NotFound();

        var enhanced = await _ai.EnhancePartDescriptionAsync(part, ct);

        part.AiCleanDescription = enhanced;
        await _db.SaveChangesAsync(ct);

        TempData["AiMessage"] = "Description enhanced by AI.";
        return RedirectToAction("Details", new { id });
    }

}
