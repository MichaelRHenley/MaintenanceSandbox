namespace MaintenanceSandbox.Models;

public sealed class IncidentEmbedding
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int IncidentId { get; set; }

    /// <summary>Plain-text chunk that was embedded (equipment + area + status + description).</summary>
    public string TextChunk { get; set; } = "";

    /// <summary>JSON-serialized float[] from the Ollama embedding model.</summary>
    public string EmbeddingJson { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
