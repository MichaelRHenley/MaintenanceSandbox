using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MaintenanceSandbox.Services.Ai;

/// <summary>
/// IChatModel backed by a local Ollama instance.
/// Requires Ollama running at Ai:BaseUrl (default http://localhost:11434)
/// with the model named in Ai:Model already pulled (e.g. "llama3.2").
/// </summary>
public sealed class OllamaChatModel : IChatModel
{
    private readonly HttpClient _http;
    private readonly AiOptions _opts;
    private readonly ILogger<OllamaChatModel> _logger;

    public OllamaChatModel(HttpClient http, IOptions<AiOptions> opts, ILogger<OllamaChatModel> logger)
    {
        _http   = http;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                model  = _opts.Model,
                stream = false,
                options = new
                {
                    temperature  = _opts.Temperature,
                    num_predict  = _opts.MaxTokens
                },
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userMessage  }
                }
            };

            var response = await _http.PostAsJsonAsync("/api/chat", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Ollama API error: {StatusCode} - {Error}",
                    response.StatusCode, error);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(
                cancellationToken: ct);

            return result?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama API call failed — AI suggestions unavailable");
            return string.Empty;
        }
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
