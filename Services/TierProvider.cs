using Microsoft.Extensions.Configuration;

namespace MaintenanceSandbox.Services;

public class TierProvider : ITierProvider
{
    private ProductTier _currentTier;

    public ProductTier CurrentTier => _currentTier;

    public TierProvider(IConfiguration config)
    {
        var raw = config["ProductTier"] ?? "Standard";

        if (!Enum.TryParse<ProductTier>(raw, ignoreCase: true, out var parsed))
        {
            parsed = ProductTier.Standard;
        }

        _currentTier = parsed;
    }

    public void SetTier(ProductTier tier)
    {
        _currentTier = tier;
    }
}


