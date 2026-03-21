using MaintenanceSandbox.ViewModels.Admin;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Owns all tenant lifecycle transitions: provisioning, suspension, and reactivation.
/// Controllers depend only on this service; it internally coordinates both
/// the Directory DB (identity/billing) and the business AppDbContext.
/// </summary>
public interface ITenantLifecycleService
{
    /// <summary>Returns a summary list of all tenants for the admin panel.</summary>
    Task<IReadOnlyList<TenantSummaryVm>> GetTenantSummariesAsync(CancellationToken ct = default);

    /// <summary>Returns detailed provisioning health for all tenants (admin health dashboard).</summary>
    Task<IReadOnlyList<TenantHealthSummaryVm>> GetTenantHealthSummariesAsync(CancellationToken ct = default);

    /// <summary>Runs full operational provisioning for an existing tenant record.</summary>
    /// <param name="actor">Optional caller identity recorded for provisioning observability.</param>
    Task ProvisionTenantAsync(Guid tenantId, string companyName, string? actor = null, CancellationToken ct = default);

    /// <summary>
    /// Retries provisioning for a tenant whose status is <see cref="TenantProvisioningStatus.Failed"/>
    /// or stale <see cref="TenantProvisioningStatus.Provisioning"/>.
    /// Resets lifecycle state and delegates to <see cref="ITenantOperationalProvisioner"/>.
    /// Throws <see cref="InvalidOperationException"/> if the tenant is not found or not eligible.
    /// </summary>
    Task RetryProvisionTenantAsync(Guid tenantId, string? actor, CancellationToken ct = default);

    /// <summary>
    /// Suspends the tenant in both the Directory and business databases.
    /// Returns the tenant name on success, or null if the tenant was not found.
    /// </summary>
    Task<string?> SuspendTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Reactivates a suspended tenant in both databases.
    /// Returns the tenant name on success, or null if the tenant was not found.
    /// </summary>
    Task<string?> ReactivateTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns all provisioning audit events for a tenant, newest first,
    /// along with the tenant display name for the page header.
    /// </summary>
    Task<(string? TenantName, IReadOnlyList<TenantProvisioningEventVm> Events)>
        GetTenantProvisioningEventsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns provisioning audit events grouped into attempt cards by CorrelationId,
    /// newest attempt first, events within each attempt ordered oldest to newest.
    /// Events with no CorrelationId are treated as individual single-event attempts.
    /// </summary>
    Task<TenantProvisioningHistoryVm>
        GetTenantProvisioningHistoryAsync(Guid tenantId, CancellationToken ct = default);
}

