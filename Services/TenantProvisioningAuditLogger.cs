using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services;

public sealed class TenantProvisioningAuditLogger : ITenantProvisioningAuditLogger
{
    private readonly AppDbContext _db;

    public TenantProvisioningAuditLogger(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogEventAsync(
        Guid tenantId,
        string action,
        TenantProvisioningStatus statusBefore,
        TenantProvisioningStatus statusAfter,
        bool success,
        string? actor = null,
        string? errorMessage = null,
        int? durationSeconds = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        _db.TenantProvisioningEvents.Add(new TenantProvisioningEvent
        {
            TenantId = tenantId,
            TimestampUtc = DateTime.UtcNow,
            Actor = actor,
            Action = action,
            StatusBefore = statusBefore,
            StatusAfter = statusAfter,
            Success = success,
            ErrorMessage = errorMessage?.Length > 2000 ? errorMessage[..2000] : errorMessage,
            DurationSeconds = durationSeconds,
            CorrelationId = correlationId
        });
        await _db.SaveChangesAsync(ct);
    }
}
