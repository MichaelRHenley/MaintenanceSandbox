namespace MaintenanceSandbox.Models;

public sealed class IncidentAiInsight
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int IncidentId { get; set; }
    public string Language { get; set; } = "en";
    public string InsightText { get; set; } = "";
    public string ModelUsed { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
