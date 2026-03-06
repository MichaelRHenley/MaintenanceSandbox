namespace MaintenanceSandbox.ViewModels.Admin;

public sealed class UserRowVm
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Roles { get; set; } = "";
    public bool IsActive { get; set; }
}

