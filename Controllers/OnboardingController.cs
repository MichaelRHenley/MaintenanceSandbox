using MaintenanceSandbox.Data;
using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Models.MasterData;
using MaintenanceSandbox.Models.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintenanceSandbox.Services.Ai;
using MaintenanceSandbox.Services.Onboarding;
using Microsoft.AspNetCore.Http;




[Authorize]
[Route("onboarding")]
public sealed class OnboardingController : Controller
{
    private const string DraftJsonKey = "Onboarding.DraftJson";

    private string GetDraftJson()
        => HttpContext.Session.GetString(DraftJsonKey) ?? "{ }";

    private void SaveDraftJson(string json)
        => HttpContext.Session.SetString(DraftJsonKey, string.IsNullOrWhiteSpace(json) ? "{ }" : json);

    private readonly AppDbContext _appDb;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IOnboardingAiService _onboardingAi;
    private readonly IOnboardingAiClient _ai;

    public OnboardingController(AppDbContext appDb, UserManager<ApplicationUser> users, IOnboardingAiService onboardingAi, IOnboardingAiClient ai)
    {
        _appDb = appDb;
        _users = users;
        _onboardingAi = onboardingAi;
        _ai = ai;
    }



    [HttpGet("/onboarding")]
    public async Task<IActionResult> Index()
    {
       
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        // If not subscribed / no tenant, send to subscribe
        if (user.TenantId is null || user.TenantId == Guid.Empty)
            return Redirect("/subscribe");

        // Ensure a session exists
        var session = await _appDb.OnboardingSessions.FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session?.AppliedUtc != null)
            return RedirectToAction("Index", "Home");

        if (session == null)
        {
            session = new OnboardingSession
            {
                UserId = user.Id,
                TenantId = user.TenantId!.Value,
                Status = "InProgress"
            };

            _appDb.OnboardingSessions.Add(session);
            await _appDb.SaveChangesAsync();
        }


