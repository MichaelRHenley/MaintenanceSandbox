using MaintenanceSandbox.Models.Base;
using MaintenanceSandbox.Models.MasterData;

namespace MaintenanceSandbox.Models;

public class MaintenanceRequest : TenantEntity
{
    public int Id { get; set; }
    
    public DateTime CreatedAt { get; set; }

    // Generic site / area / line info
    public string Site { get; set; } = string.Empty;        // e.g. "Plant A"
    public string Area { get; set; } = string.Empty;        // e.g. "Packaging"   
   

    // Request info
    public string Status { get; set; } = "New";             // New, In Progress, Resolved, Closed
    public string Priority { get; set; } = "Medium";        // Low, Medium, High
    public string RequestedBy { get; set; } = string.Empty; // person who reported the issue
    public string Description { get; set; } = string.Empty; // issue details
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int WorkCenterId { get; set; }
    public WorkCenter WorkCenter { get; set; } = null!;
    public int? EquipmentId { get; set; }          // nullable if optional
    public Equipment? Equipment { get; set; }      // navigation


    // Conversation
    public List<MaintenanceMessage> Messages { get; set; } = new();
}

