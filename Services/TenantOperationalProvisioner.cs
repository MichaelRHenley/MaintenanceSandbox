using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Models.MasterData;
using Microsoft.EntityFrameworkCore;
using Site = MaintenanceSandbox.Models.MasterData.Site;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Creates the minimal operational records in AppDbContext when a new tenant is
/// onboarded. Sentinel controls this flow — customers never touch the schema.
///
/// What it provisions:
///   1. A Tenant row in the business DB (the AppDbContext tenant registry).
///   2. One default Site named after the company.
///   3. One default Area ("Main Area") under that Site.
///   4. One default WorkCenter (code "WC-DEFAULT") under that Area.
///   5. One default Equipment placeholder (code "EQ-DEFAULT") under that WorkCenter.
///
/// Everything else is configured by the plant admin through the MasterDataAdmin UI.
/// </summary>
public sealed class TenantOperationalProvisioner : ITenantOperationalProvisioner
{
    private readonly AppDbContext _db;
    private readonly ITenantProvisioningAuditLogger _auditLogger;

    public TenantOperationalProvisioner(AppDbContext db, ITenantProvisioningAuditLogger auditLogger)
    {
        _db = db;
        _auditLogger = auditLogger;
    }

    public async Task ProvisionAsync(Guid tenantId, string companyName, string? actor = null, CancellationToken ct = default)
    {
        companyName = string.IsNullOrWhiteSpace(companyName) ? "New Plant" : companyName.Trim();

        var correlationId = Guid.NewGuid().ToString("D");
        var startedAt = DateTime.UtcNow;

        // Step 1: Ensure the Tenant row exists and mark it as Provisioning.
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var statusBefore = tenant?.ProvisioningStatus ?? TenantProvisioningStatus.Pending;
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = tenantId,
                Name = companyName,
                PlanTier = "Standard",
                IsActive = true,
                ProvisioningStatus = TenantProvisioningStatus.Provisioning,
                ProvisioningStartedAt = DateTime.UtcNow,
                ProvisioningActor = actor
            };
            _db.Tenants.Add(tenant);
        }
        else
        {
            // Every re-run after the first attempt counts as a retry.
            if (tenant.ProvisioningStartedAt.HasValue)
                tenant.ProvisioningRetryCount++;

            tenant.ProvisioningStatus = TenantProvisioningStatus.Provisioning;
            tenant.ProvisioningStartedAt = DateTime.UtcNow;
            tenant.ProvisioningCompletedAt = null;
            tenant.LastProvisioningError = null;
            tenant.ProvisioningActor = actor;
        }
        await _db.SaveChangesAsync(ct);
        try { await _auditLogger.LogEventAsync(tenantId, "ProvisionStart", statusBefore, TenantProvisioningStatus.Provisioning, success: true, actor: actor, correlationId: correlationId, ct: ct); } catch { }

        try
        {
            // Step 2: Default Site.
            // IgnoreQueryFilters because the current-user tenant filter would not match
            // the newly provisioned tenantId during onboarding.
            var site = await _db.Sites
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            if (site is null)
            {
                site = new Site { TenantId = tenantId, Name = companyName };
                _db.Sites.Add(site);
                await _db.SaveChangesAsync(ct);
            }

            // Step 3: Default Area.
            var area = await _db.Areas
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.SiteId == site.Id, ct);

            if (area is null)
            {
                area = new Area { TenantId = tenantId, SiteId = site.Id, Name = "Main Area" };
                _db.Areas.Add(area);
                await _db.SaveChangesAsync(ct);
            }

            // Step 4: Default WorkCenter.
            var wc = await _db.WorkCenters
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.AreaId == area.Id, ct);

            if (wc is null)
            {
                wc = new WorkCenter
                {
                    TenantId = tenantId,
                    AreaId = area.Id,
                    Code = "WC-DEFAULT",
                    DisplayName = "Default Work Center"
                };
                _db.WorkCenters.Add(wc);
                await _db.SaveChangesAsync(ct);
            }

            // Step 5: Default Equipment placeholder.
            var hasEquipment = await _db.Equipment
                .IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.WorkCenterId == wc.Id, ct);

            if (!hasEquipment)
            {
                _db.Equipment.Add(new Equipment
                {
                    TenantId = tenantId,
                    WorkCenterId = wc.Id,
                    Code = "EQ-DEFAULT",
                    DisplayName = "Default Equipment",
                    IsActive = true
                });
            }

            // Mark Ready.
            tenant.ProvisioningStatus = TenantProvisioningStatus.Ready;
            tenant.ProvisionedAt = DateTime.UtcNow;
            tenant.ProvisioningCompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            var successDuration = (int)(DateTime.UtcNow - startedAt).TotalSeconds;
            try { await _auditLogger.LogEventAsync(tenantId, "ProvisionSuccess", TenantProvisioningStatus.Provisioning, TenantProvisioningStatus.Ready, success: true, actor: actor, durationSeconds: successDuration, correlationId: correlationId, ct: ct); } catch { }
        }
        catch (Exception ex)
        {
            tenant.ProvisioningStatus = TenantProvisioningStatus.Failed;
            tenant.LastProvisioningError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            try { await _db.SaveChangesAsync(ct); } catch { /* best effort — preserve error state */ }
            var failDuration = (int)(DateTime.UtcNow - startedAt).TotalSeconds;
            try { await _auditLogger.LogEventAsync(tenantId, "ProvisionFailed", TenantProvisioningStatus.Provisioning, TenantProvisioningStatus.Failed, success: false, actor: actor, errorMessage: ex.Message, durationSeconds: failDuration, correlationId: correlationId, ct: ct); } catch { }
            throw;
        }
    }
}

