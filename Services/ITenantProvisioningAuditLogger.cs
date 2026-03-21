using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Writes append-only provisioning audit events to the business database.
/// All parameters after <paramref name="success"/> are optional; callers
/// should supply whatever context is available without changing control flow.
/// Implementations must never throw — audit failures must not mask
/// the original provisioning exception.
/// </summary>
public interface ITenantProvisioningAuditLogger
{
    Task LogEventAsync(
        Guid tenantId,
        string action,
        TenantProvisioningStatus statusBefore,
        TenantProvisioningStatus statusAfter,
        bool success,
        string? actor = null,
        string? errorMessage = null,
        int? durationSeconds = null,
        string? correlationId = null,
        CancellationToken ct = default);
}
