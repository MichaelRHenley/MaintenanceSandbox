using MaintenanceSandbox.Data;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Services.Ai;

public sealed class IncidentContextProvider : IIncidentContextProvider
{
    private readonly AppDbContext _db;

    public IncidentContextProvider(AppDbContext db) => _db = db;

    public async Task<List<AiIncidentSummary>> GetOpenIncidentsAsync(AiParsedIntent intent, CancellationToken ct)
    {
        var query = _db.MaintenanceRequests
            .Include(r => r.Equipment)
            .Where(r => r.Status != "Resolved" && r.Status != "Closed");

        if (!string.IsNullOrWhiteSpace(intent.Equipment))
            query = query.Where(r =>
                r.Equipment != null &&
                (r.Equipment.DisplayName == intent.Equipment || r.Equipment.Code == intent.Equipment));

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .Select(r => new AiIncidentSummary
            {
                Id = r.Id,
                Equipment = r.Equipment != null ? (r.Equipment.DisplayName ?? r.Equipment.Code) : "",
                Status = r.Status,
                Description = r.Description,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<List<AiIncidentSummary>> GetSimilarIncidentsAsync(AiParsedIntent intent, string userQuery, CancellationToken ct)
    {
        var query = _db.MaintenanceRequests
            .Include(r => r.Equipment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(intent.Equipment))
            query = query.Where(r =>
                r.Equipment != null &&
                (r.Equipment.DisplayName == intent.Equipment || r.Equipment.Code == intent.Equipment));

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .Select(r => new AiIncidentSummary
            {
                Id = r.Id,
                Equipment = r.Equipment != null ? (r.Equipment.DisplayName ?? r.Equipment.Code) : "",
                Status = r.Status,
                Description = r.Description,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }
}
