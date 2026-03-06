namespace MaintenanceSandbox.Models
{

    public sealed class SubscribeStartVm
    {
        public string CompanyName { get; set; } = "";
        public string Tier { get; set; } = "Tier1";
        public string BillingCadence { get; set; } = "Monthly";
    }

}