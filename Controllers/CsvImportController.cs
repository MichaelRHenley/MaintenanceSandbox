using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Services; // for ITenantProvider
using MaintenanceSandbox.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace MaintenanceSandbox.Controllers;

[Authorize]
[ServiceFilter(typeof(RequireTenantFilter))]
public class CsvImportController : Controller
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public CsvImportController(AppDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string importType, bool dryRun, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please select a CSV file.");
            return View("Index");
        }

        if (string.IsNullOrWhiteSpace(importType))
        {
            ModelState.AddModelError(string.Empty, "Please select an import type.");
            return View("Index");
        }

        var result = new CsvImportResultViewModel
        {
            ImportType = importType,
            IsDryRun = dryRun
        };

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        string? headerLine = await reader.ReadLineAsync();
        if (headerLine == null)
        {
            result.Errors.Add("File is empty.");
            return View("Result", result);
        }

        var tenantId = _tenantProvider.GetTenantId();

        // Normalize type
        importType = importType.Trim().ToLowerInvariant();

        int rowNumber = 1; // header = row 1
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            result.TotalRows++;

            try
            {
                var columns = SplitCsvLine(line);

                switch (importType)
                {
                    case "parts":
                        await ImportPartRow(columns, tenantId, dryRun, result);
                        break;

                    case "inventory":
                        await ImportInventoryRow(columns, tenantId, dryRun, result);
                        break;

                    case "bom":
                        await ImportBomRow(columns, tenantId, dryRun, result);
                        break;

                    default:
                        result.Errors.Add($"Unknown import type: {importType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Row {rowNumber}: {ex.Message}");
            }
        }

        if (!dryRun)
        {
            await _db.SaveChangesAsync();
        }

        return View("Result", result);
    }

    // ------------------------------------------------------
    // Helpers
    // ------------------------------------------------------

    // Simple CSV splitter (no quoted-field complexity for v1)
    private static string[] SplitCsvLine(string line)
    {
        return line.Split(',', StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Parts CSV format (header expected):
    /// PartNumber,ShortDescription,LongDescription,Manufacturer,ManufacturerPartNumber,IsActive
    /// </summary>
    private async Task ImportPartRow(string[] cols, Guid tenantId, bool dryRun, CsvImportResultViewModel result)
    {
        if (cols.Length < 1)
        {
            result.Errors.Add("Parts: row has no columns.");
            return;
        }

        var partNumber = cols[0];
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            result.Errors.Add("Parts: PartNumber is required.");
            return;
        }

        string? shortDesc = cols.Length > 1 ? cols[1] : null;
        string? longDesc = cols.Length > 2 ? cols[2] : null;
        string? manufacturer = cols.Length > 3 ? cols[3] : null;
        string? mfgPart = cols.Length > 4 ? cols[4] : null;
        bool isActive = true;

        if (cols.Length > 5 && bool.TryParse(cols[5], out var parsedActive))
        {
            isActive = parsedActive;
        }

        var existing = await _db.Parts
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PartNumber == partNumber);

        if (existing == null)
        {
            if (!dryRun)
            {
                var part = new Part
                {
                    TenantId = tenantId,
                    PartNumber = partNumber,
                    ShortDescription = shortDesc,
                    LongDescription = longDesc,
                    Manufacturer = manufacturer,
                    ManufacturerPartNumber = mfgPart,
                    IsActive = isActive
                };

                _db.Parts.Add(part);
            }

            result.Inserted++;
        }
        else
        {
            if (!dryRun)
            {
                existing.ShortDescription = shortDesc;
                existing.LongDescription = longDesc;
                existing.Manufacturer = manufacturer;
                existing.ManufacturerPartNumber = mfgPart;
                existing.IsActive = isActive;
            }

            result.Updated++;
        }
    }

    /// <summary>
    /// Inventory CSV format (header expected):
    /// PartNumber,LocationCode,Site,QuantityOnHand,ReorderPoint,TargetQuantity
    /// </summary>
    private async Task ImportInventoryRow(string[] cols, Guid tenantId, bool dryRun, CsvImportResultViewModel result)
    {
        if (cols.Length < 3)
        {
            result.Errors.Add("Inventory: expected at least PartNumber,LocationCode,Site.");
            return;
        }

        var partNumber = cols[0];
        var locationCode = cols[1];
        var site = cols[2];

        if (string.IsNullOrWhiteSpace(partNumber) ||
            string.IsNullOrWhiteSpace(locationCode) ||
            string.IsNullOrWhiteSpace(site))
        {
            result.Errors.Add("Inventory: PartNumber, LocationCode, and Site are required.");
            return;
        }

        decimal qty = 0m, reorder = 0m, target = 0m;

        if (cols.Length > 3 && !string.IsNullOrWhiteSpace(cols[3]))
            decimal.TryParse(cols[3], NumberStyles.Number, CultureInfo.InvariantCulture, out qty);

        if (cols.Length > 4 && !string.IsNullOrWhiteSpace(cols[4]))
            decimal.TryParse(cols[4], NumberStyles.Number, CultureInfo.InvariantCulture, out reorder);

        if (cols.Length > 5 && !string.IsNullOrWhiteSpace(cols[5]))
            decimal.TryParse(cols[5], NumberStyles.Number, CultureInfo.InvariantCulture, out target);

        var part = await _db.Parts
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PartNumber == partNumber);

        if (part == null)
        {
            result.Errors.Add($"Inventory: Part not found for PartNumber '{partNumber}'.");
            return;
        }

        var location = await _db.LocationBins
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Code == locationCode && b.Site == site);

        if (location == null)
        {
            if (!dryRun)
            {
                location = new LocationBin
                {
                    TenantId = tenantId,
                    Code = locationCode,
                    Site = site
                };
                _db.LocationBins.Add(location);
            }
        }

        // If location is being created on non-dry-run, we can't use its Id yet until SaveChanges.
        // But for v1 we can treat Part+LocationCode+Site as a unique key and rely on a single import.

        var inventory = await _db.InventoryLevels
            .Include(il => il.LocationBin)
            .FirstOrDefaultAsync(il =>
                il.TenantId == tenantId &&
                il.PartId == part.Id &&
                il.LocationBin.Code == locationCode &&
                il.LocationBin.Site == site);

        if (inventory == null)
        {
            if (!dryRun)
            {
                inventory = new InventoryLevel
                {
                    TenantId = tenantId,
                    Part = part,
                    LocationBin = location!,
                    QuantityOnHand = qty,
                    ReorderPoint = reorder,
                    TargetQuantity = target
                };
                _db.InventoryLevels.Add(inventory);
            }

            result.Inserted++;
        }
        else
        {
            if (!dryRun)
            {
                inventory.QuantityOnHand = qty;
                inventory.ReorderPoint = reorder;
                inventory.TargetQuantity = target;
            }

            result.Updated++;
        }
    }

    /// <summary>
    /// BOM CSV format (header expected):
    /// AssetCode,AssetName,WorkCenter,PartNumber,QuantityPerAsset
    /// </summary>
    private async Task ImportBomRow(string[] cols, Guid tenantId, bool dryRun, CsvImportResultViewModel result)
    {
        if (cols.Length < 4)
        {
            result.Errors.Add("BOM: expected at least AssetCode,AssetName,WorkCenter,PartNumber.");
            return;
        }

        var assetCode = cols[0];
        var assetName = cols[1];
        var workCenter = cols[2];
        var partNumber = cols[3];

        if (string.IsNullOrWhiteSpace(assetCode) ||
            string.IsNullOrWhiteSpace(assetName) ||
            string.IsNullOrWhiteSpace(partNumber))
        {
            result.Errors.Add("BOM: AssetCode, AssetName, and PartNumber are required.");
            return;
        }

        decimal qtyPerAsset = 1m;
        if (cols.Length > 4 && !string.IsNullOrWhiteSpace(cols[4]))
            decimal.TryParse(cols[4], NumberStyles.Number, CultureInfo.InvariantCulture, out qtyPerAsset);

        var part = await _db.Parts
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PartNumber == partNumber);

        if (part == null)
        {
            result.Errors.Add($"BOM: Part not found for PartNumber '{partNumber}'.");
            return;
        }

        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AssetCode == assetCode);

        if (asset == null)
        {
            if (!dryRun)
            {
                asset = new Asset
                {
                    TenantId = tenantId,
                    AssetCode = assetCode,
                    Name = assetName,
                    WorkCenter = workCenter
                };
                _db.Assets.Add(asset);
            }
        }

        var existingBom = await _db.BomItems
            .Include(b => b.Asset)
            .Include(b => b.Part)
            .FirstOrDefaultAsync(b =>
                b.TenantId == tenantId &&
                b.Asset.AssetCode == assetCode &&
                b.Part.PartNumber == partNumber);

        if (existingBom == null)
        {
            if (!dryRun)
            {
                var bom = new BomItem
                {
                    TenantId = tenantId,
                    Asset = asset!,
                    Part = part,
                    QuantityPerAsset = qtyPerAsset
                };
                _db.BomItems.Add(bom);
            }

            result.Inserted++;
        }
        else
        {
            if (!dryRun)
            {
                existingBom.QuantityPerAsset = qtyPerAsset;
            }

            result.Updated++;
        }
    }
}

