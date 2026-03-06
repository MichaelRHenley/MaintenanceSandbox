using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.ViewModels
{
    public sealed class EquipmentCreateVm
    {
        [Required]
        public int WorkCenterId { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = "";

        [MaxLength(200)]
        public string? DisplayName { get; set; }
    }
}