        return View(session);
    }
    [HttpPost("/onboarding/message")]
    public async Task<IActionResult> Message([FromBody] OnboardingMessageVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _appDb.OnboardingSessions
            .FirstAsync(x => x.UserId == user.Id);

        // 🔴 TEMP: fake Claude response so UI flow is proven
        // We will swap this with real Claude next
        session.DraftJson = await _onboardingAi.GenerateDraftJsonAsync(vm.Message);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var draft = JsonSerializer.Deserialize<OnboardingDraft>(session.DraftJson, opts);

            if (draft?.Sites == null || draft.Sites.Count == 0)
                throw new Exception("No sites in draft.");
        }
        catch (Exception ex)
        {
            // Store the error so the UI can show it
            session.DraftJson = "";
            await _appDb.SaveChangesAsync();
            return BadRequest("AI returned invalid draft JSON: " + ex.Message);
        }

        await _appDb.SaveChangesAsync();

        return Ok(new { draftJson = session.DraftJson });

        
    }


    [HttpPost("/onboarding/apply")]
    public async Task<IActionResult> Apply()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.TenantId is null || user.TenantId == Guid.Empty)
            return BadRequest("No tenant on user.");

        var session = await _appDb.OnboardingSessions
            .FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session == null)
            return BadRequest("No onboarding session.");

        if (session.OnboardedAtUtc != null)
            return Ok(new { note = "Already onboarded", onboardedUtc = session.OnboardedAtUtc });

        if (string.IsNullOrWhiteSpace(session.DraftJson))
            return BadRequest("No draft configuration");

        await using var tx = await _appDb.Database.BeginTransactionAsync();
        // ============================
        // WRITE CONFIG TABLES HERE
        // Sites / Areas / WorkCenters / Equipment
        // ============================

        var draft = JsonSerializer.Deserialize<OnboardingDraft>(
            session.DraftJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new OnboardingDraft();

        // Basic validation
        if (draft.Sites == null || draft.Sites.Count == 0)
            return BadRequest("Draft has no sites.");

        var tenantId = user.TenantId.Value;

        // Optional: if you want “re-apply” to overwrite, you can delete existing master data first.
        // For now, we UPSERT to avoid duplicates.
        foreach (var dSite in draft.Sites)
        {
            var siteName = (dSite.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(siteName))
                continue;

            // --- SITE ---
            var site = await _appDb.Sites
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Name == siteName);

            if (site == null)
            {
                site = new Site
                {
                    TenantId = tenantId,
                    Name = siteName
                };
                _appDb.Sites.Add(site);
                await _appDb.SaveChangesAsync(); // ensures site.Id for FK usage
            }

            // --- AREAS ---
            if (dSite.Areas == null) continue;

            foreach (var dArea in dSite.Areas)
            {
                var areaName = (dArea.Name ?? "").Trim();
                if (string.IsNullOrWhiteSpace(areaName))
                    continue;

                var area = await _appDb.Areas
                    .FirstOrDefaultAsync(a =>
                        a.TenantId == tenantId &&
                        a.SiteId == site.Id &&
                        a.Name == areaName);

                if (area == null)
                {
                    area = new Area
                    {
                        TenantId = tenantId,
                        SiteId = site.Id,
                        Name = areaName
                    };
                    _appDb.Areas.Add(area);
                    await _appDb.SaveChangesAsync();
                }

                // --- WORK CENTERS ---
                if (dArea.WorkCenters == null) continue;

                foreach (var dWc in dArea.WorkCenters)
                {
                    var wcCode = (dWc.Code ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(wcCode))
                        continue;

                    var wc = await _appDb.WorkCenters
                        .FirstOrDefaultAsync(w =>
                            w.TenantId == tenantId &&
                            w.AreaId == area.Id &&
                            w.Code == wcCode);

                    if (wc == null)
                    {
                        wc = new WorkCenter
                        {
                            TenantId = tenantId,
                            AreaId = area.Id,
                            Code = wcCode,
                            DisplayName = string.IsNullOrWhiteSpace(dWc.DisplayName) ? wcCode : dWc.DisplayName.Trim()
                        };
                        _appDb.WorkCenters.Add(wc);
                        await _appDb.SaveChangesAsync();
                    }

                    // --- EQUIPMENT (optional) ---
                    if (dWc.Equipment == null) continue;

                    foreach (var eqCodeRaw in dWc.Equipment)
                    {
                        var eqCode = (eqCodeRaw ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(eqCode))
                            continue;

                        var exists = await _appDb.Equipment.AnyAsync(e =>
                            e.TenantId == tenantId &&
                            e.WorkCenterId == wc.Id &&
                            e.Code == eqCode);

                        if (!exists)
                        {
                            _appDb.Equipment.Add(new Equipment
                            {
                                TenantId = tenantId,
                                WorkCenterId = wc.Id,
                                Code = eqCode,
                                DisplayName = eqCode // or null; but this is fine for now
                            });
                        }
                    }

                    await _appDb.SaveChangesAsync();

                }
            }
        }

        // ============================
        // WRITE CONFIG TABLES HERE
        // Sites / Areas / WorkCenters / Equipment
        // ============================

        session.Status = "Applied";
        session.AppliedUtc = DateTime.UtcNow;
        session.OnboardedAtUtc = DateTime.UtcNow;

        await _appDb.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { onboardedUtc = session.OnboardedAtUtc });
    }





    [HttpPost("/onboarding/site")]
    public async Task<IActionResult> Site([FromBody] SiteRequestVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.TenantId is null || user.TenantId == Guid.Empty)
            return BadRequest("No tenant on user.");

        var siteName = (vm?.SiteName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(siteName))
            return BadRequest("siteName is required.");

        var session = await _appDb.OnboardingSessions
            .FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session == null)
            return BadRequest("No onboarding session. Visit /onboarding first.");

        // Load current draft (or create empty)
        var draft = string.IsNullOrWhiteSpace(session.DraftJson)
            ? new OnboardingDraft()
            : JsonSerializer.Deserialize<OnboardingDraft>(session.DraftJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new OnboardingDraft();

        DraftMerge.EnsureSite(draft, siteName);

        session.TenantId = user.TenantId.Value;
        session.Status = "InProgress";
        session.DraftJson = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true });

        await _appDb.SaveChangesAsync();

        return Ok(new { draftJson = session.DraftJson });
    }
    [HttpPost("/onboarding/areas")]
    public async Task<IActionResult> Areas([FromBody] AreasRequestVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.TenantId is null || user.TenantId == Guid.Empty)
            return BadRequest("No tenant on user.");

        var session = await _appDb.OnboardingSessions
            .FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session == null)
            return BadRequest("No onboarding session. Visit /onboarding first.");

        vm.SiteName = (vm.SiteName ?? "").Trim();
        vm.UserText = (vm.UserText ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vm.SiteName))
            return BadRequest("siteName is required.");
        if (string.IsNullOrWhiteSpace(vm.UserText))
            return BadRequest("userText is required.");

        var draft = string.IsNullOrWhiteSpace(session.DraftJson)
            ? new OnboardingDraft()
            : (JsonSerializer.Deserialize<OnboardingDraft>(
                    session.DraftJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new OnboardingDraft());

        DraftMerge.EnsureSite(draft, vm.SiteName);

        var json = await _ai.GetAreasJsonAsync(vm.SiteName, vm.UserText);

        AreasResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AreasResponse>(
                         json,
                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                     ?? new AreasResponse();
        }
        catch (Exception ex)
        {
            return BadRequest("AI returned invalid Areas JSON: " + ex.Message);
        }

        if (parsed.Areas.Count == 0)
            return BadRequest("AI returned zero areas.");

        DraftMerge.MergeAreas(draft, parsed.Areas);

        session.TenantId = user.TenantId.Value;
        session.Status = "InProgress";
        session.DraftJson = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true });

        await _appDb.SaveChangesAsync();

        return Ok(new { draftJson = session.DraftJson });
    }


    [HttpPost("/onboarding/workcenters")]
