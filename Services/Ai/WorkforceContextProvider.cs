using MaintenanceSandbox.Data;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Services.Ai;

public sealed class WorkforceContextProvider : IWorkforceContextProvider
{
    private readonly AppDbContext _db;

    public WorkforceContextProvider(AppDbContext db) => _db = db;

    public async Task<AiWorkforceSnapshot> GetWorkforceAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-8);

        var activeResponders = await _db.MaintenanceMessages
            .Where(m =>
                m.SentAt >= since &&
                (m.Sender == "Technician" || m.Sender == "Tech" || m.Sender == "Supervisor"))
            .Select(m => m.Sender)
            .Distinct()
            .ToListAsync(ct);

        return new AiWorkforceSnapshot
        {
            AvailableResponders = activeResponders.Count,
            ActiveResponders = activeResponders
        };
    }
}
