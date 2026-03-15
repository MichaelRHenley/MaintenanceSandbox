namespace MaintenanceSandbox.Models;

public class AiToolAudit
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    public Guid TenantId { get; set; }
    public string ToolName { get; set; } = "";
    public string ToolInputJson { get; set; } = "";
    public string ToolOutputJson { get; set; } = "";
    public bool Succeeded { get; set; }
    public DateTime CreatedUtc { get; set; }
}
