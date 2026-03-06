namespace MaintenanceSandbox.Models.Onboarding;

public sealed class AreasRequestVm
{
    public string SiteName { get; set; } = "";
    public string UserText { get; set; } = ""; // "Packaging, Mixing"
}

public sealed class WorkCentersRequestVm
{
    public string SiteName { get; set; } = "";
    public string AreaName { get; set; } = ""; // "Packaging"
    public string UserText { get; set; } = ""; // "Bulk loader, hand packer, palletizer"
}
public sealed class RemoveAreaRequestVm
{
    public string SiteName { get; set; } = "";
    public string AreaName { get; set; } = "";
}
public sealed class DeleteAreaVm
{
    public string SiteName { get; set; } = "";
    public string AreaName { get; set; } = "";
}

public sealed class DeleteWorkCenterVm
{
    public string SiteName { get; set; } = "";
    public string AreaName { get; set; } = "";
    public string WorkCenterCode { get; set; } = "";
}
