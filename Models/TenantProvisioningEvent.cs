namespace MaintenanceSandbox.Models;

/// <summary>
/// Append-only record of a provisioning lifecycle event for a tenant.
/// Rows must never be updated after insert.
/// </summary>
public sealed class TenantProvisioningEvent
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? Actor { get; set; }
    public string Action { get; set; } = "";
    public TenantProvisioningStatus StatusBefore { get; set; }
    public TenantProvisioningStatus StatusAfter { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationSeconds { get; set; }
    public string? CorrelationId { get; set; }
}
