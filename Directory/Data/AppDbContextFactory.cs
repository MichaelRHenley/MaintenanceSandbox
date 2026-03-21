using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MaintenanceSandbox.Services;

namespace MaintenanceSandbox.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Design-time only — used by `dotnet ef migrations add`.
        // Matches appsettings.json DefaultConnection for local development.
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=MaintenanceSandbox;Trusted_Connection=True;MultipleActiveResultSets=true");

        return new AppDbContext(
            optionsBuilder.Options,
            new DesignTimeTenantProvider());
    }

    private class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid GetTenantId() => Guid.Empty;
        public Guid TryGetTenantId() => Guid.Empty;
    }
}