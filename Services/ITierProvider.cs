namespace MaintenanceSandbox.Services;

public enum ProductTier
{
    Standard,
    Enhanced,
    Premium
}

public interface ITierProvider
{
    ProductTier CurrentTier { get; }
    void SetTier(ProductTier tier);
}


