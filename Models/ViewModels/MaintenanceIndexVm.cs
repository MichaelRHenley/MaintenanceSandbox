using Microsoft.AspNetCore.Mvc.Rendering;

namespace MaintenanceSandbox.Models.ViewModels;

public sealed class MaintenanceIndexVm
{
    // Selected values (from querystring)
    public int? SiteId { get; set; }   // ✅ not Guid?
    public int? AreaId { get; set; }
    public int? WorkCenterId { get; set; }
    public string? Search { get; set; }
    public bool IncludeClosed { get; set; }

    // Dropdown options
    public List<SelectListItem> SiteOptions { get; set; } = new();
    public List<SelectListItem> AreaOptions { get; set; } = new();
    public List<SelectListItem> WorkCenterOptions { get; set; } = new();

    // Results (your cards)
    public List<MaintenanceRequest> Requests { get; set; } = new();
}

