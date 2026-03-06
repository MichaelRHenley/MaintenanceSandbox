using System.Security.Claims;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaintenanceSandbox.Controllers;

[AllowAnonymous]
public class DemoUserController : Controller
{
    private readonly IDemoUserProvider _demoUserProvider;

    public DemoUserController(IDemoUserProvider demoUserProvider)
    {
        _demoUserProvider = demoUserProvider;
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
        var user = _demoUserProvider.ValidateUser(email, "demo");
        if (user is null)
        {
            TempData["Error"] = "Unknown demo user.";
            return RedirectToAction("Login", "Account");
        }

        // Same tenant as we use in AccountController
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("tenant_id", tenantId.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Default: bounce to Maintenance dashboard
        return RedirectToAction("Index", "Maintenance");
    }
}
