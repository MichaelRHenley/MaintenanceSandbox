using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models
{
    public class BomItem : TenantEntity
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        public int PartId { get; set; }
        public Part Part { get; set; } = null!;

        public decimal QuantityPerAsset { get; set; }
    }
}

