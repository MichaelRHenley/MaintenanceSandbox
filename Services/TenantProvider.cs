using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MaintenanceSandbox.Services;

public sealed class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _http;

    public TenantProvider(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid TryGetTenantId()
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return Guid.Empty;

        // IMPORTANT: match your actual claim type
        // Your UI shows "tenant_id claim: ..." so we read "tenant_id"
        var raw = user.FindFirstValue("tenant_id")
               ?? user.FindFirstValue("tenantId")
               ?? user.FindFirstValue("TenantId");

        return Guid.TryParse(raw, out var tid) ? tid : Guid.Empty;
    }

    public Guid GetTenantId()
    {
        var tid = TryGetTenantId();
        if (tid == Guid.Empty)
            throw new InvalidOperationException("TenantId not resolved");
        return tid;
    }
}
