using MaintenanceSandbox.Directory.Data;
using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Directory.Models.Tenants;
using MaintenanceSandbox.Directory.Models.ViewModels;
using MaintenanceSandbox.Directory.Services;
using MaintenanceSandbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Common;


namespace MaintenanceSandbox.Controllers;


[Authorize(Roles = "MaintenanceAdmin,Supervisor")]
public sealed class TenantUserInvitesController : Controller
{
    private readonly DirectoryDbContext _dir;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITenantProvider _tenantProvider;

    public TenantUserInvitesController(
        DirectoryDbContext dir,
        UserManager<ApplicationUser> users,
        ITenantProvider tenantProvider)
    {
        _dir = dir;
        _users = users;
        _tenantProvider = tenantProvider;
    }

    // GET: /TenantUserInvites
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenantId = _tenantProvider.GetTenantId();

        var invites = await _dir.TenantUserInvites
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedUtc)
            .ToListAsync();

        return View(invites);
    }

    // POST: /TenantUserInvites/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string role)

    {
        var tenantId = _tenantProvider.GetTenantId();

        email = (email ?? "").Trim().ToLowerInvariant();
        role = (role ?? "").Trim();

        if (!Enum.TryParse<TenantRole>(role, ignoreCase: true, out var parsedRole))
        {
            TempData["err"] = "Invalid role.";
            return RedirectToAction(nameof(Index));
        }


        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
        {
            TempData["err"] = "Email and role are required.";
            return RedirectToAction(nameof(Index));
        }

        var token = MaintenanceSandbox.Directory.Services.InviteToken.GenerateToken();
        var tokenHash = MaintenanceSandbox.Directory.Services.InviteToken.HashToken(token);


        var invite = new TenantUserInvite
        {
            TenantId = tenantId,
            Email = email,
            Role = parsedRole,
            TokenHash = tokenHash,
            Status = "Pending",
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };


        _dir.TenantUserInvites.Add(invite);
        await _dir.SaveChangesAsync();

        // Build link (v1: show it; later: email it)
        var link = Url.Action("Accept", "TenantUserInvites",
            new { inviteId = invite.Id, token = token }, Request.Scheme);

        TempData["ok"] = $"Invite created. Link: {link}";
        return RedirectToAction(nameof(Index));
    }

    // Allow anonymous acceptance
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Accept(int inviteId, string token)
    {
        var invite = await _dir.TenantUserInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == inviteId);

        if (invite == null) return NotFound();

        if (invite.Status != "Pending") return BadRequest("Invite is not pending.");
        if (invite.ExpiresUtc < DateTimeOffset.UtcNow) return BadRequest("Invite expired.");

        var hash = MaintenanceSandbox.Directory.Services.InviteToken.HashToken(token ?? "");

        if (!string.Equals(hash, invite.TokenHash, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid token.");

        var vm = new InviteAcceptVm
        {
            InviteId = invite.Id,
            Token = token ?? "",
            Email = invite.Email
        };

        return View(vm);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(InviteAcceptVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var invite = await _dir.TenantUserInvites
            .FirstOrDefaultAsync(i => i.Id == vm.InviteId);

        if (invite == null) return NotFound();
        if (invite.Status != "Pending") return BadRequest("Invite is not pending.");
        if (invite.ExpiresUtc < DateTimeOffset.UtcNow) return BadRequest("Invite expired.");

        var hash = InviteToken.HashToken(vm.Token ?? "");


        if (!string.Equals(hash, invite.TokenHash, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid token.");

        // Create user (Identity user id is STRING; do not use int anywhere here)
        var existing = await _users.FindByEmailAsync(invite.Email);

        if (existing != null)
        {
            // Optional policy:
            // - if existing user has a different TenantId -> block
            if (existing.TenantId.HasValue && existing.TenantId != invite.TenantId)
                return BadRequest("User already belongs to a different tenant.");
        }

        var user = existing ?? new ApplicationUser
        {
            UserName = invite.Email,
            Email = invite.Email,
        };

        user.TenantId = invite.TenantId;
        user.DisplayName = vm.DisplayName.Trim();
        user.ProvisioningState = UserProvisioningState.Active;

        IdentityResult createResult;

        if (existing == null)
        {
            createResult = await _users.CreateAsync(user, vm.Password);
        }
        else
        {
            // Existing user: set password if none or allow reset (v1 simplest: require new user)
            return BadRequest("User already exists. (V1 policy: only new users can accept invites.)");
        }

        if (!createResult.Succeeded)
        {
            foreach (var e in createResult.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        // Record tenant role (your directory table)
        _dir.TenantUserRoles.Add(new TenantUserRole
        {
            TenantId = invite.TenantId,
            UserId = user.Id,
            Role = invite.Role, // use the role stored on the invite
        });


        invite.Status = "Accepted";
        invite.AcceptedUtc = DateTimeOffset.UtcNow;

        await _dir.SaveChangesAsync();

        TempData["ok"] = "Invite accepted. You can sign in now.";
        return Redirect("/Identity/Account/Login");
    }
}
