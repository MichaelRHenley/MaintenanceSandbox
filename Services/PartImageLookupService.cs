namespace MaintenanceSandbox.Services;

/// <summary>
/// Resolves manufacturer part images by walking a registered
/// <see cref="IPartImageProvider"/> pipeline in order and returning the first
/// verified HTTPS URL. Returns null when no provider succeeds — callers must
/// treat null as "no image available" and render a fallback UI.
/// Placeholder or guessed URLs are never returned.
/// </summary>
public class PartImageLookupService : IPartImageLookupService
{
    private readonly IEnumerable<IPartImageProvider> _providers;
    private readonly ILogger<PartImageLookupService> _logger;

    public PartImageLookupService(
        IEnumerable<IPartImageProvider> providers,
        ILogger<PartImageLookupService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<string?> GetImageUrlAsync(string manufacturer, string partNumber)
    {
        // Provider pipeline — executed in registration order; first non-null HTTPS URL wins:
        //   • SkfImageProvider                — SKF MediaHub CDN via catalog search API (see Services/SkfImageProvider.cs)
        //   • GenericDistributorImageProvider — distributor HTML/JSON scraping: Grainger, Zoro, RS Online (see Services/GenericDistributorImageProvider.cs)
        //   • VendorCatalogProvider           — manufacturer API / scrape (Schaeffler, ABB, etc.) [future]
        //   • InternalImageProvider           — internal CDN / blob storage keyed by PartNumber [future]
        //   • BingImageProvider               — Bing Image Search v7 fallback (see Services/BingImageProvider.cs)
        // Each provider must return null rather than a guessed URL when it cannot resolve.

        foreach (var provider in _providers)
        {
            var url = await provider.TryGetImageAsync(manufacturer, partNumber);
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        _logger.LogWarning(
            "No verified image resolved for manufacturer '{Manufacturer}', part '{PartNumber}'.",
            manufacturer, partNumber);

        return null;
    }
}

