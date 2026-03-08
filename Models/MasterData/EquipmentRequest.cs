using MaintenanceSandbox.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.Models.MasterData
{
    public sealed class EquipmentRequest : TenantEntity
    {
        public int Id { get; set; }

        [Required]
        public int WorkCenterId { get; set; }
        public MaintenanceSandbox.Models.MasterData.WorkCenter WorkCenter { get; set; } = null!;

        [Required, MaxLength(50)]
        public string RequestedCode { get; set; } = "";   // what operator typed

        [MaxLength(200)]
        public string? RequestedDisplayName { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public string RequestedByUserId { get; set; } = "";
        [MaxLength(200)]
        public string RequestedByDisplayName { get; set; } = "";

        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

        // Admin workflow
        [MaxLength(30)]
        public string Status { get; set; } = "Pending"; // Pending/Approved/Rejected

        public DateTimeOffset? ReviewedUtc { get; set; }
        public string? ReviewedByUserId { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }

        public int? CreatedEquipmentId { get; set; } // set on approval
    }
}
