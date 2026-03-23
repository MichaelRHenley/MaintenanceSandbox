using System.Text.RegularExpressions;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Attempts to resolve industrial part images from common distributor/catalog
/// search pages without requiring manufacturer-specific providers or API keys.
/// Uses Open Graph image meta-tag extraction and embedded JSON URL scanning
/// (including Next.js <c>__NEXT_DATA__</c> blobs) to find valid product image URLs.
/// Returns the first valid HTTPS image URL found across the attempted sources,
/// or null when all sources fail or yield no trustworthy image.
/// </summary>
public class GenericDistributorImageProvider : IPartImageProvider
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly string[] _imageExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    // Captures og:image content regardless of attribute order in the <meta> tag
    private static readonly Regex _ogImageRegex = new(
        @"<meta\b[^>]*\bproperty=""og:image""[^>]*\bcontent=""([^""]+)""[^>]*>" +
        @"|<meta\b[^>]*\bcontent=""([^""]+)""[^>]*\bproperty=""og:image""[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    // Finds the first quoted HTTPS image URL inside any embedded JSON blob
    // (__NEXT_DATA__, application/ld+json, inline script JSON, etc.)
    private static readonly Regex _jsonImageUrlRegex = new(
        @"""(https://[^""\\]{10,500}\.(?:jpg|jpeg|png|webp)(?:\?[^""\\]*)?)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;

    public GenericDistributorImageProvider(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> TryGetImageAsync(string manufacturer, string manufacturerPartNumber)
    {
        var mfr  = Regex.Replace(manufacturer.Trim(), @"\s+", " ");
        var part = Regex.Replace(manufacturerPartNumber.Trim(), @"\s+", " ").ToUpperInvariant();

        if (string.IsNullOrEmpty(part)) return null;

        var query = Uri.EscapeDataString($"{mfr} {part}");

        // ── Distributor sources — tried in order ─────────────────────────────
        // Each source is fetched with a browser-like UA; the first 256 KB of HTML
        // is scanned for embedded JSON image URLs (Next.js sites) or og:image tags.
        //
        // Sources NOT attempted and why:
        //   McMaster-Carr    — single-page app, anti-scraping; no server-rendered images
        //   DigiKey          — Angular SPA; product images require OAuth API
        //   Mouser           — Angular SPA; initial HTML contains no product images
        //   AutomationDirect — product images only in JS-rendered DOM
        // ─────────────────────────────────────────────────────────────────────

        // 1. Grainger — largest US industrial distributor; uses Next.js which embeds
        //    product data (including image URLs) in __NEXT_DATA__ JSON in the HTML.
        var grainger = await TryExtractFromPageAsync(
            $"https://www.grainger.com/search?searchQuery={query}", preferJsonScan: true);
        if (grainger is not null) return grainger;

        // 2. Zoro (Grainger subsidiary) — same technology stack, complementary catalog.
        var zoro = await TryExtractFromPageAsync(
            $"https://www.zoro.com/search?q={query}", preferJsonScan: true);
        if (zoro is not null) return zoro;

        // 3. RS Online — server-rendered pages for industrial/electronic components;
        //    og:image may be a real product image when a query resolves to a single result.
        var rs = await TryExtractFromPageAsync(
            $"https://www.rs-online.com/web/c/?searchTerm={query}", preferJsonScan: false);
        if (rs is not null) return rs;

        return null;
    }

    // ── Page fetch + image extraction ────────────────────────────────────────

    private async Task<string?> TryExtractFromPageAsync(string pageUrl, bool preferJsonScan)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode) return null;

            // Read at most 256 KB — enough to cover the <head> and embedded JSON blobs
            // while avoiding downloading full multi-MB page bodies.
            var html = await ReadBoundedAsync(response.Content, maxBytes: 256 * 1024);

            return preferJsonScan
                ? ExtractFirstJsonImageUrl(html) ?? ExtractOgImage(html)
                : ExtractOgImage(html) ?? ExtractFirstJsonImageUrl(html);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ReadBoundedAsync(HttpContent content, int maxBytes)
    {
        using var stream = await content.ReadAsStreamAsync();
        var buffer = new byte[maxBytes];
        var total  = 0;
        int read;
        while (total < maxBytes
               && (read = await stream.ReadAsync(buffer.AsMemory(total, maxBytes - total))) > 0)
        {
            total += read;
        }
        return System.Text.Encoding.UTF8.GetString(buffer, 0, total);
    }

    // ── Extraction helpers ────────────────────────────────────────────────────

    private string? ExtractOgImage(string html)
    {
        var match = _ogImageRegex.Match(html);
        if (!match.Success) return null;

        var url = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return IsValidImageUrl(url) ? url : null;
    }

    private string? ExtractFirstJsonImageUrl(string html)
    {
        foreach (Match match in _jsonImageUrlRegex.Matches(html))
        {
            var url = match.Groups[1].Value;
            if (IsValidImageUrl(url)) return url;
        }
        return null;
    }

    private static bool IsValidImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;

        var lower = url.ToLowerInvariant();
        var path  = lower.Contains('?') ? lower[..lower.IndexOf('?')] : lower;

        // Must end with a recognised image extension
        if (!_imageExtensions.Any(ext => path.EndsWith(ext))) return false;

        // Exclude common site chrome — logos and icons appear on all pages regardless
        // of the search query and are not product images
        if (path.Contains("/logo") || path.Contains("/icon") || path.Contains("/favicon"))
            return false;

        return true;
    }
}
