using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Filters;
using MaintenanceSandbox.Services;
using MaintenanceSandbox.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Controllers;

[Authorize(Roles = "Supervisor,MaintenanceAdmin")]
[ServiceFilter(typeof(RequireTenantFilter))]
public sealed class UsersAdminController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ITenantProvider _tenantProvider;

    public UsersAdminController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        ITenantProvider tenantProvider)
    {
        _users = users;
        _roles = roles;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenantId = _tenantProvider.GetTenantId();

        var list = await _users.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Email)
            .Select(u => new UserRowVm
            {
                UserId = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.UserName ?? "",
                IsActive = !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.UtcNow
            })
            .ToListAsync();

        foreach (var u in list)
        {
            var user = await _users.FindByIdAsync(u.UserId);
            u.Roles = user == null ? "" : string.Join(", ", await _users.GetRolesAsync(user));
        }

        return View(list);
    }

    [HttpGet]
    [ServiceFilter(typeof(BlockDemoFilter))]
    public async Task<IActionResult> Create()
    {
        var vm = new CreateUserVm
        {
            RoleOptions = await GetRoleOptionsAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(BlockDemoFilter))]
    public async Task<IActionResult> Create(CreateUserVm vm)
    {
        vm.RoleOptions = await GetRoleOptionsAsync();

        if (!ModelState.IsValid)
            return View(vm);

        var tenantId = _tenantProvider.GetTenantId();

        var email = (vm.Email ?? "").Trim();
        var displayName = (vm.DisplayName ?? "").Trim();
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

        if (!await _roles.RoleExistsAsync(role))
        {
            ModelState.AddModelError(nameof(vm.Role), "Selected role does not exist.");
            return View(vm);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            TenantId = tenantId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName
        };

        var tempPassword = string.IsNullOrWhiteSpace(vm.TempPassword)
            ? "Sentinel!" + Guid.NewGuid().ToString("N")[..8] + "a1"
            : vm.TempPassword;

        var result = await _users.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        await _users.AddToRoleAsync(user, role);

        TempData["ok"] = $"Created {email} as {role}. Temporary password: {tempPassword}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string id)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (user.TenantId != tenantId) return Forbid();

        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));

        TempData["ok"] = $"Deactivated {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(string id)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (user.TenantId != tenantId) return Forbid();

        await _users.SetLockoutEndDateAsync(user, null);

        TempData["ok"] = $"Reactivated {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> GetRoleOptionsAsync()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Operator", "Tech", "Supervisor", "MaintenanceAdmin"
        };

        var roles = await _roles.Roles
            .Select(r => r.Name!)
            .OrderBy(n => n)
            .ToListAsync();

        return roles.Where(r => allowed.Contains(r)).ToList();
    }
}