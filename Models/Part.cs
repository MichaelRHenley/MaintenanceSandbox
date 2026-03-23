using System.ComponentModel.DataAnnotations;
using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models
{
    public class Part : TenantEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string PartNumber { get; set; } = string.Empty;

        [MaxLength(128)]
        public string? ShortDescription { get; set; }

        [MaxLength(512)]
        public string? LongDescription { get; set; }

        // AI-cleaned, customer-friendly description
        [MaxLength(512)]
        public string? AiCleanDescription { get; set; }

        [MaxLength(128)]
        public string? Manufacturer { get; set; }

        [MaxLength(128)]
        public string? ManufacturerPartNumber { get; set; }

        // External URL to a manufacturer-hosted or CDN-hosted product image.
        // Never store image binaries here — only URL references.
        // Future: Azure Blob Storage / CDN / multi-image gallery per part.
        public string? ManufacturerImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<InventoryLevel> InventoryLevels { get; set; } = new List<InventoryLevel>();
        public ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();

        public string? EnhancedDescription { get; set; }
    }
}
