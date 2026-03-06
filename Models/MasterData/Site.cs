using MaintenanceSandbox.Models.Base; // TenantEntity
using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.Models.MasterData
{
    public class Site : TenantEntity
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = "";

        public ICollection<Area> Areas { get; set; } = new List<Area>();
    }
}
