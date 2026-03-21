using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaintenanceSandbox.Services.Ai;

public sealed class OllamaService : IOllamaService
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(HttpClient http, ILogger<OllamaService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> ChatAsync(
        string model,
        IEnumerable<OllamaMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken ct = default)
    {
        var payload = new OllamaChatPayload(
            model,
            messages.Select(m => new OllamaMessagePayload(m.Role, m.Content)),
            false,
            new OllamaOptionsPayload(temperature, maxTokens));

        using var response = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        _logger.LogDebug("Ollama chat ({Model}): {Preview}",
            model, content.Length > 120 ? content[..120] + "…" : content);

        return content;
    }

    private sealed record OllamaChatPayload(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IEnumerable<OllamaMessagePayload> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaOptionsPayload Options);

    private sealed record OllamaMessagePayload(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaOptionsPayload(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    public async Task<float[]> EmbedAsync(string model, string text, CancellationToken ct = default)
    {
        var payload = new { model, prompt = text };
        using var response = await _http.PostAsJsonAsync("/api/embeddings", payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(e => e.GetSingle())
            .ToArray();
    }
}
