namespace MaintenanceSandbox.Models;

public enum TenantProvisioningStatus
{
    Pending = 0,
    Provisioning = 1,
    Ready = 2,
    Failed = 3,
    Suspended = 4
}
