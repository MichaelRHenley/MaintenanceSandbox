using MaintenanceSandbox.Services;

namespace MaintenanceSandbox.Services.Ai;

public sealed class KnowledgeContextProvider : IKnowledgeContextProvider
{
    private readonly IIncidentVectorSearch _vectorSearch;
    private readonly ITenantProvider _tenantProvider;

    public KnowledgeContextProvider(IIncidentVectorSearch vectorSearch, ITenantProvider tenantProvider)
    {
        _vectorSearch = vectorSearch;
        _tenantProvider = tenantProvider;
    }

    public async Task<List<AiKnowledgeHit>> SearchAsync(AiParsedIntent intent, string userQuery, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TryGetTenantId();
        if (tenantId == Guid.Empty) return new();

        var matches = await _vectorSearch.SearchAsync(userQuery, tenantId, 5, ct);

        return matches
            .Select(m => new AiKnowledgeHit
            {
                SourceType = "incident",
                SourceId = m.IncidentId.ToString(),
                Text = m.TextChunk,
                Score = m.Score
            })
            .ToList();
    }
}
