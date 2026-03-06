using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MaintenanceSandbox.Directory.Data;
using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Directory.Models.Tenants;

namespace MaintenanceSandbox.Directory.Services;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly DirectoryDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public TenantProvisioningService(DirectoryDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<ProvisionTenantResult> EnsureTenantAndSubscriptionAsync(
        string userId,
        string companyName,
        string tier,
        string billingCadence,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId is required.", nameof(userId));

        companyName = (companyName ?? "").Trim();
        if (companyName.Length == 0)
            throw new ArgumentException("companyName is required.", nameof(companyName));

        tier = string.IsNullOrWhiteSpace(tier) ? "Tier1" : tier.Trim();
        billingCadence = string.IsNullOrWhiteSpace(billingCadence) ? "Monthly" : billingCadence.Trim();

        // Load user
        var user = await _users.FindByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        // Already has tenant? Ensure subscription exists and return state.
        if (user.TenantId is Guid existingTenantId && existingTenantId != Guid.Empty)
        {
            var sub = await _db.TenantSubscriptions.SingleOrDefaultAsync(
                s => s.TenantId == existingTenantId, ct);

            if (sub == null)
            {
                // Create missing subscription (Pilot mode: active immediately)
                sub = new TenantSubscription
                {
                    TenantId = existingTenantId,
                    Tier = tier,
                    BillingCadence = billingCadence,
                    IsActive = true
                };
                _db.TenantSubscriptions.Add(sub);
                await _db.SaveChangesAsync(ct);
            }

            // Keep user state consistent
            if (sub.IsActive && user.ProvisioningState != UserProvisioningState.Active)
            {
                user.ProvisioningState = UserProvisioningState.Active;
                user.CompanyName ??= companyName;
                await _users.UpdateAsync(user);
            }

            return new ProvisionTenantResult(existingTenantId, sub.IsActive, sub.Tier, sub.BillingCadence);
        }

        // Provision new tenant + subscription + role in a transaction
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var tenant = new Tenant
        {
            Name = companyName,
            Status = TenantStatus.Active
        };
        _db.Tenants.Add(tenant);

        // Pilot mode: activate immediately.
        var subscription = new TenantSubscription
        {
            TenantId = tenant.Id,
            Tier = tier,
            BillingCadence = billingCadence,
            IsActive = true
        };
        _db.TenantSubscriptions.Add(subscription);

        // Assign tenant role
        _db.TenantUserRoles.Add(new TenantUserRole
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.TenantOwner
        });

        await _db.SaveChangesAsync(ct);

        // Update Identity user
        user.TenantId = tenant.Id;
        user.CompanyName = companyName;
        user.ProvisioningState = subscription.IsActive
            ? UserProvisioningState.Active
            : UserProvisioningState.PaymentPending;

        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            throw new InvalidOperationException("Failed to update user: " + string.Join("; ", upd.Errors.Select(e => e.Description)));

        await tx.CommitAsync(ct);

        return new ProvisionTenantResult(tenant.Id, subscription.IsActive, subscription.Tier, subscription.BillingCadence);
    }
}
