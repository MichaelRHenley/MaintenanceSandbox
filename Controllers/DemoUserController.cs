using System.Security.Claims;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Controllers;

[AllowAnonymous]
public class DemoUserController : Controller
{
    private readonly IDemoUserProvider _demoUserProvider;
    private readonly AppDbContext _db;

    public DemoUserController(IDemoUserProvider demoUserProvider, AppDbContext db)
    {
        _demoUserProvider = demoUserProvider;
        _db = db;
    }

    // Simple page that tells people what demo accounts exist
    [HttpGet]
    public IActionResult Index()
    {
        var vm = new DemoUserViewModel
        {
            Name = "Supervisor",
            Role = "Supervisor",
            Users =
        {
            new DemoUser
            {
                Name = "Supervisor",
                Email = "supervisor@sentinel-demo.local",
                Role = "Supervisor"
            },
            new DemoUser
            {
                Name = "Operator",
                Email = "operator@sentinel-demo.local",
                Role = "Operator"
            }
        }
        };

        return View(vm);
    }



    // POST /DemoUser/Switch  — fresh session (called from login page or marketing site)
    [HttpPost]
    public async Task<IActionResult> Switch(string role, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(role))
            return RedirectToAction("Login", "Account");

        var user = _demoUserProvider.GetByRole(role);
        if (user is null)
        {
            TempData["Error"] = "Unknown demo role.";
            return RedirectToAction("Login", "Account");
        }

        // Purge stale sessions then spin up a fresh isolated tenant for this visitor.
        await DbInitializer.PurgeExpiredDemoTenantsAsync(_db, TimeSpan.FromHours(2));
        var tenantId = await DbInitializer.SeedDemoSessionAsync(_db);

        await SignInDemoUser(user, tenantId.ToString());

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Maintenance");
    }

    // POST /DemoUser/SwitchRole — changes role within the same demo tenant (no reseed)
    [HttpPost]
    public async Task<IActionResult> SwitchRole(string role, string? returnUrl = null)
    {
        // Must already be an active demo session
        var tenantIdRaw = User.FindFirstValue("tenant_id");
        if (!User.HasClaim("is_demo", "true") || string.IsNullOrEmpty(tenantIdRaw))
            return RedirectToAction("Login", "Account");

        var user = _demoUserProvider.GetByRole(role);
        if (user is null)
            return RedirectToAction("Index", "Maintenance");

        await SignInDemoUser(user, tenantIdRaw);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Maintenance");
    }

    private async Task SignInDemoUser(DemoUser user, string tenantId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("tenant_id", tenantId),
            new Claim("is_demo", "true")
        };

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme)));
    }
}
