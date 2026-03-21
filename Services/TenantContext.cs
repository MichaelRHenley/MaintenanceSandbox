using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Scoped concrete implementation of ITenantContext.
/// TenantContextMiddleware calls Set() once per request after resolving the tenant.
/// All other consumers read through the ITenantContext interface.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public string? TenantName { get; private set; }
    public TenantProvisioningStatus ProvisioningStatus { get; private set; } = TenantProvisioningStatus.Pending;
    public bool IsResolved { get; private set; }

    /// <summary>Called once by TenantContextMiddleware to populate tenant identity for this request.</summary>
    public void Set(Guid tenantId, string? tenantName, TenantProvisioningStatus status)
    {
        TenantId = tenantId;
        TenantName = tenantName;
        ProvisioningStatus = status;
        IsResolved = true;
    }
}
