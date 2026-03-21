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

        public TenantProvisioningStatus ProvisioningStatus { get; set; } = TenantProvisioningStatus.Pending;
        public DateTime? ProvisionedAt { get; set; }
        public string? LastProvisioningError { get; set; }

        // Observability — populated by TenantOperationalProvisioner
        public DateTime? ProvisioningStartedAt { get; set; }
        public DateTime? ProvisioningCompletedAt { get; set; }
        public int ProvisioningRetryCount { get; set; } = 0;
        public string? ProvisioningActor { get; set; }

        public ICollection<TenantSite> Sites { get; set; } = new List<TenantSite>();
    }
}

