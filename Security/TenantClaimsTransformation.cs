using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using MaintenanceSandbox.Directory.Models;

namespace MaintenanceSandbox.Security;

public sealed class TenantClaimsTransformation : IClaimsTransformation
{
    public const string TenantClaimType = "tenant_id";

    private readonly UserManager<ApplicationUser> _users;

    public TenantClaimsTransformation(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        // Avoid re-adding
        if (principal.HasClaim(c => c.Type == TenantClaimType))
            return principal;

        var user = await _users.GetUserAsync(principal);
        if (user?.TenantId is null || user.TenantId == Guid.Empty)
            return principal;

        if (principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(TenantClaimType, user.TenantId.Value.ToString()));
        }

        return principal;
    }
}

