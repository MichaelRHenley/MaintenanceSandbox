using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaintenanceSandbox.Controllers;

/// <summary>
/// Cross-tenant management panel for Sentinel operators.
/// Requires the SentinelAdmin role — assign this role manually to Sentinel
/// team members; it is never granted during normal tenant onboarding.
///
/// Access: /SentinelAdmin
/// </summary>
[Authorize(Roles = "SentinelAdmin")]
public sealed class SentinelAdminController : Controller
{
    private readonly ITenantLifecycleService _lifecycle;

    public SentinelAdminController(ITenantLifecycleService lifecycle)
    {
        _lifecycle = lifecycle;
    }

    // GET: /SentinelAdmin
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vms = await _lifecycle.GetTenantSummariesAsync();
        return View(vms.ToList());
    }

    // GET: /SentinelAdmin/TenantHealth
    [HttpGet]
    public async Task<IActionResult> TenantHealth()
    {
        var summaries = await _lifecycle.GetTenantHealthSummariesAsync();
        return View(summaries.ToList());
    }

    // POST: /SentinelAdmin/Suspend
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var name = await _lifecycle.SuspendTenantAsync(id);
        if (name is null) return NotFound();
        TempData["ok"] = $"Tenant \"{name}\" suspended.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /SentinelAdmin/Reactivate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var name = await _lifecycle.ReactivateTenantAsync(id);
        if (name is null) return NotFound();
        TempData["ok"] = $"Tenant \"{name}\" reactivated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /SentinelAdmin/TenantProvisioningHistory
    [HttpGet]
    public async Task<IActionResult> TenantProvisioningHistory(Guid tenantId)
    {
        var vm = await _lifecycle.GetTenantProvisioningHistoryAsync(tenantId);
        return View(vm);
    }

    // POST: /SentinelAdmin/RetryProvision
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryProvision(Guid tenantId)
    {
        try
        {
            await _lifecycle.RetryProvisionTenantAsync(tenantId, User.Identity?.Name);
            TempData["ok"] = "Provisioning retry started successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["err"] = ex.Message;
        }
        return RedirectToAction(nameof(TenantHealth));
    }
}
