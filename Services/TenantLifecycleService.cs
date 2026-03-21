using MaintenanceSandbox.Data;
using MaintenanceSandbox.Directory.Data;
using MaintenanceSandbox.Directory.Models.Tenants;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Owns all tenant lifecycle transitions across both databases.
/// DirectoryDbContext owns identity/billing state; AppDbContext owns operational provisioning state.
/// </summary>
public sealed class TenantLifecycleService : ITenantLifecycleService
{
    private readonly DirectoryDbContext _dir;
    private readonly AppDbContext _biz;
    private readonly ITenantOperationalProvisioner _provisioner;
    private readonly IConfiguration _config;
    private readonly ITenantProvisioningAuditLogger _auditLogger;

    public TenantLifecycleService(
        DirectoryDbContext dir,
        AppDbContext biz,
        ITenantOperationalProvisioner provisioner,
        IConfiguration config,
        ITenantProvisioningAuditLogger auditLogger)
    {
        _dir = dir;
        _biz = biz;
        _provisioner = provisioner;
        _config = config;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<TenantSummaryVm>> GetTenantSummariesAsync(CancellationToken ct = default)
    {
        var tenants = await _dir.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedUtc)
            .ToListAsync(ct);

        var tenantIds = tenants.Select(t => t.Id).ToList();

        var subscriptions = await _dir.TenantSubscriptions
            .AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId))
            .ToListAsync(ct);

        var userCounts = await _dir.TenantUserRoles
            .AsNoTracking()
            .Where(r => tenantIds.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        var bizStatuses = await _biz.Tenants
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.ProvisioningStatus })
            .ToDictionaryAsync(x => x.Id, x => x.ProvisioningStatus, ct);

        var subMap = subscriptions.ToDictionary(s => s.TenantId);

        return tenants.Select(t =>
        {
            subMap.TryGetValue(t.Id, out var sub);
            userCounts.TryGetValue(t.Id, out var users);
            bizStatuses.TryGetValue(t.Id, out var provStatus);
            return new TenantSummaryVm(
                TenantId: t.Id,
                Name: t.Name,
                Status: t.Status,
                CreatedUtc: t.CreatedUtc,
                Tier: sub?.Tier,
                SubscriptionActive: sub?.IsActive ?? false,
                UserCount: users,
                ProvisioningStatus: provStatus
            );
        }).ToList();
    }

    public Task ProvisionTenantAsync(Guid tenantId, string companyName, string? actor = null, CancellationToken ct = default)
        => _provisioner.ProvisionAsync(tenantId, companyName, actor, ct);

    public async Task RetryProvisionTenantAsync(Guid tenantId, string? actor, CancellationToken ct = default)
    {
        var bizTenant = await _biz.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (bizTenant is null)
            throw new InvalidOperationException($"Tenant {tenantId} not found in business database.");

        if (bizTenant.ProvisioningStatus is not TenantProvisioningStatus.Failed
                                         and not TenantProvisioningStatus.Provisioning)
        {
            throw new InvalidOperationException(
                $"Tenant \"{bizTenant.Name}\" has status {bizTenant.ProvisioningStatus} and is not eligible for retry.");
        }

        var statusBefore = bizTenant.ProvisioningStatus;
        var correlationId = Guid.NewGuid().ToString("D");
        var startedAt = DateTime.UtcNow;

        // Pre-flight reset: clear error state and return to Pending.
        // TenantOperationalProvisioner handles StartedAt, RetryCount increment, Actor, and CompletedAt.
        bizTenant.ProvisioningStatus = TenantProvisioningStatus.Pending;
        bizTenant.LastProvisioningError = null;
        bizTenant.ProvisioningCompletedAt = null;
        await _biz.SaveChangesAsync(ct);
        try { await _auditLogger.LogEventAsync(tenantId, "RetryRequested", statusBefore, TenantProvisioningStatus.Pending, success: true, actor: actor, correlationId: correlationId, ct: ct); } catch { }

        try
        {
            await _provisioner.ProvisionAsync(tenantId, bizTenant.Name, actor, ct);
            var duration = (int)(DateTime.UtcNow - startedAt).TotalSeconds;
            try { await _auditLogger.LogEventAsync(tenantId, "RetrySucceeded", statusBefore, TenantProvisioningStatus.Ready, success: true, actor: actor, durationSeconds: duration, correlationId: correlationId, ct: ct); } catch { }
        }
        catch (Exception ex)
        {
            var duration = (int)(DateTime.UtcNow - startedAt).TotalSeconds;
            try { await _auditLogger.LogEventAsync(tenantId, "RetryFailed", statusBefore, TenantProvisioningStatus.Failed, success: false, actor: actor, errorMessage: ex.Message, durationSeconds: duration, correlationId: correlationId, ct: ct); } catch { }
            throw;
        }
    }

    public async Task<IReadOnlyList<TenantHealthSummaryVm>> GetTenantHealthSummariesAsync(CancellationToken ct = default)
    {
        // 1. All tenants from directory, newest first
        var tenants = await _dir.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedUtc)
            .ToListAsync(ct);

        var tenantIds = tenants.Select(t => t.Id).ToList();

        // 2. User counts per tenant from directory
        var userCounts = await _dir.TenantUserRoles
            .AsNoTracking()
            .Where(r => tenantIds.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        // 3. Full provisioning state from business DB (Tenants has no global query filter)
        var bizTenants = await _biz.Tenants
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .ToListAsync(ct);
        var bizData = bizTenants.ToDictionary(t => t.Id);

        // 4. Last activity per tenant — max completed onboarding timestamp
        //    IgnoreQueryFilters required because OnboardingSession is a TenantEntity
        var lastActivityRaw = await _biz.OnboardingSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId) && s.OnboardedAtUtc != null)
            .GroupBy(s => s.TenantId)
            .Select(g => new { TenantId = g.Key, LastAt = g.Max(s => s.OnboardedAtUtc) })
            .ToListAsync(ct);
        var lastActivity = lastActivityRaw.ToDictionary(x => x.TenantId, x => x.LastAt);

        // 5. Stale threshold from configuration
        var staleMinutes = _config.GetValue<int>("Provisioning:StaleThresholdMinutes", 10);
        var staleThreshold = TimeSpan.FromMinutes(staleMinutes);
        var now = DateTime.UtcNow;

        return tenants.Select(t =>
        {
            userCounts.TryGetValue(t.Id, out var users);
            bizData.TryGetValue(t.Id, out var biz);
            lastActivity.TryGetValue(t.Id, out var lastAtOffset);

            var isStale = biz?.ProvisioningStatus == TenantProvisioningStatus.Provisioning
                && biz.ProvisioningStartedAt.HasValue
                && now - biz.ProvisioningStartedAt.Value > staleThreshold;

            return new TenantHealthSummaryVm(
                TenantId: t.Id,
                Name: t.Name,
                ProvisioningStatus: biz?.ProvisioningStatus ?? TenantProvisioningStatus.Pending,
                ProvisioningStartedAt: biz?.ProvisioningStartedAt,
                ProvisioningCompletedAt: biz?.ProvisioningCompletedAt,
                LastProvisioningError: biz?.LastProvisioningError,
                ProvisioningRetryCount: biz?.ProvisioningRetryCount ?? 0,
                ProvisioningActor: biz?.ProvisioningActor,
                UserCount: users,
                IsProvisioningStale: isStale,
                LastActivityAt: lastAtOffset?.UtcDateTime
            );
        }).ToList();
    }

    public async Task<string?> SuspendTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var dirTenant = await _dir.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (dirTenant is null) return null;

        dirTenant.Status = TenantStatus.Suspended;

        var sub = await _dir.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (sub is not null) sub.IsActive = false;

        await _dir.SaveChangesAsync(ct);

        var bizTenant = await _biz.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (bizTenant is not null)
        {
            bizTenant.ProvisioningStatus = TenantProvisioningStatus.Suspended;
            await _biz.SaveChangesAsync(ct);
        }

        return dirTenant.Name;
    }

    public async Task<string?> ReactivateTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var dirTenant = await _dir.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (dirTenant is null) return null;

        dirTenant.Status = TenantStatus.Active;

        var sub = await _dir.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (sub is not null) sub.IsActive = true;

        await _dir.SaveChangesAsync(ct);

        var bizTenant = await _biz.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (bizTenant is not null)
        {
            bizTenant.ProvisioningStatus = TenantProvisioningStatus.Ready;
            await _biz.SaveChangesAsync(ct);
        }

        return dirTenant.Name;
    }

    public async Task<(string? TenantName, IReadOnlyList<TenantProvisioningEventVm> Events)>
        GetTenantProvisioningEventsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenantName = await _dir.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);

        var events = await _biz.TenantProvisioningEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.TimestampUtc)
            .Select(e => new TenantProvisioningEventVm(
                e.TimestampUtc,
                e.Actor,
                e.Action,
                e.StatusBefore,
                e.StatusAfter,
                e.Success,
                e.ErrorMessage,
                e.DurationSeconds,
                e.CorrelationId))
            .ToListAsync(ct);

        return (tenantName, events);
    }

    public async Task<TenantProvisioningHistoryVm>
        GetTenantProvisioningHistoryAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenantName = await _dir.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? tenantId.ToString();

        var rawEvents = await _biz.TenantProvisioningEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync(ct);

        var attempts = rawEvents
            .GroupBy(e => string.IsNullOrEmpty(e.CorrelationId)
                ? $"no-correlation-{e.Id}"
                : e.CorrelationId)
            .Select(g =>
            {
                var events = g.OrderBy(e => e.TimestampUtc).ToList();
                var startedAt = events.Min(e => e.TimestampUtc);
                var completedAt = events.Max(e => e.TimestampUtc);
                var duration = (int)(completedAt - startedAt).TotalSeconds;
                var isSuccessful = events.Any(e => e.Action is "ProvisionSuccess" or "RetrySucceeded");
                var hasFailure = events.Any(e => e.Action is "ProvisionFailed" or "RetryFailed");
                var actor = events.Select(e => e.Actor).FirstOrDefault(a => a is not null);
                var eventVms = events.Select(e => new TenantProvisioningEventVm(
                    e.TimestampUtc,
                    e.Actor,
                    e.Action,
                    e.StatusBefore,
                    e.StatusAfter,
                    e.Success,
                    e.ErrorMessage,
                    e.DurationSeconds,
                    e.CorrelationId)).ToList();
                return new TenantProvisioningAttemptVm(
                    g.Key, startedAt, completedAt, duration,
                    isSuccessful, hasFailure, actor, eventVms);
            })
            .OrderByDescending(a => a.StartedAtUtc)
            .ToList();

        return new TenantProvisioningHistoryVm(tenantId, tenantName, attempts);
    }
}