public async Task<IActionResult> WorkCenters([FromBody] WorkCentersRequestVm vm)
{
    var user = await _users.GetUserAsync(User);
    if (user == null) return Unauthorized();

    var session = await _appDb.OnboardingSessions
        .FirstOrDefaultAsync(x => x.UserId == user.Id);

    if (session == null)
        return BadRequest("No onboarding session. Visit /onboarding first.");

    if (user.TenantId is null || user.TenantId == Guid.Empty)
        return BadRequest("No tenant on user.");

        vm.SiteName = (vm.SiteName ?? "").Trim();
        vm.AreaName = (vm.AreaName ?? "").Trim();
        vm.UserText = (vm.UserText ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vm.SiteName))
            return BadRequest("siteName is required.");
        if (string.IsNullOrWhiteSpace(vm.AreaName))
            return BadRequest("areaName is required.");
        if (string.IsNullOrWhiteSpace(vm.UserText))
            return BadRequest("userText is required.");


        var draft = string.IsNullOrWhiteSpace(session.DraftJson)
        ? new OnboardingDraft()
        : JsonSerializer.Deserialize<OnboardingDraft>(session.DraftJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new OnboardingDraft();

    DraftMerge.EnsureSite(draft, vm.SiteName);


    var json = await _ai.GetWorkCentersJsonAsync(vm.SiteName, vm.AreaName, vm.UserText);

    WorkCentersResponse parsed;
    try
    {
        parsed = JsonSerializer.Deserialize<WorkCentersResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new WorkCentersResponse();
    }
    catch (Exception ex)
    {
        return BadRequest("AI returned invalid WorkCenters JSON: " + ex.Message);
    }

    if (parsed.WorkCenters.Count == 0)
        return BadRequest("AI returned zero work centers.");

        DraftMerge.MergeWorkCenters(draft, vm.AreaName, parsed.WorkCenters);

        session.TenantId = user.TenantId.Value;
    session.Status = "InProgress";
    session.DraftJson = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true });

    await _appDb.SaveChangesAsync();

    return Ok(new { draftJson = session.DraftJson });
}
    [HttpPost("/onboarding/reset")]
    public async Task<IActionResult> Reset()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _appDb.OnboardingSessions
            .FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session == null)
            return BadRequest("No onboarding session.");

        session.DraftJson = "{ }";
        session.Status = "InProgress";
        session.AppliedUtc = null;

        await _appDb.SaveChangesAsync();

        return Ok(new { draftJson = session.DraftJson });
    }

    [HttpPost("/onboarding/remove-area")]
    public async Task<IActionResult> RemoveArea([FromBody] RemoveAreaRequestVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.TenantId is null || user.TenantId == Guid.Empty)
            return BadRequest("No tenant on user.");

        var session = await _appDb.OnboardingSessions
            .FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session == null)
            return BadRequest("No onboarding session. Visit /onboarding first.");

        vm.SiteName = (vm.SiteName ?? "").Trim();
        vm.AreaName = (vm.AreaName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vm.SiteName))
            return BadRequest("siteName is required.");
        if (string.IsNullOrWhiteSpace(vm.AreaName))
            return BadRequest("areaName is required.");

        var draft = string.IsNullOrWhiteSpace(session.DraftJson)
            ? new OnboardingDraft()
            : (JsonSerializer.Deserialize<OnboardingDraft>(
                    session.DraftJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new OnboardingDraft());

        DraftMerge.EnsureSite(draft, vm.SiteName);

        var site = draft.Sites.FirstOrDefault();
        if (site?.Areas == null || site.Areas.Count == 0)
            return Ok(new { draftJson = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }) });

        // Remove matching area (case-insensitive)
        var removed = site.Areas.RemoveAll(a =>
            string.Equals((a.Name ?? "").Trim(), vm.AreaName, StringComparison.OrdinalIgnoreCase));

        session.DraftJson = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true });
        await _appDb.SaveChangesAsync();

        return Ok(new
        {
            draftJson = session.DraftJson,
            removed = removed
        });
    }
    [HttpDelete("/onboarding/draft/area")]
    public async Task<IActionResult> DeleteArea([FromBody] DeleteAreaVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _appDb.OnboardingSessions
            .FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session == null)
            return BadRequest("No onboarding session. Visit /onboarding first.");

        vm.SiteName = (vm.SiteName ?? "").Trim();
        vm.AreaName = (vm.AreaName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vm.SiteName)) return BadRequest("siteName is required.");
        if (string.IsNullOrWhiteSpace(vm.AreaName)) return BadRequest("areaName is required.");

        var draft = string.IsNullOrWhiteSpace(session.DraftJson)
            ? new OnboardingDraft()
            : (JsonSerializer.Deserialize<OnboardingDraft>(session.DraftJson) ?? new OnboardingDraft());

        var site = draft.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, vm.SiteName, StringComparison.OrdinalIgnoreCase));

        if (site == null) return NotFound("Site not found in draft.");

        var removed = site.Areas.RemoveAll(a =>
            string.Equals(a.Name, vm.AreaName, StringComparison.OrdinalIgnoreCase));

        if (removed == 0) return NotFound("Area not found in draft.");

        session.DraftJson = JsonSerializer.Serialize(draft);
        await _appDb.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpDelete("/onboarding/draft/workcenter")]
    public async Task<IActionResult> DeleteWorkCenter([FromBody] DeleteWorkCenterVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _appDb.OnboardingSessions
            .FirstOrDefaultAsync(x => x.UserId == user.Id);

        if (session == null)
            return BadRequest("No onboarding session. Visit /onboarding first.");

        vm.SiteName = (vm.SiteName ?? "").Trim();
        vm.AreaName = (vm.AreaName ?? "").Trim();
        vm.WorkCenterCode = (vm.WorkCenterCode ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vm.SiteName)) return BadRequest("siteName is required.");
        if (string.IsNullOrWhiteSpace(vm.AreaName)) return BadRequest("areaName is required.");
        if (string.IsNullOrWhiteSpace(vm.WorkCenterCode)) return BadRequest("workCenterCode is required.");

        var draft = string.IsNullOrWhiteSpace(session.DraftJson)
            ? new OnboardingDraft()
            : (JsonSerializer.Deserialize<OnboardingDraft>(session.DraftJson) ?? new OnboardingDraft());

        var site = draft.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, vm.SiteName, StringComparison.OrdinalIgnoreCase));
        if (site == null) return NotFound("Site not found in draft.");

        var area = site.Areas.FirstOrDefault(a =>
            string.Equals(a.Name, vm.AreaName, StringComparison.OrdinalIgnoreCase));
        if (area == null) return NotFound("Area not found in draft.");

        var removed = area.WorkCenters.RemoveAll(wc =>
            string.Equals(wc.Code, vm.WorkCenterCode, StringComparison.OrdinalIgnoreCase));

        if (removed == 0) return NotFound("Work center not found in draft.");

        session.DraftJson = JsonSerializer.Serialize(draft);
        await _appDb.SaveChangesAsync();

        return Ok(new { ok = true });
    }

}

