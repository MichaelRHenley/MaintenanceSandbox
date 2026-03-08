using MaintenanceSandbox.Data;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MaintenanceSandbox.Filters;

/// <summary>
/// Blocks mutating actions when the current session belongs to a demo tenant.
/// Apply with [ServiceFilter(typeof(BlockDemoFilter))].
/// </summary>
public sealed class BlockDemoFilter : IAsyncActionFilter
{
    private readonly ITenantProvider _tenantProvider;
    private readonly AppDbContext _db;

    public BlockDemoFilter(ITenantProvider tenantProvider, AppDbContext db)
    {
        _tenantProvider = tenantProvider;
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantId = _tenantProvider.TryGetTenantId();

        if (tenantId != Guid.Empty)
        {
            var tenant = await _db.Tenants.FindAsync(tenantId);
            if (tenant?.PlanTier == DbInitializer.DemoPlanTier)
            {
                if (context.Controller is Controller controller)
                    controller.TempData["err"] = "This action is not available in demo mode.";

                var controllerName = context.RouteData.Values["controller"]?.ToString();
                context.Result = new RedirectToActionResult("Index", controllerName, null);
                return;
            }
        }

        await next();
    }
}
