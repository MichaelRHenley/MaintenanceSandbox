using System.ComponentModel.DataAnnotations;
using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models
{
    public class LocationBin : TenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string Code { get; set; } = string.Empty; // e.g. "STO-01-02"

        [MaxLength(128)]
        public string? Name { get; set; }

        [MaxLength(64)]
        public string? Site { get; set; }
    }
}

