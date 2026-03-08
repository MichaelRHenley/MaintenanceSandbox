using MaintenanceSandbox.Models.Base; // TenantEntity
using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.Models.MasterData
{
    public class WorkCenter : TenantEntity
    {
        public int Id { get; set; }

        [Required]
        public int AreaId { get; set; }
        public MaintenanceSandbox.Models.MasterData.Area Area { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Code { get; set; } = ""; // e.g., "WC-01"

        [MaxLength(200)]
        public string? DisplayName { get; set; } // e.g., "Packaging Line A"

        public ICollection<MaintenanceSandbox.Models.MasterData.Equipment> Equipment { get; set; } = new List<MaintenanceSandbox.Models.MasterData.Equipment>();
    }
}
