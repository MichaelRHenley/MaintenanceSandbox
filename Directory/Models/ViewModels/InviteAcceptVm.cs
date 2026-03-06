using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.Directory.Models.ViewModels;

public sealed class InviteAcceptVm
{
    [Required]
    public int InviteId { get; set; }

    [Required]
    public string Token { get; set; } = "";

    public string Email { get; set; } = "";

    [Required, MinLength(2), MaxLength(100)]
    public string DisplayName { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
}
