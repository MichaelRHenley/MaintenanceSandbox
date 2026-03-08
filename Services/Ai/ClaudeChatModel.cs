using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MaintenanceSandbox.Services.Ai;

public class ClaudeChatModel : IChatModel
{
    private readonly HttpClient _http;
    private readonly ILogger<ClaudeChatModel> _logger;

    public ClaudeChatModel(HttpClient http, ILogger<ClaudeChatModel> logger)
    {
        _http = http;
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
                model = "claude-sonnet-4-20250514",
                max_tokens = 1000,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                }
            };

            var response = await _http.PostAsJsonAsync(
                "https://api.anthropic.com/v1/messages",
                payload,
                ct
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Anthropic API error: {StatusCode} - {Error}",
                    response.StatusCode, error);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(
                cancellationToken: ct
            );

            return result?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude API call failed — AI suggestions unavailable");
            return string.Empty;
        }
    }

    // DTOs for Anthropic API response
    private class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<ContentBlock>? Content { get; set; }

        [JsonPropertyName("usage")]
        public UsageInfo? Usage { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class UsageInfo
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}