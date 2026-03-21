using MaintenanceSandbox.Data;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Services.Ai;

public sealed class PartsContextProvider : IPartsContextProvider
{
    private readonly AppDbContext _db;

    public PartsContextProvider(AppDbContext db) => _db = db;

    public async Task<AiPartsSnapshot?> GetPartsAsync(AiParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(intent.Equipment) && string.IsNullOrWhiteSpace(intent.WorkCenter))
            return null;

        var assetQuery = _db.Assets.AsQueryable();

        if (!string.IsNullOrWhiteSpace(intent.Equipment))
            assetQuery = assetQuery.Where(a =>
                a.Name.Contains(intent.Equipment) ||
                a.AssetCode.Contains(intent.Equipment) ||
                (a.WorkCenter != null && a.WorkCenter.Contains(intent.Equipment)));
        else if (!string.IsNullOrWhiteSpace(intent.WorkCenter))
            assetQuery = assetQuery.Where(a =>
                a.WorkCenter != null && a.WorkCenter.Contains(intent.WorkCenter));

        var assetIds = await assetQuery
            .Select(a => a.Id)
            .Take(5)
            .ToListAsync(ct);

        if (assetIds.Count == 0) return null;

        var partIds = await _db.BomItems
            .Where(b => assetIds.Contains(b.AssetId))
            .Select(b => b.PartId)
            .Distinct()
            .ToListAsync(ct);

        if (partIds.Count == 0) return null;

        var parts = await _db.Parts
            .Where(p => partIds.Contains(p.Id))
            .Select(p => new AiPartAvailability
            {
                PartNumber = p.PartNumber,
                Description = p.AiCleanDescription ?? p.ShortDescription ?? p.PartNumber,
                QtyOnHand = p.InventoryLevels.Sum(il => (decimal?)il.QuantityOnHand) ?? 0
            })
            .ToListAsync(ct);

        return new AiPartsSnapshot
        {
            Equipment = intent.Equipment ?? intent.WorkCenter ?? "",
            Parts = parts
        };
    }
}
