using MaintenanceSandbox.Models.Base; // TenantEntity
using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.Models.MasterData
{
    public class Equipment : TenantEntity
    {
        public int Id { get; set; }

        [Required]
        public int WorkCenterId { get; set; }
        public WorkCenter WorkCenter { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Code { get; set; } = ""; // e.g., "EQ-1001"

        [MaxLength(200)]
        public string? DisplayName { get; set; } // e.g., "Conveyor Belt"
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public string? CreatedByUserId { get; set; }
    }
}
