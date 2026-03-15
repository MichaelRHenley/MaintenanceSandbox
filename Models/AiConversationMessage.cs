namespace MaintenanceSandbox.Models;

public class AiConversationMessage
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    public Guid TenantId { get; set; }
    public string Role { get; set; } = ""; // user | assistant | tool
    public string? Language { get; set; }
    public string OriginalText { get; set; } = "";
    public string? NormalizedText { get; set; }
    public DateTime CreatedUtc { get; set; }
}
