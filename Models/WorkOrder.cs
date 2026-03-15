using MaintenanceSandbox.Models.Base;

namespace MaintenanceSandbox.Models;

public class WorkOrder : TenantEntity
{
    public int Id { get; set; }

    /// <summary>e.g. WO-2024-0042 — generated after INSERT</summary>
    public string WorkOrderNumber { get; set; } = string.Empty;

    public int MaintenanceRequestId { get; set; }
    public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

    /// <summary>Open | In Progress | Complete | Cancelled</summary>
    public string Status { get; set; } = "Open";

    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
