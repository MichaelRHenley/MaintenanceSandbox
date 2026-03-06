using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.ViewModels.Admin;

public sealed class InviteUserVm
{
    [Required, EmailAddress]
    public string? Email { get; set; }

    [Required]
    public string? Role { get; set; }

    // for dropdown
    public List<string> RoleOptions { get; set; } = new();
}