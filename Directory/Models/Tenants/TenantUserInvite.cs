namespace MaintenanceSandbox.Directory.Models.Tenants
{
    public sealed class TenantUserInvite
    {
        public int Id { get; set; }
        public Guid TenantId { get; set; }

        public string Email { get; set; } = "";
        public TenantRole Role { get; set; }   // NOT string


        public string TokenHash { get; set; } = ""; // store hash, not raw token
        public DateTimeOffset ExpiresUtc { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Accepted, Revoked, Expired
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public string CreatedByUserId { get; set; } = "";

        public DateTimeOffset? AcceptedUtc { get; set; }
        public string? AcceptedByUserId { get; set; }
    }

}
