using MaintenanceSandbox.Services; // where ITenantProvider / ITierProvider live, if you have them
using MaintenanceSandbox.Services.Ai;
using System.Text;
using MaintenanceSandbox.Models; // make sure this is at the top so Part is visible

namespace MaintenanceSandbox.Services.Ai;

public class MaintenanceAiService
{
    private readonly IChatModel _model;

    public MaintenanceAiService(IChatModel model)
    {
        _model = model;
    }

    // Simple smoke test
    public Task<string> RawTestAsync(CancellationToken ct = default)
    {
        var system = "You are the AI engine behind the MaintenanceSandbox app.";
        var user = "Say 'Maintenance AI wiring OK' in one short sentence.";
        return _model.CompleteAsync(system, user, ct);
    }


    public Task<string> EnhancePartDescriptionAsync(Part part, CancellationToken ct = default)
    {
        var systemPrompt = """
        You are the AI engine behind the Sentinel Manufacturing Suite.
        You write clear, technician-friendly descriptions of spare parts
        used in industrial plants. Do not invent specifications, part
        numbers, approvals, or manufacturer details that are not present
        in the input. Keep the output under 80 words. Use clear, practical
        language a maintenance technician would understand.
        """;

        var userMessage = $"""
        Here is the part context from Sentinel:

        - Part Number: {part.PartNumber}
        - Short Description: {part.ShortDescription ?? "(none)"}
        - Long Description: {part.LongDescription ?? "(none)"}
        - Manufacturer: {part.Manufacturer ?? "(unknown)"}
        - Manufacturer Part Number: {part.ManufacturerPartNumber ?? "(none)"}
        - Existing AI-clean description (if any): {part.AiCleanDescription ?? "(none yet)"}

        Please rewrite this into a clear, technician-friendly description.
        Mention what the part is and typical use in a plant if that is obvious
        from the text. If important information is missing, say so instead of
        guessing.
        """;

        return _model.CompleteAsync(systemPrompt, userMessage, ct);
    }

    public Task<string> SuggestFixAsync(
     MaintenanceRequest request,
     IEnumerable<MaintenanceMessage> messages,
     string culture = "en",
     CancellationToken ct = default)
    {
        // Language hint based on culture
        var languageHint = culture switch
        {
            var c when c.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
                => "Réponds en français (Canada), de manière claire et professionnelle.",
            var c when c.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                => "Responde en español de forma clara y profesional.",
            _ => "Answer in clear, professional Canadian English."
        };

        var systemPrompt = $"""
        {languageHint}
        
        You are the AI engine behind the Sentinel Manufacturing Suite.
        You respond like a senior industrial maintenance technician.

        SAFETY RULES:
        - Always put safety first.
        - Never tell the user to bypass interlocks, guards, or lockout/tagout.
        - If a step requires LOTO, clearly say so.
        - If information is missing to safely diagnose, say what extra data is needed.

        BEHAVIOR:
        - Use concise bullet points.
        - Start with quick, low-risk checks.
        - Separate "likely causes" from "things to verify".
        - If you are guessing based on patterns, say so.
        """;

        var sb = new StringBuilder();

        sb.AppendLine("Emergency Maintenance ticket details:");
        sb.AppendLine($"- EM ID: {request.Id}");
        sb.AppendLine($"- Site: {request.Site}");
        sb.AppendLine($"- Area: {request.Area}");
        sb.AppendLine($"- Work Center: {request.WorkCenter}");
        sb.AppendLine($"- Equipment: {request.Equipment}");
        sb.AppendLine($"- Priority: {request.Priority}");
        sb.AppendLine($"- Status: {request.Status}");
        sb.AppendLine($"- Operator description: {request.Description}");
        sb.AppendLine();

        sb.AppendLine("Recent comments / chat:");
        foreach (var m in messages.OrderBy(m => m.SentAt))
        {
            sb.AppendLine($"[{m.SentAt:u}] {m.Sender}: {m.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("Based on this, please provide:");
        sb.AppendLine("1) 2–5 likely root causes.");
        sb.AppendLine("2) For each cause, short diagnostic checks.");
        sb.AppendLine("3) Any obvious parts or areas to inspect (ONLY if implied by the text).");
        sb.AppendLine("4) Any critical safety warnings for this situation.");

        var userMessage = sb.ToString();

        return _model.CompleteAsync(systemPrompt, userMessage, ct);
    }

    // Later we’ll add:
    // - SuggestFixAsync(MaintenanceRequest req, ...)
    // - EnhancePartDescriptionAsync(Part part)
    // - GenerateHelpTextAsync(...)
}

