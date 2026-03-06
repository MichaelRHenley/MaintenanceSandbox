using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models;

public class MaintenanceMessage : TenantEntity
{
    public int Id { get; set; }

    public int MaintenanceRequestId { get; set; }
    public MaintenanceRequest? MaintenanceRequest { get; set; }

    public DateTime SentAt { get; set; }
    public string Sender { get; set; } = string.Empty;   // "Operator", "Technician", etc.
    public string Message { get; set; } = string.Empty;
}

