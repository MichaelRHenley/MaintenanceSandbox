namespace MaintenanceSandbox.Services;

public interface IPartImageLookupService
{
    /// <summary>
    /// Returns a URL for a manufacturer part image, or null if none is available.
    /// </summary>
    Task<string?> GetImageUrlAsync(string manufacturer, string partNumber);
}
