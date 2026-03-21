using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Services.Ai;

public sealed class AiIncidentInsightService : IAiIncidentInsightService
{
    private readonly AppDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiPromptComposer _promptComposer;
    private readonly IOllamaService _ollama;
    private readonly IIncidentVectorSearch _vectorSearch;
    private readonly IConfiguration _config;
    private readonly ILogger<AiIncidentInsightService> _logger;

    public AiIncidentInsightService(
        AppDbContext db,
        IAiContextBuilder contextBuilder,
        IAiPromptComposer promptComposer,
        IOllamaService ollama,
        IIncidentVectorSearch vectorSearch,
        IConfiguration config,
        ILogger<AiIncidentInsightService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _promptComposer = promptComposer;
        _ollama = ollama;
        _vectorSearch = vectorSearch;
        _config = config;
        _logger = logger;
    }

    public async Task<IncidentAiInsight?> GetOrGenerateAsync(
        int incidentId,
        Guid tenantId,
        bool force,
        string language,
        CancellationToken ct)
    {
        if (!force)
        {
            var cached = await _db.IncidentAiInsights
                .FirstOrDefaultAsync(i => i.IncidentId == incidentId && i.TenantId == tenantId && i.Language == language, ct);

            if (cached != null) return cached;
        }

        // AppDbContext global filter handles tenant scoping for MaintenanceRequest.
        var incident = await _db.MaintenanceRequests
            .Include(r => r.Equipment)
            .Include(r => r.WorkCenter)
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == incidentId, ct);

        if (incident is null) return null;

        // Background indexing — non-blocking.
        _ = _vectorSearch.IndexRecentAsync(tenantId, CancellationToken.None)
            .ContinueWith(
                t => _logger.LogWarning(t.Exception, "Background RAG indexing failed for incident insight"),
                TaskContinuationOptions.OnlyOnFaulted);

        var equipmentName = incident.Equipment?.DisplayName ?? incident.Equipment?.Code;

        // Pre-populate intent directly from the incident — no LLM round-trip needed.
        var intent = new AiParsedIntent
        {
            Intent = "Troubleshoot",
            Language = language,
            NormalizedEnglish = incident.Description,
            Equipment = equipmentName,
            WorkCenter = incident.WorkCenter?.DisplayName ?? incident.WorkCenter?.Code,
            Symptom = incident.Description
        };

        var request = new AiQueryRequest
        {
            Mode = "Troubleshoot",
            UserText = incident.Description,
            IncidentId = incidentId
        };

        var context = await _contextBuilder.BuildAsync(request, intent, ct);
        var prompt = _promptComposer.Compose(context, intent);

        var chatModel = _config["Ollama:ChatModel"] ?? "llama3.2";

        var messages = new[]
        {
            new OllamaMessage("system", PromptLibrary.TroubleshootSystem),
            new OllamaMessage("user", prompt)
        };

        var insightText = await _ollama.ChatAsync(chatModel, messages, 0.3, 600, ct);

        if (force)
        {
            var old = await _db.IncidentAiInsights
                .FirstOrDefaultAsync(i => i.IncidentId == incidentId && i.TenantId == tenantId && i.Language == language, ct);

            if (old is not null) _db.IncidentAiInsights.Remove(old);
        }

        var insight = new IncidentAiInsight
        {
            TenantId = tenantId,
            IncidentId = incidentId,
            Language = language,
            InsightText = insightText,
            ModelUsed = chatModel,
            CreatedUtc = DateTime.UtcNow
        };

        _db.IncidentAiInsights.Add(insight);
        await _db.SaveChangesAsync(ct);

        return insight;
    }
}
