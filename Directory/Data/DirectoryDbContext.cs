using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Directory.Models.Tenants;

using DirectoryTenant = MaintenanceSandbox.Directory.Models.Tenants.Tenant;
using DirectoryTenantSubscription = MaintenanceSandbox.Directory.Models.Tenants.TenantSubscription;
using DirectoryTenantUserRole = MaintenanceSandbox.Directory.Models.Tenants.TenantUserRole;
using DirectoryTenantUserInvite = MaintenanceSandbox.Directory.Models.Tenants.TenantUserInvite;


namespace MaintenanceSandbox.Directory.Data;

public class DirectoryDbContext : IdentityDbContext<ApplicationUser>
{
    public DirectoryDbContext(DbContextOptions<DirectoryDbContext> options)
        : base(options) { }

    public DbSet<DirectoryTenant> Tenants => Set<DirectoryTenant>();
    public DbSet<DirectoryTenantSubscription> TenantSubscriptions => Set<DirectoryTenantSubscription>();
    public DbSet<DirectoryTenantUserRole> TenantUserRoles => Set<DirectoryTenantUserRole>();
    public DbSet<DirectoryTenantUserInvite> TenantUserInvites => Set<DirectoryTenantUserInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DirectoryTenant>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<DirectoryTenantSubscription>(b =>
        {
            b.Property(x => x.Tier).IsRequired().HasMaxLength(50);
            b.Property(x => x.BillingCadence).IsRequired().HasMaxLength(50);
            b.Property(x => x.StripeCustomerId).HasMaxLength(200);
            b.Property(x => x.StripeSubscriptionId).HasMaxLength(200);

            b.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.TenantId).IsUnique();
        });

        modelBuilder.Entity<DirectoryTenantUserRole>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.UserId, x.Role });
        });

        // If your invite model needs constraints, add them here too
        modelBuilder.Entity<DirectoryTenantUserInvite>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Email, x.Status });
            b.Property(x => x.Email).IsRequired().HasMaxLength(256);
            b.Property(x => x.Role).IsRequired().HasMaxLength(50);
            b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
        });
    }
}


