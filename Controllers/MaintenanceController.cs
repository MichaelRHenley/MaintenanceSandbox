using MaintenanceSandbox;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Demo;
using MaintenanceSandbox.Hubs;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Models.MasterData;
using MaintenanceSandbox.Models.ViewModels;
using MaintenanceSandbox.Services;
using MaintenanceSandbox.Services.Ai;
using MaintenanceSandbox.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Area = MaintenanceSandbox.Models.MasterData.Area;
using Site = MaintenanceSandbox.Models.MasterData.Site;
using WorkCenter = MaintenanceSandbox.Models.MasterData.WorkCenter;

[Authorize]
[ServiceFilter(typeof(RequireTenantFilter))]
public class MaintenanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly IMaintenanceSuggestionService _suggestions;
    private readonly ITierProvider _tierProvider;
    private readonly IDemoUserProvider _demoUserProvider;
    private readonly IHubContext<MaintenanceHub> _maintenanceHub;
    private readonly MaintenanceAiService _ai;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private string L(string key) => _localizer[key];
    private readonly ITenantProvider _tenantProvider;
    private readonly IDemoAiRateLimiter _demoAiRateLimiter;

    public MaintenanceController(
        AppDbContext db,
        IMaintenanceSuggestionService suggestions,
        ITierProvider tierProvider,
        IDemoUserProvider demoUserProvider,
        IHubContext<MaintenanceHub> maintenanceHub,
        MaintenanceAiService ai,
        IStringLocalizer<SharedResource> localizer,
        ITenantProvider tenantProvider,
        IDemoAiRateLimiter demoAiRateLimiter)
    {
        _db = db;
        _suggestions = suggestions;
        _tierProvider = tierProvider;
        _demoUserProvider = demoUserProvider;
        _maintenanceHub = maintenanceHub;
        _ai = ai;
        _localizer = localizer;
        _tenantProvider = tenantProvider;
        _demoAiRateLimiter = demoAiRateLimiter;
    }

    // --------------------------------------------------------------------
    // INDEX
    // --------------------------------------------------------------------
    public async Task<IActionResult> Index(int? siteId, int? areaId, int? workCenterId, string? search, bool includeClosed = false)
    {
        // DB info (optional)
        var conn = _db.Database.GetDbConnection();
        ViewBag.DbServer = conn.DataSource;
        ViewBag.DbName = conn.Database;

        // Tenant debug (optional)
        var tenantId = _tenantProvider.GetTenantId();
        ViewBag._DebugTenant = tenantId.ToString();

        // -----------------------------
        // 1) Load Sites (tenant-filtered by your global filter)
        // -----------------------------
        var sites = await _db.Sites
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        if (siteId.HasValue && !sites.Any(s => s.Id == siteId.Value))
            siteId = null;

        // -----------------------------
        // 2) Load Areas (filtered by Site if selected)
        // -----------------------------
        IQueryable<Area> areasQuery = _db.Areas;

        if (siteId.HasValue)
            areasQuery = areasQuery.Where(a => a.SiteId == siteId.Value);

        var areas = await areasQuery
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync();

        if (areaId.HasValue && !areas.Any(a => a.Id == areaId.Value))
            areaId = null;

        // -----------------------------
        // 3) Load WorkCenters
        // -----------------------------
        IQueryable<WorkCenter> wcQuery = _db.WorkCenters;

        if (areaId.HasValue)
        {
            wcQuery = wcQuery.Where(w => w.AreaId == areaId.Value);
        }
        else if (siteId.HasValue)
        {
            wcQuery =
                from wc in _db.WorkCenters
                join a in _db.Areas on wc.AreaId equals a.Id
                where a.SiteId == siteId.Value
                select wc;
        }
        else
        {
            // No site/area selected: show all WCs (tenant-filtered by global filter)
            wcQuery = _db.WorkCenters;
        }

        var workCenters = await wcQuery
            .AsNoTracking()
            .OrderBy(w => w.Code)
            .ToListAsync();

        if (workCenterId.HasValue && !workCenters.Any(w => w.Id == workCenterId.Value))
            workCenterId = null;

        // -----------------------------
        // 4) Build VM + dropdown options
        // -----------------------------
        var vm = new MaintenanceIndexVm
        {
            SiteId = siteId,
            AreaId = areaId,
            WorkCenterId = workCenterId,
            Search = search,
            IncludeClosed = includeClosed,

            SiteOptions = sites.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name,
                Selected = siteId.HasValue && siteId.Value == s.Id
            }).ToList(),

            AreaOptions = areas.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = a.Name,
                Selected = areaId.HasValue && areaId.Value == a.Id
            }).ToList(),

            WorkCenterOptions = workCenters.Select(w => new SelectListItem
            {
                Value = w.Id.ToString(),
                Text = w.Code,
                Selected = workCenterId.HasValue && workCenterId.Value == w.Id
            }).ToList()
        };

        ViewBag.SiteCount = sites.Count;
        ViewBag.AreaCount = areas.Count;
        ViewBag.WcCount = workCenters.Count;

        // -----------------------------
        // 5) Requests list (apply filters)
        // -----------------------------
        IQueryable<MaintenanceRequest> reqQuery = _db.MaintenanceRequests
            .Include(r => r.Messages)
            .Include(r => r.WorkCenter)
            .Include(r => r.Equipment);

        if (!includeClosed)
            reqQuery = reqQuery.Where(r => r.Status != "Closed");

        // NOTE: you currently filter Site/Area by string fields on the request.
        // This compiles, but long-term you should add SiteId/AreaId FKs to MaintenanceRequest.

        if (areaId.HasValue)
        {
            var areaName = areas.First(a => a.Id == areaId.Value).Name;
            reqQuery = reqQuery.Where(r => r.Area == areaName);
        }

        if (workCenterId.HasValue)
        {
            reqQuery = reqQuery.Where(r => r.WorkCenterId == workCenterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            reqQuery = reqQuery.Where(r =>
                (r.Equipment != null && r.Equipment.Code.Contains(search)) ||
                (r.WorkCenter != null && r.WorkCenter.Code.Contains(search)) ||
                r.Description.Contains(search));
        }

        vm.Requests = await reqQuery
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        ViewBag.ProductTier = _tierProvider.CurrentTier;

        ViewBag.OpenCount = vm.Requests.Count(r => r.Status != "Closed" && r.Status != "Resolved");
        ViewBag.WaitingOnPartsCount = vm.Requests.Count(r => r.Status == "Waiting on Parts");
        ViewBag.ResolvedTodayCount = vm.Requests.Count(r =>
            r.Status == "Resolved" &&
            r.ResolvedAt.HasValue &&
            r.ResolvedAt.Value.Date == DateTime.UtcNow.Date);

        return View(vm);
    }

    // --------------------------------------------------------------------
    // CREATE (GET)
    // --------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Create(int? areaId, int? workCenterId, int? equipmentId, string? description = null, string? priority = null)
    {
        PopulateCommonViewData();
        var demoUser = _demoUserProvider.CurrentUser;

        var sites = await _db.Sites.AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        int? siteId = sites.Count == 1 ? sites[0].Id : (int?)null;

        IQueryable<Area> areasQuery = _db.Areas.AsNoTracking();
        if (siteId.HasValue)
            areasQuery = areasQuery.Where(a => a.SiteId == siteId.Value);

        var areas = await areasQuery.OrderBy(a => a.Name).ToListAsync();

        if (areaId.HasValue && !areas.Any(a => a.Id == areaId.Value))
            areaId = null;

        IQueryable<WorkCenter> wcQuery = _db.WorkCenters.AsNoTracking();
        wcQuery = areaId.HasValue
            ? wcQuery.Where(w => w.AreaId == areaId.Value)
            : wcQuery.Where(_ => false);

        var workCenters = await wcQuery.OrderBy(w => w.Code).ToListAsync();

        if (workCenterId.HasValue && !workCenters.Any(w => w.Id == workCenterId.Value))
            workCenterId = null;

        IQueryable<Equipment> eqQuery = _db.Equipment.AsNoTracking();
        eqQuery = workCenterId.HasValue
            ? eqQuery.Where(e => e.WorkCenterId == workCenterId.Value)
            : eqQuery.Where(_ => false);

        var equipment = await eqQuery.OrderBy(e => e.Code).ToListAsync();

        if (equipmentId.HasValue && !equipment.Any(e => e.Id == equipmentId.Value))
            equipmentId = null;

        var vm = new CreateMaintenanceRequestViewModel
        {
            SiteId = siteId,
            AreaId = areaId,
            WorkCenterId = workCenterId,
            EquipmentId = equipmentId,

            RequestedBy = demoUser.Name,
            Priority = priority ?? "Medium",
            Description = description,

            AreaOptions = areas.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = a.Name,
                Selected = areaId.HasValue && areaId.Value == a.Id
            }).ToList(),

            WorkCenterOptions = workCenters.Select(w => new SelectListItem
            {
                Value = w.Id.ToString(),
                Text = w.Code,
                Selected = workCenterId.HasValue && workCenterId.Value == w.Id
            }).ToList(),

            EquipmentOptions = equipment.Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = string.IsNullOrWhiteSpace(e.DisplayName)
                    ? e.Code
                    : $"{e.Code} — {e.DisplayName}",
                Selected = equipmentId.HasValue && equipmentId.Value == e.Id
            }).ToList()
        };

        return View(vm);
    }

    // --------------------------------------------------------------------
    // CREATE (POST)
    // --------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMaintenanceRequestViewModel model)
    {
        PopulateCommonViewData();
        var demoUser = _demoUserProvider.CurrentUser;

        if (!ModelState.IsValid)
            return await Create(model.AreaId, model.WorkCenterId, model.EquipmentId);

        var site = model.SiteId.HasValue
            ? await _db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == model.SiteId.Value)
            : null;

        var area = await _db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == model.AreaId!.Value);
        var wc = await _db.WorkCenters.AsNoTracking().FirstOrDefaultAsync(w => w.Id == model.WorkCenterId!.Value);
        var eq = await _db.Equipment.AsNoTracking().FirstOrDefaultAsync(e => e.Id == model.EquipmentId!.Value);

        if (area == null || wc == null || eq == null)
            return BadRequest("Invalid selection.");

        if (wc.AreaId != area.Id) return BadRequest("Work center does not belong to selected area.");
        if (eq.WorkCenterId != wc.Id) return BadRequest("Equipment does not belong to selected work center.");

        var request = new MaintenanceRequest
        {
            TenantId = _tenantProvider.GetTenantId(),
            Site = site?.Name ?? "",
            Area = area.Name,
            WorkCenterId = wc.Id,
            EquipmentId = eq.Id,
            Priority = model.Priority,
            RequestedBy = demoUser.Name,
            Description = model.Description!.Trim(),
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };

        _db.MaintenanceRequests.Add(request);
        await _db.SaveChangesAsync();

        await _maintenanceHub.Clients.All.SendAsync("MaintenanceListChanged");

        return RedirectToAction(nameof(Index));
    }

    // --------------------------------------------------------------------
    // DETAILS
    // --------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        PopulateCommonViewData();

        var request = await _db.MaintenanceRequests
            .Include(r => r.Messages)
            .Include(r => r.WorkCenter)
            .Include(r => r.Equipment)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        request.Messages = request.Messages
            .OrderByDescending(m => m.SentAt)
            .ToList();

        ViewBag.StatusOptions = new List<SelectListItem>
        {
            new(L("Maint_Status_New"),            "New"),
            new(L("Maint_Status_InProgress"),     "In Progress"),
            new(L("Maint_Status_WaitingOnParts"), "Waiting on Parts"),
            new(L("Maint_Status_Resolved"),       "Resolved"),
            new(L("Maint_Status_Closed"),         "Closed")
        };

        ViewBag.PriorityOptions = new List<SelectListItem>
        {
            new(L("Maint_Priority_Low"),    "Low"),
            new(L("Maint_Priority_Medium"), "Medium"),
            new(L("Maint_Priority_High"),   "High")
        };

        // Related requests (safe comparisons)
        var relatedBaseQuery = _db.MaintenanceRequests.Where(r => r.Id != request.Id);

        var sameEquipment = await relatedBaseQuery
            .Where(r => r.EquipmentId == request.EquipmentId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        var remaining = 5 - sameEquipment.Count;
        var relatedList = sameEquipment;

        if (remaining > 0)
        {
            var extra = await relatedBaseQuery
                .Where(r => r.WorkCenterId == request.WorkCenterId && r.EquipmentId != request.EquipmentId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(remaining)
                .ToListAsync();

            relatedList = sameEquipment.Concat(extra).ToList();
        }

        ViewBag.RelatedRequests = relatedList;

        var allSuggestions = _suggestions.GetSuggestions(request);
        bool isOperator = ViewBag.IsOperator is bool op && op;

        ViewBag.Suggestions = isOperator
            ? allSuggestions.Where(s => s.ForOperators).ToList()
            : allSuggestions;

        ViewBag.ProductTier = _tierProvider.CurrentTier;

        return View(request);
    }

    // --------------------------------------------------------------------
    // COMMENTS PARTIAL
    // --------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> CommentsPartial(int id)
    {
        var request = await _db.MaintenanceRequests
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        request.Messages = request.Messages
            .OrderByDescending(m => m.SentAt)
            .ToList();

        return PartialView("_CommentsList", request);
    }

    // --------------------------------------------------------------------
    // ADD COMMENT
    // --------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMessage(int maintenanceRequestId, string sender, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return RedirectToAction("Details", new { id = maintenanceRequestId });

        var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(r => r.Id == maintenanceRequestId);
        if (request == null)
            return NotFound();

        var demoUser = _demoUserProvider.CurrentUser;
        var displayName = string.IsNullOrWhiteSpace(sender) ? demoUser.Name : sender;

        // Stamp first response time when a tech or supervisor posts on an unacknowledged request
        if (!request.RespondedAt.HasValue &&
            request.Status == "New" &&
            demoUser.Role is "Tech" or "Supervisor")
        {
            request.RespondedAt = DateTime.UtcNow;
        }

        var message = new MaintenanceMessage
        {
            MaintenanceRequestId = maintenanceRequestId,
            Sender = displayName,
            Message = text,
            SentAt = DateTime.UtcNow,
            TenantId = _tenantProvider.GetTenantId()
        };

        _db.MaintenanceMessages.Add(message);
        await _db.SaveChangesAsync();

        var groupName = $"request-{maintenanceRequestId}";

        await _maintenanceHub.Clients.Group(groupName)
            .SendAsync("MaintenanceCommentAdded", new
            {
                requestId = maintenanceRequestId,
                sentAt = message.SentAt.ToLocalTime().ToString("g"),
                sender = message.Sender,
                message = message.Message
            });

        await _maintenanceHub.Clients.All.SendAsync("MaintenanceListChanged");
        await _maintenanceHub.Clients.Group(groupName).SendAsync("CommentsChanged", maintenanceRequestId);

        return RedirectToAction("Details", new { id = maintenanceRequestId });
    }

    // --------------------------------------------------------------------
    // UPDATE STATUS / PRIORITY
    // --------------------------------------------------------------------
    [Authorize(Roles = "Supervisor,Tech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRequest(int id, string status, string priority)
    {
        PopulateCommonViewData();

        var currentUser = _demoUserProvider.CurrentUser;
        if (string.Equals(currentUser.Role, "Operator", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Operators cannot change status or priority.";
            return RedirectToAction("Details", new { id });
        }

        var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request == null)
            return NotFound();

        var oldStatus = request.Status ?? "";
        var oldPriority = request.Priority ?? "";

        if (status == "Resolved")
            request.ResolvedBy = currentUser.Name;

        var currentStatus = request.Status ?? "New";

        if (!string.IsNullOrWhiteSpace(status) &&
            !RequestStatusRules.CanTransition(currentStatus, status))
        {
            TempData["Error"] = $"Invalid status change: {request.Status} → {status}";
            return RedirectToAction("Details", new { id });
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "Resolved" && request.Status != "Resolved")
                request.ResolvedAt = DateTime.UtcNow;
            else if (request.Status == "Resolved" && status != "Resolved")
                request.ResolvedAt = null;

            // Stamp first response time when someone moves the request off New
            if (!request.RespondedAt.HasValue && request.Status == "New" && status != "New")
                request.RespondedAt = DateTime.UtcNow;

            request.Status = status;
        }

        if (!string.IsNullOrWhiteSpace(priority))
            request.Priority = priority;

        await _db.SaveChangesAsync();

        if (!string.Equals(oldStatus, request.Status, StringComparison.OrdinalIgnoreCase))
            await AddSystemCommentAsync(id, $"Status changed from {oldStatus} to {request.Status} by {currentUser.Name}");

        if (!string.Equals(oldPriority, request.Priority, StringComparison.OrdinalIgnoreCase))
            await AddSystemCommentAsync(id, $"Priority changed from {oldPriority} to {request.Priority} by {currentUser.Name}");

        var groupName = $"request-{id}";

        await _maintenanceHub.Clients.All.SendAsync("MaintenanceListChanged");
        await _maintenanceHub.Clients.Group(groupName).SendAsync("CommentsChanged", id);
        await _maintenanceHub.Clients.Group(groupName)
            .SendAsync("RequestUpdated", new
            {
                requestId = id,
                status = request.Status,
                priority = request.Priority
            });

        return RedirectToAction("Details", new { id });
    }

    // --------------------------------------------------------------------
    // MARK RESOLVED BUTTON
    // --------------------------------------------------------------------
    [Authorize(Roles = "Operator,Supervisor,Tech")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkResolved(int id)
    {
        var request = await _db.MaintenanceRequests
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        var currentUser = _demoUserProvider.CurrentUser;
        var oldStatus = request.Status ?? "New";

        // Ensure RespondedAt is always set (covers direct New → Resolved jumps)
        if (!request.RespondedAt.HasValue)
            request.RespondedAt = DateTime.UtcNow;

        request.Status = "Resolved";
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedBy = currentUser.Name;

        await _db.SaveChangesAsync();

        await AddSystemCommentAsync(id, $"Status changed from {oldStatus} to Resolved by {currentUser.Name}");

        var groupName = $"request-{id}";

        await _maintenanceHub.Clients.All.SendAsync("MaintenanceListChanged");
        await _maintenanceHub.Clients.Group(groupName).SendAsync("CommentsChanged", id);
        await _maintenanceHub.Clients.Group(groupName)
            .SendAsync("RequestUpdated", new
            {
                requestId = id,
                status = request.Status,
                priority = request.Priority
            });

        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    // --------------------------------------------------------------------
    // EM DASHBOARD
    // --------------------------------------------------------------------
    [Authorize(Roles = "Supervisor")]
    [HttpGet]
    public async Task<IActionResult> EmDashboard(string? statusFilter)
    {
        PopulateCommonViewData();

        var requests = await _db.MaintenanceRequests
            .Include(r => r.Messages)
            .Include(r => r.WorkCenter)
            .Include(r => r.Equipment)
            .Where(r => r.Status != "Closed")
            .ToListAsync();

        requests = requests
            .OrderBy(r => GetEmSortOrder(r.Status))
            .ThenByDescending(r => r.CreatedAt)
            .ToList();

        ViewBag.ActiveCount        = requests.Count;
        ViewBag.MachineDownCount   = requests.Count(r => r.Status == "New");
        ViewBag.InvestigatingCount = requests.Count(r => r.Status is "In Progress" or "Waiting on Parts");
        ViewBag.RunningCount       = requests.Count(r => r.Status == "Resolved");
        ViewBag.StatusFilter       = statusFilter ?? "All";

        var responded = requests.Where(r => r.RespondedAt.HasValue).ToList();
        ViewBag.AvgResponseMinutes = responded.Count > 0
            ? Math.Round(responded.Average(r => (r.RespondedAt!.Value - r.CreatedAt).TotalMinutes), 1)
            : 0.0;

        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
        {
            requests = statusFilter switch
            {
                "Machine Down"  => requests.Where(r => r.Status == "New").ToList(),
                "Investigating" => requests.Where(r => r.Status is "In Progress" or "Waiting on Parts").ToList(),
                "Running"       => requests.Where(r => r.Status == "Resolved").ToList(),
                _               => requests
            };
        }

        return View(requests);
    }

    private static int GetEmSortOrder(string? status) => status switch
    {
        "New"              => 0,
        "In Progress"      => 1,
        "Waiting on Parts" => 2,
        "Resolved"         => 3,
        _                  => 4
    };

    private async Task AddSystemCommentAsync(int requestId, string message)
    {
        var c = new MaintenanceMessage
        {
            MaintenanceRequestId = requestId,
            Sender = "System",
            Message = message,
            SentAt = DateTime.UtcNow,
            TenantId = _tenantProvider.GetTenantId(),
        };

        _db.MaintenanceMessages.Add(c);
        await _db.SaveChangesAsync();
    }

    private void PopulateCommonViewData()
    {
        var demoUser = _demoUserProvider.CurrentUser;
        ViewBag.DemoUserName = demoUser.Name;
        ViewBag.DemoUserRole = demoUser.Role;
        ViewBag.IsOperator = string.Equals(demoUser.Role, "Operator", StringComparison.OrdinalIgnoreCase);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestFix(int id, CancellationToken ct)
    {
        if (User.HasClaim("is_demo", "true"))
        {
            var tenantId = User.FindFirstValue("tenant_id") ?? "unknown";
            if (!_demoAiRateLimiter.TryConsume(tenantId))
                return StatusCode(429, new { message = "Demo AI limit reached for this session — AI suggestions will be available again in the next hour." });
        }

        var request = await _db.MaintenanceRequests
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request == null)
            return NotFound();

        var messages = await _db.MaintenanceMessages
            .Where(m => m.MaintenanceRequestId == id)
            .ToListAsync(ct);

        var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>();
        var culture = requestCulture?.RequestCulture.UICulture.Name ?? "en";

        var suggestion = await _ai.SuggestFixAsync(request, messages, culture, ct);

        return Json(new { suggestion });
    }
}