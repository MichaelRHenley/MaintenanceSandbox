namespace MaintenanceSandbox.Services;

public interface ITenantProvider
{
    Guid TryGetTenantId();
    Guid GetTenantId();
}
