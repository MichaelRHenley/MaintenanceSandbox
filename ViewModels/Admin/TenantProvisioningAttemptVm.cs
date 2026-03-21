namespace MaintenanceSandbox.ViewModels.Admin;

public record TenantProvisioningAttemptVm(
    string CorrelationId,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int DurationSeconds,
    bool IsSuccessful,
    bool HasFailure,
    string? Actor,
    IReadOnlyList<TenantProvisioningEventVm> Events);
