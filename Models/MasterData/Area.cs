using MaintenanceSandbox.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace MaintenanceSandbox.Models.MasterData
{
    public class Area : TenantEntity
{
    public int Id { get; set; }

    [Required]
    public int SiteId { get; set; }     // ✅ int (matches Site.Id)
    public Site Site { get; set; } = null!;

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    public ICollection<MaintenanceSandbox.Models.MasterData.WorkCenter> WorkCenters { get; set; } = new List<MaintenanceSandbox.Models.MasterData.WorkCenter>();
    }
}