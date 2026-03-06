using MaintenanceSandbox.Models.Base;


namespace MaintenanceSandbox.Models
{
    public class Tenant
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Domain { get; set; }  // e.g. acme.sentinel.cloud
        public string? PlanTier { get; set; } // Standard / Advanced / Premium
        public bool IsActive { get; set; } = true;

        public ICollection<TenantSite> Sites { get; set; } = new List<TenantSite>();
    }
}

