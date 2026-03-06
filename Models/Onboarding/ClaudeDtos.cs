namespace MaintenanceSandbox.Models.Onboarding;

// Claude returns ONLY this for areas step
public sealed class AreasResponse
{
    public List<AreaItem> Areas { get; set; } = new();
}

public sealed class AreaItem
{
    public string Name { get; set; } = "";
}

// Claude returns ONLY this for workcenters step (for ONE area)
public sealed class WorkCentersResponse
{
    public List<WorkCenterItem> WorkCenters { get; set; } = new();
}

public sealed class WorkCenterItem
{
    public string Code { get; set; } = "";
    public string? DisplayName { get; set; }
}
public sealed class SiteRequestVm
{
    public string SiteName { get; set; } = "";
}
