using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Resolves part images using the Bing Image Search API v7.
/// Requires <c>Bing:ApiKey</c> in configuration (set via environment variable
/// <c>Bing__ApiKey</c> in production — never commit the key to source control).
/// Returns null immediately when the key is absent so the pipeline
/// degrades gracefully without throwing.
/// </summary>
public class BingImageProvider : IPartImageProvider
{
    private const string SearchEndpoint =
        "https://api.bing.microsoft.com/v7.0/images/search";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BingImageProvider> _logger;

    public BingImageProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<BingImageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> TryGetImageAsync(string manufacturer, string partNumber)
    {
        var apiKey = _configuration["Bing:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogDebug(
                "Bing:ApiKey is not configured — BingImageProvider skipped for '{Manufacturer}' '{PartNumber}'.",
                manufacturer, partNumber);
            return null;
        }

        var query = Uri.EscapeDataString($"{manufacturer} {partNumber}");
        var requestUrl = $"{SearchEndpoint}?q={query}&count=5&safeSearch=Strict&imageType=Photo";

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Bing Image Search returned {StatusCode} for '{Manufacturer}' '{PartNumber}'.",
                    response.StatusCode, manufacturer, partNumber);
                return null;
            }

            var result = await response.Content
                .ReadFromJsonAsync<BingSearchResponse>();

            var imageUrl = result?.Value?
                .Select(v => v.ContentUrl)
                .FirstOrDefault(url =>
                    !string.IsNullOrWhiteSpace(url) &&
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            if (imageUrl is null)
                _logger.LogDebug(
                    "Bing Image Search returned no HTTPS image for '{Manufacturer}' '{PartNumber}'.",
                    manufacturer, partNumber);

            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "BingImageProvider failed for '{Manufacturer}' '{PartNumber}'.",
                manufacturer, partNumber);
            return null;
        }
    }

    // ── Bing API response shape ──────────────────────────────────────────────

    private sealed class BingSearchResponse
    {
        [JsonPropertyName("value")]
        public BingImageResult[]? Value { get; init; }
    }

    private sealed class BingImageResult
    {
        [JsonPropertyName("contentUrl")]
        public string? ContentUrl { get; init; }
    }
}
