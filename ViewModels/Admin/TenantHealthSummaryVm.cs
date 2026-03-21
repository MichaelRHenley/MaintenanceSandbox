using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.ViewModels.Admin;

public sealed record TenantHealthSummaryVm(
    Guid TenantId,
    string Name,
    TenantProvisioningStatus ProvisioningStatus,
    DateTime? ProvisioningStartedAt,
    DateTime? ProvisioningCompletedAt,
    string? LastProvisioningError,
    int ProvisioningRetryCount,
    string? ProvisioningActor,
    int UserCount,
    /// <summary>
    /// True when ProvisioningStatus is Provisioning and ProvisioningStartedAt
    /// is older than the configured stale threshold (default 10 minutes).
    /// Indicates a provisioning run that may be hung or never completed.
    /// </summary>
    bool IsProvisioningStale,
    DateTime? LastActivityAt
);
