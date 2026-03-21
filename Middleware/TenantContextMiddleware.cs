using System.Security.Claims;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Services;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Middleware;

/// <summary>
/// Resolves tenant identity once per request and populates the scoped TenantContext.
/// Must run after UseAuthentication() so that user claims are available.
/// Fails safely — if the tenant cannot be resolved the request continues unblocked.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, TenantContext tenantContext, AppDbContext db)
    {
        if (ctx.User?.Identity?.IsAuthenticated == true)
        {
            var raw = ctx.User.FindFirstValue("tenant_id")
                   ?? ctx.User.FindFirstValue("tenantId")
                   ?? ctx.User.FindFirstValue("TenantId");

            if (Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty)
            {
                try
                {
                    var tenant = await db.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == tenantId, ctx.RequestAborted);

                    if (tenant is not null)
                        tenantContext.Set(tenantId, tenant.Name, tenant.ProvisioningStatus);
                }
                catch
                {
                    // Fail safe — middleware must never block the request pipeline
                }
            }
        }

        await _next(ctx);
    }
}
