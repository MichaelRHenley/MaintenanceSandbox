namespace MaintenanceSandbox.Services.Ai;

public interface IOllamaService
{
    Task<string> ChatAsync(
        string model,
        IEnumerable<OllamaMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);

    Task<float[]> EmbedAsync(string model, string text, CancellationToken ct = default);
}
