using System.Security.Claims;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Demo;
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
    private readonly DemoSmsLinkTokenService _tokenService;
    private readonly IEmailService _emailService;

    public DemoUserController(
        IDemoUserProvider demoUserProvider,
        AppDbContext db,
        DemoSmsLinkTokenService tokenService,
        IEmailService emailService)
    {
        _demoUserProvider = demoUserProvider;
        _db = db;
        _tokenService = tokenService;
        _emailService = emailService;
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
            },
            new DemoUser
            {
                Name = "Maintenance Tech",
                Email = "tech@sentinel-demo.local",
                Role = "Tech"
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

    // GET /DemoUser/JoinDemo?token=... — anonymous, validates HMAC token, signs into same demo tenant
    [HttpGet]
    public async Task<IActionResult> JoinDemo(string token)
    {
        var result = _tokenService.ValidateToken(token);
        if (result is null)
        {
            TempData["Error"] = "This demo link has expired or is invalid.";
            return RedirectToAction("Index");
        }

        var (tenantId, role) = result.Value;

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null)
        {
            TempData["Error"] = "This demo session has expired. Please request a new link.";
            return RedirectToAction("Index");
        }

        var user = _demoUserProvider.GetByRole(role);
        if (user is null)
        {
            TempData["Error"] = "Invalid role in demo link.";
            return RedirectToAction("Index");
        }

        await SignInDemoUser(user, tenantId.ToString());
        return RedirectToAction("Index", "Maintenance");
    }

    // POST /DemoUser/SendEmailLink — demo session only, generates token and sends email
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmailLink([FromForm] string toEmail, [FromForm] string role)
    {
        if (!User.HasClaim("is_demo", "true"))
            return Json(new { error = "Not a demo session." });

        var tenantIdRaw = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
            return Json(new { error = "Invalid session." });

        toEmail = (toEmail ?? "").Trim();
        if (!toEmail.Contains('@'))
            return Json(new { error = "Please enter a valid email address." });

        if (role is not ("Supervisor" or "Operator" or "Tech"))
            return Json(new { error = "Invalid role." });

        var token = _tokenService.GenerateToken(tenantId, role);
        var link = Url.Action("JoinDemo", "DemoUser", new { token }, Request.Scheme)!;

        bool emailSent = false;
        string? emailError = null;
        try
        {
            await _emailService.SendAsync(
                toEmail,
                "Sentinel Demo Access",
                $"You've been invited to the Sentinel demo. Join here: {link}");
            emailSent = true;
        }
        catch (Exception ex)
        {
            emailError = ex.Message;
        }

        return Json(new { emailSent, link, emailError });
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
