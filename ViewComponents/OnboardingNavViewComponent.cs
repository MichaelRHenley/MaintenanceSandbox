using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Directory.Models; // <-- ApplicationUser namespace

namespace MaintenanceSandbox.ViewComponents;

public sealed class OnboardingNavViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public OnboardingNavViewComponent(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var u = await _users.GetUserAsync(HttpContext.User);
        if (u == null)
            return Content(string.Empty);

        var s = await _db.OnboardingSessions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(x => x.UserId == u.Id);

        var show = (s == null || s.OnboardedAtUtc == null);
        if (!show)
            return Content(string.Empty);

        var controller = (ViewContext.RouteData.Values["Controller"]?.ToString() ?? "");
        var isActive = controller.Equals("Onboarding", StringComparison.OrdinalIgnoreCase);

        return View("Default", isActive);
    }

}
