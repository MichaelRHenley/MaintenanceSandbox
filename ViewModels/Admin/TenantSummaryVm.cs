using MaintenanceSandbox.Directory.Models.Tenants;
using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.ViewModels.Admin;

public sealed record TenantSummaryVm(
    Guid TenantId,
    string Name,
    TenantStatus Status,
    DateTime CreatedUtc,
    string? Tier,
    bool SubscriptionActive,
    int UserCount,
    TenantProvisioningStatus ProvisioningStatus = TenantProvisioningStatus.Pending
);
