using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.ViewModels.Admin;

public record TenantProvisioningEventVm(
    DateTime TimestampUtc,
    string? Actor,
    string Action,
    TenantProvisioningStatus StatusBefore,
    TenantProvisioningStatus StatusAfter,
    bool Success,
    string? ErrorMessage,
    int? DurationSeconds,
    string? CorrelationId);
