namespace MaintenanceSandbox.Services;

/// <summary>
/// Handles the AppDbContext (business-data) side of tenant provisioning.
/// Sentinel calls this once when a new tenant is onboarded; the plant admin
/// then configures Sites / Areas / Work Centers / Equipment via MasterDataAdmin.
///
/// Idempotent — safe to call multiple times for the same tenantId.
/// </summary>
public interface ITenantOperationalProvisioner
{
    /// <param name="actor">Optional identity of the caller (e.g. user email) recorded for observability.</param>
    Task ProvisionAsync(Guid tenantId, string companyName, string? actor = null, CancellationToken ct = default);
}
