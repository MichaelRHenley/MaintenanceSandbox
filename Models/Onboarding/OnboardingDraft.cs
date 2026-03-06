namespace MaintenanceSandbox.Models.Onboarding;

public sealed class OnboardingDraft
{
    public List<OnboardingDraftSite> Sites { get; set; } = new();
}

public sealed class OnboardingDraftSite
{
    public string Name { get; set; } = "";
    public List<OnboardingDraftArea> Areas { get; set; } = new();
}

public sealed class OnboardingDraftArea
{
    public string Name { get; set; } = "";
    public List<OnboardingDraftWorkCenter> WorkCenters { get; set; } = new();
}

public sealed class OnboardingDraftWorkCenter
{
    public string Code { get; set; } = "";
    public List<string> Equipment { get; set; } = new();
    public string? DisplayName { get; set; }
}
