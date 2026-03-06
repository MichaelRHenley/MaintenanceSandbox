using System.Text;
using MaintenanceSandbox.Services.Ai;
using MaintenanceSandbox.Services;

namespace MaintenanceSandbox.Services.Onboarding;

public sealed class OnboardingAiClient : IOnboardingAiClient
{
    private readonly IAiAssistantClient _ai;

    public OnboardingAiClient(IAiAssistantClient ai)
    {
        _ai = ai;
    }

    public async Task<string> GetAreasJsonAsync(string siteName, string userText, CancellationToken ct = default)
    {
        siteName = (siteName ?? "").Trim();
        userText = (userText ?? "").Trim();

        var prompt = BuildAreasPrompt(siteName, userText);
        return await _ai.GetJsonAsync(prompt, ct);
    }

    public async Task<string> GetWorkCentersJsonAsync(string siteName, string areaName, string userText, CancellationToken ct = default)
    {
        siteName = (siteName ?? "").Trim();
        areaName = (areaName ?? "").Trim();
        userText = (userText ?? "").Trim();

        var prompt = BuildWorkCentersPrompt(siteName, areaName, userText);
        return await _ai.GetJsonAsync(prompt, ct);
    }

    private static string BuildAreasPrompt(string siteName, string userText)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are helping configure a manufacturing facility in a step-by-step onboarding wizard.");
        sb.AppendLine("Return ONLY valid JSON. No markdown. No commentary. No code fences.");
        sb.AppendLine();
        sb.AppendLine("Task: Extract the list of departments/areas for the site.");
        sb.AppendLine($"Site name: \"{siteName}\"");
        sb.AppendLine("User input (free text):");
        sb.AppendLine(userText);
        sb.AppendLine();
        sb.AppendLine("Return JSON in EXACTLY this shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"areas\": [");
        sb.AppendLine("    { \"name\": \"Packaging\" },");
        sb.AppendLine("    { \"name\": \"Mixing\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Include only areas/departments, not work centers or equipment.");
        sb.AppendLine("- Use Title Case names when appropriate.");
        sb.AppendLine("- If the user gives comma-separated items, split them.");
        sb.AppendLine("- If unclear, make best reasonable assumptions and still return JSON.");

        return sb.ToString();
    }

    private static string BuildWorkCentersPrompt(string siteName, string areaName, string userText)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are helping configure a manufacturing facility in a step-by-step onboarding wizard.");
        sb.AppendLine("Return ONLY valid JSON. No markdown. No commentary. No code fences.");
        sb.AppendLine();
        sb.AppendLine("Task: Extract work centers for ONE area/department.");
        sb.AppendLine($"Site name: \"{siteName}\"");
        sb.AppendLine($"Area name: \"{areaName}\"");
        sb.AppendLine("User input (free text):");
        sb.AppendLine(userText);
        sb.AppendLine();
        sb.AppendLine("Return JSON in EXACTLY this shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"workCenters\": [");
        sb.AppendLine("    { \"code\": \"PKG-01\", \"displayName\": \"Bulk Loader\" },");
        sb.AppendLine("    { \"code\": \"PKG-02\", \"displayName\": \"Hand Packer\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Include only work centers (code + optional displayName). Do NOT include equipment.");
        sb.AppendLine("- Codes should be stable and short. Prefer patterns like PKG-01, PKG-02 for Packaging; MIX-01, MIX-02 for Mixing.");
        sb.AppendLine("- If the user provides names only, generate codes in a sensible sequence.");
        sb.AppendLine("- Keep displayName human-friendly.");

        return sb.ToString();
    }
}
