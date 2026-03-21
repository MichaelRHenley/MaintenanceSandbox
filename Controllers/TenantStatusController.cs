using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Services;
using MaintenanceSandbox.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Controllers;

/// <summary>
/// Tenant-facing lifecycle status page.
/// Shown when ProvisioningStatusGateMiddleware determines the tenant is not yet Ready.
/// </summary>
[Authorize]
public sealed class TenantStatusController : Controller
{
    private readonly ITenantContext _tenantContext;
    private readonly AppDbContext _db;

    public TenantStatusController(ITenantContext tenantContext, AppDbContext db)
    {
        _tenantContext = tenantContext;
        _db = db;
    }

    // GET: /TenantStatus
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // If the tenant is Ready, there is nothing to show here — send them home.
        if (_tenantContext.IsResolved
            && _tenantContext.ProvisioningStatus == TenantProvisioningStatus.Ready)
        {
            return RedirectToAction("Index", "Home");
        }

        DateTime? provisionedAt = null;
        string? lastError = null;

        if (_tenantContext.TenantId != Guid.Empty)
        {
            var row = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == _tenantContext.TenantId)
                .Select(t => new { t.ProvisionedAt, t.LastProvisioningError })
                .FirstOrDefaultAsync();

            provisionedAt = row?.ProvisionedAt;
            lastError = row?.LastProvisioningError;
        }

        var vm = new TenantStatusVm(
            TenantId: _tenantContext.TenantId,
            TenantName: _tenantContext.TenantName,
            ProvisioningStatus: _tenantContext.ProvisioningStatus,
            ProvisionedAt: provisionedAt,
            LastProvisioningError: lastError,
            DisplayMessage: GetDisplayMessage(_tenantContext.ProvisioningStatus)
        );

        return View(vm);
    }

    private static string GetDisplayMessage(TenantProvisioningStatus status) => status switch
    {
        TenantProvisioningStatus.Pending
            => "Your workspace is queued for setup. This usually takes just a moment — please refresh the page shortly.",
        TenantProvisioningStatus.Provisioning
            => "Your workspace is being provisioned. Please wait a moment, then refresh this page.",
        TenantProvisioningStatus.Failed
            => "We encountered a problem setting up your workspace. Our team has been notified. Please contact Sentinel support if this persists.",
        TenantProvisioningStatus.Suspended
            => "Your account has been suspended. Please contact Sentinel support to discuss reactivation.",
        TenantProvisioningStatus.Ready
            => "Your workspace is ready.",
        _ => "Unable to determine workspace status. Please contact Sentinel support."
    };
}
