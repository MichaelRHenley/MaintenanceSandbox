using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using System.Text.Json;

namespace MaintenanceSandbox.Services.Ai;

public sealed class AiOrchestrator : IAiOrchestrator
{
    private readonly IOllamaService _ollama;
    private readonly IIncidentAiTools _tools;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AiOrchestrator> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiOrchestrator(
        IOllamaService ollama,
        IIncidentAiTools tools,
        AppDbContext db,
        IConfiguration config,
        ILogger<AiOrchestrator> logger)
    {
        _ollama = ollama;
        _tools = tools;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<AiQueryResponse> HandleAsync(
        AiQueryRequest request,
        Guid tenantId,
        string userName,
        CancellationToken ct = default)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString()
            : request.SessionId;

        var chatModel = _config["Ollama:ChatModel"] ?? "llama3.2";

        try
        {
            // Turn 1 — extract structured intent from user text
            var intentMessages = new[]
            {
                new OllamaMessage("system", PromptLibrary.IntentExtractionSystem),
                new OllamaMessage("user", request.UserText)
            };

            var intentRaw = await _ollama.ChatAsync(chatModel, intentMessages, 0.05, 250, ct);
            var intent = ParseIntent(intentRaw);

            _logger.LogInformation(
                "AI intent: {Intent} (lang: {Lang}) session: {Session}",
                intent.Intent, intent.Language, sessionId);

            // Route to tool
            AiToolResult toolResult;
            string answer;

            switch (intent.Intent)
            {
                case "SearchOpenIncidents":
                    toolResult = await _tools.SearchOpenIncidentsAsync(
                        intent.Equipment, intent.Area, intent.SinceHours, ct);
                    answer = await ComposeResponseAsync(chatModel, intent, toolResult, ct);
                    break;

                case "SearchIncidentHistory":
                    toolResult = await _tools.SearchIncidentHistoryAsync(
                        intent.Equipment, intent.Symptom, ct);
                    answer = await ComposeResponseAsync(chatModel, intent, toolResult, ct);
                    break;

                case "CreateIncidentDraft":
                    toolResult = await _tools.CreateIncidentDraftAsync(
                        intent.Equipment,
                        intent.IssueSummary ?? intent.Symptom,  // LLM sometimes uses symptom for CreateIncidentDraft
                        intent.Priority, ct);
                    answer = toolResult.Summary;
                    break;

                default:
                    toolResult = new AiToolResult { ToolName = "none", Success = true };
                    answer = await ComposeResponseAsync(chatModel, intent, toolResult, ct);
                    break;
            }

            var response = new AiQueryResponse
            {
                SessionId = sessionId,
                Answer = answer,
                DetectedLanguage = intent.Language,
                Intent = intent.Intent,
                Citations = toolResult.Citations,
                SuggestedActions = toolResult.SuggestedActions,
                Success = true
            };

            try
            {
                await LogAsync(sessionId, tenantId, userName, request, intent, toolResult, response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI audit log failed for session {Session}", sessionId);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiOrchestrator error for session {Session}", sessionId);
            return new AiQueryResponse
            {
                SessionId = sessionId,
                Success = false,
                ErrorMessage = "AI assistant is temporarily unavailable. Please try again."
            };
        }
    }

    private async Task<string> ComposeResponseAsync(
        string model,
        AiParsedIntent intent,
        AiToolResult toolResult,
        CancellationToken ct)
    {
        var context = toolResult.ToolName == "none"
            ? $"User question: {intent.NormalizedEnglish}\nOnly answer questions about incident management."
            : $"Tool result:\n{toolResult.Summary}\n\nUser asked: {intent.NormalizedEnglish}";

        var messages = new[]
        {
            new OllamaMessage("system",
                PromptLibrary.ResponseComposerSystem + $"\n\nDetected language: {intent.Language}"),
            new OllamaMessage("user", context)
        };

        return await _ollama.ChatAsync(model, messages, 0.2, 400, ct);
    }

    private static AiParsedIntent ParseIntent(string raw)
    {
        try
        {
            var json = StripCodeFences(raw.Trim());
            return JsonSerializer.Deserialize<AiParsedIntent>(json, _jsonOptions)
                ?? new AiParsedIntent();
        }
        catch
        {
            return new AiParsedIntent { Intent = "Unknown", NormalizedEnglish = raw };
        }
    }

    private static string StripCodeFences(string text)
    {
        if (text.StartsWith("```"))
        {
            var start = text.IndexOf('\n') + 1;
            var end = text.LastIndexOf("```");
            if (end > start)
                return text[start..end].Trim();
        }
        return text;
    }

    private async Task LogAsync(
        string sessionId,
        Guid tenantId,
        string userName,
        AiQueryRequest request,
        AiParsedIntent intent,
        AiToolResult toolResult,
        AiQueryResponse response)
    {
        var now = DateTime.UtcNow;

        _db.AiConversationSessions.Add(new AiConversationSession
        {
            SessionId = sessionId,
            TenantId = tenantId,
            UserName = userName,
            StartedUtc = now,
            IncidentId = request.IncidentId,
            Mode = request.Mode
        });

        _db.AiConversationMessages.Add(new AiConversationMessage
        {
            SessionId = sessionId,
            TenantId = tenantId,
            Role = "user",
            Language = intent.Language,
            OriginalText = request.UserText,
            NormalizedText = intent.NormalizedEnglish,
            CreatedUtc = now
        });

        _db.AiConversationMessages.Add(new AiConversationMessage
        {
            SessionId = sessionId,
            TenantId = tenantId,
            Role = "assistant",
            Language = intent.Language,
            OriginalText = response.Answer,
            CreatedUtc = now
        });

        if (toolResult.ToolName != "none")
        {
            _db.AiToolAudits.Add(new AiToolAudit
            {
                SessionId = sessionId,
                TenantId = tenantId,
                ToolName = toolResult.ToolName,
                ToolInputJson = JsonSerializer.Serialize(new
                {
                    intent.Equipment,
                    intent.Area,
                    intent.Symptom,
                    intent.SinceHours
                }),
                ToolOutputJson = toolResult.RawJson ?? toolResult.Summary,
                Succeeded = toolResult.Success,
                CreatedUtc = now
            });
        }

        await _db.SaveChangesAsync();
    }
}
