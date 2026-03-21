using MaintenanceSandbox.Data;
using MaintenanceSandbox.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MaintenanceSandbox.Services.Ai;

public sealed class IncidentVectorSearch : IIncidentVectorSearch
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOllamaService _ollama;
    private readonly IConfiguration _config;
    private readonly ILogger<IncidentVectorSearch> _logger;

    private const int IndexBatchSize = 50;

    public IncidentVectorSearch(
        IDbContextFactory<AppDbContext> dbFactory,
        IOllamaService ollama,
        IConfiguration config,
        ILogger<IncidentVectorSearch> logger)
    {
        _dbFactory = dbFactory;
        _ollama = ollama;
        _config = config;
        _logger = logger;
    }

    public async Task IndexRecentAsync(Guid tenantId, CancellationToken ct = default)
    {
        var model = _config["Ollama:EmbeddingModel"] ?? "mxbai-embed-large";

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var indexedIds = await db.IncidentEmbeddings
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.IncidentId)
            .ToListAsync(ct);

        var unindexed = await db.MaintenanceRequests
            .Include(r => r.Equipment)
            .Where(r => !indexedIds.Contains(r.Id))
            .OrderByDescending(r => r.CreatedAt)
            .Take(IndexBatchSize)
            .ToListAsync(ct);

        if (unindexed.Count == 0) return;

        foreach (var incident in unindexed)
        {
            try
            {
                var text = BuildChunk(incident);
                var vector = await _ollama.EmbedAsync(model, text, ct);

                db.IncidentEmbeddings.Add(new IncidentEmbedding
                {
                    TenantId = tenantId,
                    IncidentId = incident.Id,
                    TextChunk = text,
                    EmbeddingJson = JsonSerializer.Serialize(vector),
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to embed incident #{Id}", incident.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("RAG: indexed {Count} incident(s) for tenant {Tenant}", unindexed.Count, tenantId);
    }

    public async Task<List<RagMatch>> SearchAsync(
        string queryText, Guid tenantId, int topK = 5, CancellationToken ct = default)
    {
        var model = _config["Ollama:EmbeddingModel"] ?? "mxbai-embed-large";

        var queryVector = await _ollama.EmbedAsync(model, queryText, ct);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var stored = await db.IncidentEmbeddings
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(ct);

        if (stored.Count == 0) return new List<RagMatch>();

        return stored
            .Select(e =>
            {
                var v = JsonSerializer.Deserialize<float[]>(e.EmbeddingJson) ?? Array.Empty<float>();
                return new RagMatch
                {
                    IncidentId = e.IncidentId,
                    TextChunk = e.TextChunk,
                    Score = CosineSimilarity(queryVector, v)
                };
            })
            .OrderByDescending(m => m.Score)
            .Take(topK)
            .ToList();
    }

    private static string BuildChunk(Models.MaintenanceRequest r)
    {
        var equipment = r.Equipment?.DisplayName ?? r.Equipment?.Code ?? "Unknown";
        return $"Equipment: {equipment}\nArea: {r.Area}\nStatus: {r.Status}\nPriority: {r.Priority}\nIssue: {r.Description}";
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;

        var dot = 0f;
        var normA = 0f;
        var normB = 0f;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return (normA == 0f || normB == 0f) ? 0f
            : dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
