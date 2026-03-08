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



    // Helper to switch into a demo account without typing the password
    // /DemoUser/Switch?email=supervisor@sentinel-demo.local
    [HttpPost]
    public async Task<IActionResult> Switch(string email, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction("Login", "Account");

        // All demo accounts currently use password "demo"
        var user = _demoUserProvider.ValidateUser(email, "sentineldemo");
        if (user is null)
        {
            TempData["Error"] = "Unknown demo user.";
            return RedirectToAction("Login", "Account");
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Name == DbInitializer.SandboxTenantName);
        var tenantId = tenant?.Id ?? DbInitializer.SandboxTenantId;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("tenant_id", tenantId.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            IdentityConstants.ApplicationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Default: bounce to Maintenance dashboard
        return RedirectToAction("Index", "Maintenance");
    }
}
