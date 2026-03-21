using System.Text.Json.Serialization;

namespace MaintenanceSandbox.Services.Ai;

public sealed class AiQueryRequest
{
    public string UserText { get; set; } = "";
    public string? SessionId { get; set; }
    public int? IncidentId { get; set; }
    public string Mode { get; set; } = "Ask"; // Ask | Command | Troubleshoot
}

public sealed class AiQueryResponse
{
    public string SessionId { get; set; } = "";
    public string Answer { get; set; } = "";
    public string DetectedLanguage { get; set; } = "en";
    public string Intent { get; set; } = "";
    public List<string> Citations { get; set; } = new();
    public List<AiSuggestedAction> SuggestedActions { get; set; } = new();
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public sealed class AiSuggestedAction
{
    public string Label { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Payload { get; set; }
}

public sealed class AiParsedIntent
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("normalizedEnglish")]
    public string NormalizedEnglish { get; set; } = "";

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "Unknown";

    [JsonPropertyName("equipment")]
    public string? Equipment { get; set; }

    [JsonPropertyName("workCenter")]
    public string? WorkCenter { get; set; }

    [JsonPropertyName("area")]
    public string? Area { get; set; }

    [JsonPropertyName("site")]
    public string? Site { get; set; }

    [JsonPropertyName("symptom")]
    public string? Symptom { get; set; }

    [JsonPropertyName("sinceHours")]
    public int? SinceHours { get; set; }

    [JsonPropertyName("issueSummary")]
    public string? IssueSummary { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
}

public sealed class AiToolResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Citations { get; set; } = new();
    public List<AiSuggestedAction> SuggestedActions { get; set; } = new();
    public string? RawJson { get; set; }
}

public sealed record OllamaMessage(string Role, string Content);
