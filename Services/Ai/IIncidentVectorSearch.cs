namespace MaintenanceSandbox.Services.Ai;

public interface IIncidentVectorSearch
{
    /// <summary>
    /// Background-safe: indexes any incidents that have not yet been embedded.
    /// Caps at 50 per call to keep latency bounded.
    /// </summary>
    Task IndexRecentAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Embeds <paramref name="queryText"/> and returns the top-<paramref name="topK"/>
    /// most similar stored incidents ranked by cosine similarity.
    /// </summary>
    Task<List<RagMatch>> SearchAsync(string queryText, Guid tenantId, int topK = 5, CancellationToken ct = default);
}

public sealed class RagMatch
{
    public int IncidentId { get; set; }
    public string TextChunk { get; set; } = "";
    public float Score { get; set; }
}
