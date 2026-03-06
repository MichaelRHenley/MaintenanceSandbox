using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models
{
    public class InventoryLevel : TenantEntity
    {
        public int Id { get; set; }

        public int PartId { get; set; }
        public Part Part { get; set; } = null!;

        public int LocationBinId { get; set; }
        public LocationBin LocationBin { get; set; } = null!;

        public decimal QuantityOnHand { get; set; }

        public decimal? ReorderPoint { get; set; }
        public decimal? TargetQuantity { get; set; }
    }
}

