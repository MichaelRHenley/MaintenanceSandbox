using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.ViewModels.Admin;

public sealed class CreateUserVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    public string? DisplayName { get; set; }

    [Required]
    public string Role { get; set; } = "";

    // Optional: admin-entered temp password. If blank, we generate one.
    public string? TempPassword { get; set; }

    // For dropdown rendering
    public List<string> RoleOptions { get; set; } = new();
}

