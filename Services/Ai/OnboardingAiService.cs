using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MaintenanceSandbox.Services.Ai;

public sealed class OnboardingAiService : IOnboardingAiService
{
    private readonly IChatModel _chat;

    public OnboardingAiService(IChatModel chat)
    {
        _chat = chat;
    }

    public async Task<string> GenerateDraftJsonAsync(string facilityDescription, CancellationToken ct = default)
    {
        // System: hard rules
        var system = """
You are an onboarding assistant for a manufacturing maintenance app.
Return ONLY valid JSON (no markdown, no commentary).
The JSON MUST match this schema exactly:

{
  "sites": [
    {
      "name": "string",
      "areas": [
        {
          "name": "string",
          "workCenters": [
            { "code": "string", "equipment": ["string"] }
          ]
        }
      ]
    }
  ]
}

Rules:
- Keep names short and business-appropriate.
- workCenters[].code should be uppercase with dashes, like "PKG-01".
- equipment entries should be short codes like "CONV-01", "LBL-01".
- If information is missing, make reasonable defaults (1 site, 1-3 areas, 1-5 workcenters each).
Return JSON only.
""";

        // User prompt
        var user = new StringBuilder()
            .AppendLine("Facility description:")
            .AppendLine(facilityDescription.Trim())
            .AppendLine()
            .AppendLine("Generate the JSON draft configuration now.")
            .ToString();

        // Call Claude through your abstraction
        // IMPORTANT: your IChatModel likely already supports system+user.
        // If not, implement a single method on your ClaudeChatModel that accepts system+user.
        var json = await _chat.CompleteAsync(system, user, ct);

        // Safety: trim leading/trailing whitespace
        return json.Trim();
    }
}
