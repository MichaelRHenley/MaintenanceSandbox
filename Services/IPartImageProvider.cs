namespace MaintenanceSandbox.Services;

/// <summary>
/// Pluggable image source for manufacturer part images.
/// Implementations are iterated in registration order by
/// <see cref="PartImageLookupService"/>; the first non-null
/// HTTPS URL wins.
/// </summary>
public interface IPartImageProvider
{
    /// <summary>
    /// Attempts to resolve an image URL for the given manufacturer and part number.
    /// Returns null if this provider cannot supply a verified HTTPS URL.
    /// Must never return placeholder or guessed URLs.
    /// </summary>
    Task<string?> TryGetImageAsync(string manufacturer, string partNumber);
}
