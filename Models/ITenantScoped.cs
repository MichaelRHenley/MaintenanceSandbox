namespace MaintenanceSandbox.Models;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
