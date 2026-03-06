using MaintenanceSandbox.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MaintenanceSandbox.Services;

public sealed class RequireTenantFilter : IAsyncActionFilter
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IDemoMode _demoMode;
    public RequireTenantFilter(ITenantProvider tenantProvider, IDemoMode demoMode)
    {
        _tenantProvider = tenantProvider;
        _demoMode = demoMode;
    }


    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";
        if (_demoMode.IsDemoRequest())
        {
            await next();     // IMPORTANT: continue pipeline
            return;
        }


        // Allow Identity UI endpoints
        if (path.StartsWith("/Identity/Account", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Allow Subscribe endpoints (once you add them in app host)
        if (path.StartsWith("/Subscribe", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        try
        {
            _ = _tenantProvider.GetTenantId();
            await next();
        }
        catch (InvalidOperationException)
        {
            context.Result = new RedirectToActionResult("Index", "Subscribe", null);
        }
    }

}

