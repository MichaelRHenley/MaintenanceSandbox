using MaintenanceSandbox.Data;
using MaintenanceSandbox.Directory.Data;
using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Directory.Models.Tenants;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Models.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DirectoryTenant = MaintenanceSandbox.Directory.Models.Tenants.Tenant;
using DirectoryTenantSubscription = MaintenanceSandbox.Directory.Models.Tenants.TenantSubscription;
using DirectoryTenantUserRole = MaintenanceSandbox.Directory.Models.Tenants.TenantUserRole;


[Authorize]
public sealed class SubscribeController : Controller
{
    private readonly DirectoryDbContext _dir;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _appDb;
    private readonly RoleManager<IdentityRole> _roles;

    public SubscribeController(
    DirectoryDbContext dir,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext appDb,
    RoleManager<IdentityRole> roles)
    {
        _dir = dir;
        _users = users;
        _signInManager = signInManager;
        _appDb = appDb;
        _roles = roles;
    }


    [HttpGet("/subscribe")]
    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        // If no tenant yet -> show create form
        if (user.TenantId is null || user.TenantId == Guid.Empty)
            return View("Start", new SubscribeStartVm { CompanyName = user.CompanyName ?? "" });

        // Has tenant: check subscription
        var hasActive = await _dir.TenantSubscriptions
            .AnyAsync(s => s.TenantId == user.TenantId && s.IsActive);

        if (!hasActive)
            return View("Required"); // subscription required page

        // All good
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("/subscribe/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(SubscribeStartVm vm)
    {
        if (!ModelState.IsValid) return View("Start", vm);

        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (user.TenantId is not null && user.TenantId != Guid.Empty)
            return Redirect("/subscribe");

        var tenant = new DirectoryTenant
        {
            Name = vm.CompanyName.Trim(),
            Status = TenantStatus.Active
        };
        _dir.Tenants.Add(tenant);

        var sub = new DirectoryTenantSubscription
        {
            TenantId = tenant.Id,
            Tier = vm.Tier,
            BillingCadence = vm.BillingCadence,
            IsActive = true
        };
        _dir.TenantSubscriptions.Add(sub);

        user.TenantId = tenant.Id;
        user.CompanyName = tenant.Name;
        user.ProvisioningState = UserProvisioningState.Active;

        _dir.TenantUserRoles.Add(new DirectoryTenantUserRole
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.TenantOwner
        });

        await _dir.SaveChangesAsync();
        await _users.UpdateAsync(user);

        // ✅ Make tenant creator an admin (Identity role)
        await EnsureUserInRoleAsync(user, "MaintenanceAdmin");

        // If you want, you can also do this, but I recommend admin-only for now:
        // await EnsureUserInRoleAsync(user, "Supervisor");

        // await EnsureDefaultMasterDataAsync(tenant.Id);
        await _signInManager.RefreshSignInAsync(user);

        return Redirect("/onboarding");

    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roles.RoleExistsAsync(roleName))
            await _roles.CreateAsync(new IdentityRole(roleName));
    }

    private async Task EnsureUserInRoleAsync(ApplicationUser user, string roleName)
    {
        await EnsureRoleExistsAsync(roleName);

        if (!await _users.IsInRoleAsync(user, roleName))
            await _users.AddToRoleAsync(user, roleName);
    }


    private async Task EnsureDefaultMasterDataAsync(Guid tenantId)
    {
        var anySite = await _appDb.Sites.IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == tenantId);

        if (anySite) return;

        var site = new MaintenanceSandbox.Models.MasterData.Site
        {
            TenantId = tenantId,
            Name = "Default Site"
        };

        _appDb.Sites.Add(site);
        await _appDb.SaveChangesAsync(); // site.Id (int) now populated

        var area = new Area
        {
            TenantId = tenantId,
            SiteId = site.Id,          // int -> int
            Name = "Default Area"
        };
        _appDb.Areas.Add(area);
        await _appDb.SaveChangesAsync(); // area.Id (int)

        var wc = new WorkCenter
        {
            TenantId = tenantId,
            AreaId = area.Id,          // int -> int
            Code = "WC-01",
            DisplayName = "Work Center 01"
        };
        _appDb.WorkCenters.Add(wc);
        await _appDb.SaveChangesAsync(); // wc.Id (int)

        var eq = new Equipment
        {
            TenantId = tenantId,
            WorkCenterId = wc.Id,      // int -> int
            Code = "EQ-01",
            DisplayName = "Equipment 01"
        };
        _appDb.Equipment.Add(eq);

        await _appDb.SaveChangesAsync();
    }

    public IActionResult DebugTenant()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        return Content($"tenant_id claim = {tenantClaim ?? "(null)"}");
    }
    [HttpGet("/subscribe/debug")]
    public async Task<IActionResult> Debug()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Content("UserManager.GetUserAsync(User) returned null");

        return Content(
            $"Email={user.Email}\n" +
            $"TenantId={user.TenantId}\n" +
            $"ProvisioningState={user.ProvisioningState}\n" +
            $"tenant_id claim={User.FindFirst("tenant_id")?.Value ?? "(null)"}");
    }
    }
