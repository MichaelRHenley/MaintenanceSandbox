namespace MaintenanceSandbox.Services.Onboarding;

public interface IOnboardingAiClient
{
    Task<string> GetAreasJsonAsync(string siteName, string userText, CancellationToken ct = default);
    Task<string> GetWorkCentersJsonAsync(string siteName, string areaName, string userText, CancellationToken ct = default);
}

