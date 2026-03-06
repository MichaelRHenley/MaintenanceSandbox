namespace MaintenanceSandbox.Models.Onboarding;

public sealed class OnboardingSession
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }

    public string UserId { get; set; } = "";
    public string Status { get; set; } = "InProgress";
    public string DraftJson { get; set; } = "{}";

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AppliedUtc { get; set; }      // user clicked apply
    public DateTimeOffset? OnboardedAtUtc { get; set; }  // apply fully finished
    public string? OnboardedByUserId { get; set; }
}

