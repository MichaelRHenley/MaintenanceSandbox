namespace MaintenanceSandbox.Models;

public class AiConversationSession
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    public Guid TenantId { get; set; }
    public string UserName { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public int? IncidentId { get; set; }
    public string Mode { get; set; } = "Ask";
}
