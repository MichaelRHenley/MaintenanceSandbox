using Microsoft.Extensions.Caching.Memory;

namespace MaintenanceSandbox.Demo;

public interface IDemoAiRateLimiter
{
    /// <summary>Returns true if the call is allowed and consumes one slot.</summary>
    bool TryConsume(string tenantId);

    int Remaining(string tenantId);
}

public sealed class DemoAiRateLimiter : IDemoAiRateLimiter
{
    public const int MaxCallsPerHour = 20;

    private readonly IMemoryCache _cache;

    // Store count + the fixed window expiry together so re-setting preserves the window.
    private record Window(int Count, DateTimeOffset Expiry);

    public DemoAiRateLimiter(IMemoryCache cache) => _cache = cache;

    public bool TryConsume(string tenantId)
    {
        var key = CacheKey(tenantId);

        if (_cache.TryGetValue(key, out Window? w) && w is not null)
        {
            if (w.Count >= MaxCallsPerHour) return false;
            _cache.Set(key, w with { Count = w.Count + 1 }, w.Expiry);
            return true;
        }

        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        _cache.Set(key, new Window(1, expiry), expiry);
        return true;
    }

    public int Remaining(string tenantId)
    {
        if (_cache.TryGetValue(CacheKey(tenantId), out Window? w) && w is not null)
            return Math.Max(0, MaxCallsPerHour - w.Count);
        return MaxCallsPerHour;
    }

    private static string CacheKey(string tenantId) => $"demo:ai:{tenantId}";
}
