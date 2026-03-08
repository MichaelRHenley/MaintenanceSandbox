using MaintenanceSandbox.Directory.Data;
using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Directory.Models.Tenants;
using MaintenanceSandbox.Directory.Services;
using MaintenanceSandbox.Filters;
using MaintenanceSandbox.Services;
using MaintenanceSandbox.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using AdminInviteUserVm = MaintenanceSandbox.ViewModels.Admin.InviteUserVm;

namespace MaintenanceSandbox.Controllers;

[Authorize(Roles = "MaintenanceAdmin,Supervisor")]
[ServiceFilter(typeof(RequireTenantFilter))]
public sealed class UserInvitesAdminController : Controller
{
    private readonly DirectoryDbContext _dir;
    private readonly ITenantProvider _tenantProvider;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IEmailSender _email;

    public UserInvitesAdminController(
        DirectoryDbContext dir,
        ITenantProvider tenantProvider,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        IEmailSender email)
    {
        _dir = dir;
        _tenantProvider = tenantProvider;
        _users = users;
        _roles = roles;
        _email = email;
    }

    [HttpGet]
    [ServiceFilter(typeof(BlockDemoFilter))]
    public async Task<IActionResult> Invite()
    {
        var vm = new InviteUserVm
        {
            RoleOptions = await GetRoleOptionsAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(BlockDemoFilter))]
    public async Task<IActionResult> Invite(InviteUserVm vm)
    {
        vm.RoleOptions = await GetRoleOptionsAsync();

        if (!ModelState.IsValid) return View(vm);

        var tenantId = _tenantProvider.GetTenantId();

        var email = (vm.Email ?? "").Trim().ToLowerInvariant();
        var role = (vm.Role ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(nameof(vm.Email), "Email is required.");
            return View(vm);
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            ModelState.AddModelError(nameof(vm.Role), "Role is required.");
            return View(vm);
        }

        if (!vm.RoleOptions.Contains(role))
        {
            ModelState.AddModelError(nameof(vm.Role), "Invalid role.");
            return View(vm);
        }

        // optional: prevent duplicates
        var existing = await _dir.TenantUserInvites
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Email == email && x.ExpiresUtc > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            TempData["ok"] = "An active invite already exists for that email.";
            return RedirectToAction("Index", "UsersAdmin");
        }

        // Create token (raw token emailed, hash stored)
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var tokenHash = HashToken(rawToken);

        var invite = new TenantUserInvite
        {
            TenantId = tenantId, // <-- keep this the same type as TenantId in your model (see note below)
            Email = email,
            Role = Enum.Parse<TenantRole>(role, ignoreCase: true),
            TokenHash = tokenHash,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(48),
            CreatedByUserId = _users.GetUserId(User) ?? "" // Identity user id is string by default
        };

        _dir.TenantUserInvites.Add(invite);
        await _dir.SaveChangesAsync();

        var acceptUrl = Url.Action("Accept", "Invite", new { token = rawToken }, Request.Scheme)!;

        await _email.SendEmailAsync(
            email,
            "You’ve been invited",
            $"You’ve been invited to join. Set your password here: {acceptUrl}"
        );

        TempData["ok"] = "Invite sent.";
        return RedirectToAction("Index", "UsersAdmin");
    }

    private static string HashToken(string rawToken)
    {
        // Store a one-way hash of the token (safer than storing raw)
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes); // .NET 5+
    }

    private async Task<List<string>> GetRoleOptionsAsync()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Operator", "Supervisor", "MaintenanceAdmin"
        };

        var roles = await _roles.Roles
            .Select(r => r.Name!)
            .OrderBy(n => n)
            .ToListAsync();

        return roles.Where(r => allowed.Contains(r)).ToList();
    }
}