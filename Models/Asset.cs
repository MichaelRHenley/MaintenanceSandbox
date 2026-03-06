using System.ComponentModel.DataAnnotations;
using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models
{
    public class Asset : TenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string AssetCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? WorkCenter { get; set; }

        public ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();
    }
}

