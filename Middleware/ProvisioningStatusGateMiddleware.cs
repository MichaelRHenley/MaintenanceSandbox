using MaintenanceSandbox.Models;
using MaintenanceSandbox.Services;

namespace MaintenanceSandbox.Middleware;

/// <summary>
/// Enforces ProvisioningStatus tenant lifecycle state on every authenticated browser request.
/// Tenants that are not Ready (Pending / Provisioning / Failed / Suspended) are redirected
/// to the TenantStatus page so the user sees a clear, actionable message.
///
/// Pipeline position: immediately after TenantContextMiddleware.
/// Must run after UseAuthentication() and TenantContextMiddleware (needs resolved claims
/// and a populated ITenantContext), and before SubscriptionGateMiddleware.
/// </summary>
public sealed class ProvisioningStatusGateMiddleware
{
    // Redirect target — must be in the bypass list to prevent redirect loops.
    private const string StatusPath = "/TenantStatus";

    // Paths that must always pass through regardless of provisioning state.
    private static readonly string[] BypassPrefixes =
    [
        "/TenantStatus",    // the status page itself — avoid redirect loop
        "/Account",         // login, logout, access denied
        "/Identity",        // ASP.NET Core Identity Razor pages
        "/Subscribe",       // subscription / sign-up flow
        "/Onboarding",      // onboarding wizard
        "/SentinelAdmin",   // Sentinel platform operators must never be blocked
        "/DemoUser",        // demo session switch — lets stuck demo users escape to a fresh session
        "/hubs",            // SignalR endpoints
        "/api",             // JSON API endpoints
        "/health",          // health checks
        "/swagger",         // API docs
    ];

    private readonly RequestDelegate _next;

    public ProvisioningStatusGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenantContext)
    {
        // Only gate authenticated browser requests.
        if (ctx.User?.Identity?.IsAuthenticated == true
            && IsBrowserRequest(ctx)
            && !IsBypassPath(ctx.Request.Path))
        {
            // Only act when TenantContextMiddleware successfully resolved a tenant.
            if (tenantContext.IsResolved
                && tenantContext.ProvisioningStatus != TenantProvisioningStatus.Ready)
            {
                ctx.Response.Redirect(StatusPath);
                return;
            }
        }

        await _next(ctx);
    }

    private static bool IsBrowserRequest(HttpContext ctx)
        => ctx.Request.Headers.Accept.Any(
            a => a!.Contains("text/html", StringComparison.OrdinalIgnoreCase));

    private static bool IsBypassPath(PathString path)
    {
        foreach (var prefix in BypassPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
