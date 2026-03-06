using MaintenanceSandbox.Directory.Models.Tenants;

namespace MaintenanceSandbox.Directory.Services;

public sealed record ProvisionTenantResult(
    Guid TenantId,
    bool SubscriptionActive,
    string Tier,
    string BillingCadence);

public interface ITenantProvisioningService
{
    Task<ProvisionTenantResult> EnsureTenantAndSubscriptionAsync(
        string userId,
        string companyName,
        string tier,
        string billingCadence,
        CancellationToken ct = default);
}

