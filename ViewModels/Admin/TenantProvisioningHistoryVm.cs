namespace MaintenanceSandbox.ViewModels.Admin;

public record TenantProvisioningHistoryVm(
    Guid TenantId,
    string TenantName,
    IReadOnlyList<TenantProvisioningAttemptVm> Attempts);
