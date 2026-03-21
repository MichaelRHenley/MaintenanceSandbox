using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Scoped per-request tenant context. Populated by TenantContextMiddleware.
/// Inject this to read the resolved tenant identity anywhere in the request pipeline,
/// including controllers, services, and SignalR hubs.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string? TenantName { get; }
    TenantProvisioningStatus ProvisioningStatus { get; }

    /// <summary>True once TenantContextMiddleware has successfully resolved the tenant.</summary>
    bool IsResolved { get; }
}
