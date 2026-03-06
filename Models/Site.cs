using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models;

public class TenantSite : TenantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Tenant Tenant { get; set; } = null!;
}
