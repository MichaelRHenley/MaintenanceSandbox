using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MaintenanceSandbox.Directory.Data;
using MaintenanceSandbox.Directory.Models;

namespace MaintenanceSandbox.Middleware;

public sealed class SubscriptionGateMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext ctx,
        UserManager<ApplicationUser> users,
        DirectoryDbContext directoryDb)
    {
        var path = (ctx.Request.Path.Value ?? "").ToLowerInvariant();

        // Allow these paths through without gating
        if (path.StartsWith("/identity") ||
            path.StartsWith("/subscribe") ||
            path.StartsWith("/css") ||
            path.StartsWith("/js") ||
            path.StartsWith("/lib") ||
            path.StartsWith("/hubs") ||
            path.StartsWith("/health") ||
            path.StartsWith("/swagger"))
        {
            await _next(ctx);
            return;
        }

        if (ctx.User?.Identity?.IsAuthenticated == true)
        {
            var user = await users.GetUserAsync(ctx.User);

            if (user != null)
            {
                // 1) No tenant assigned -> force subscription/onboarding
                if (user.TenantId is null || user.TenantId == Guid.Empty)
                {
                    ctx.Response.Redirect("/subscribe");
                    return;
                }

                // 2) No active subscription -> force subscription page
                var hasActive = await directoryDb.TenantSubscriptions
                    .AnyAsync(s => s.TenantId == user.TenantId && s.IsActive);

                if (!hasActive)
                {
                    ctx.Response.Redirect("/subscribe");
                    return;
                }
            }
        }

        await _next(ctx);
    }
}

