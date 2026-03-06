namespace MaintenanceSandbox.Models.Base;

public abstract class TenantEntity
{
    public Guid TenantId { get; set; }
    public bool IsOnboarded { get; set; }  // default false
    public DateTimeOffset? OnboardedAtUtc { get; set; }
    public string? OnboardedByUserId { get; set; }

}


