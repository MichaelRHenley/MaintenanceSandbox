using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MaintenanceSandbox.Controllers;

public class AccountController : Controller
{
    private readonly IDemoUserProvider _demoUserProvider;
    private readonly AppDbContext _db;

    public AccountController(IDemoUserProvider demoUserProvider, AppDbContext db)
    {
        _demoUserProvider = demoUserProvider;
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = _demoUserProvider.ValidateUser(model.Email, model.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Name == DbInitializer.SandboxTenantName);
        var tenantId = tenant?.Id ?? DbInitializer.SandboxTenantId;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Role),        // use Role claim type
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

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
