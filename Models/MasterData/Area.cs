using MaintenanceSandbox.Models.Base;
using MaintenanceSandbox.Models.MasterData;
using System.ComponentModel.DataAnnotations;

public class Area : TenantEntity
{
    public int Id { get; set; }

    [Required]
    public int SiteId { get; set; }     // ✅ int (matches Site.Id)
    public Site Site { get; set; } = null!;

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    public ICollection<WorkCenter> WorkCenters { get; set; } = new List<WorkCenter>();
}