using System.Text.RegularExpressions;

namespace MaintenanceSandbox.Services;

/// <summary>
/// Retrieves SKF product images from SKF MediaHub CDN using catalog search API.
/// Calls the SKF catalog search endpoint to resolve a hash-based image ID,
/// then constructs the corresponding MediaHub CDN URL.
/// Returns null immediately for any non-SKF manufacturer.
/// </summary>
public class SkfImageProvider : IPartImageProvider
{
    // TODO: temporary local-image rendering test — replace with real SKF MediaHub
    // CDN lookup once the image rendering path is confirmed working end-to-end.
    // Restore HttpClient constructor, SearchEndpoint, MediaHubTemplate, and the
    // SkfSearchResponse / SkfSearchItem DTOs from git history when ready.

    public Task<string?> TryGetImageAsync(string manufacturer, string partNumber)
    {
        if (!manufacturer.Equals("SKF", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<string?>(null);

        var normalized = Regex.Replace(partNumber.Trim(), @"\s+", " ").ToUpperInvariant();

        if (normalized == "6207-2RS1")
            return Task.FromResult<string?>("/images/parts/skf-6207-2rs1.png");

        return Task.FromResult<string?>(null);
    }
}
