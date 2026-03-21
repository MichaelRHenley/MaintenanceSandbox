using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.ViewModels;

public sealed record TenantStatusVm(
    Guid TenantId,
    string? TenantName,
    TenantProvisioningStatus ProvisioningStatus,
    DateTime? ProvisionedAt,
    string? LastProvisioningError,
    string DisplayMessage
);
