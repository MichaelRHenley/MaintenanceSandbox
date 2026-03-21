using MaintenanceSandbox.Data;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Services.Ai;

public sealed class EquipmentContextProvider : IEquipmentContextProvider
{
    private readonly AppDbContext _db;

    public EquipmentContextProvider(AppDbContext db) => _db = db;

    public async Task<AiEquipmentSnapshot?> GetSnapshotAsync(AiParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(intent.Equipment))
            return null;

        var latest = await _db.MaintenanceRequests
            .Include(r => r.Equipment)
            .Include(r => r.Messages)
            .Where(r =>
                r.Equipment != null &&
                (r.Equipment.DisplayName == intent.Equipment || r.Equipment.Code == intent.Equipment))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return null;

        var lastTechComment = latest.Messages
            .Where(m => m.Sender == "Technician" || m.Sender == "Tech")
            .OrderByDescending(m => m.SentAt)
            .Select(m => m.Message)
            .FirstOrDefault();

        return new AiEquipmentSnapshot
        {
            Equipment = latest.Equipment?.DisplayName ?? latest.Equipment?.Code ?? intent.Equipment,
            CurrentStatus = latest.Status,
            LastIssue = latest.Description,
            LastMaintenanceComment = lastTechComment
        };
    }

    public async Task<List<string>> GetKnownNamesAsync(CancellationToken ct)
    {
        return await _db.Equipment
            .Where(e => e.IsActive)
            .OrderBy(e => e.DisplayName ?? e.Code)
            .Select(e => e.DisplayName ?? e.Code)
            .ToListAsync(ct);
    }
}
