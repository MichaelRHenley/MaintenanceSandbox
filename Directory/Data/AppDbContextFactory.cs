using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MaintenanceSandbox.Services;

namespace MaintenanceSandbox.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseSqlServer(
            "YOUR-CONNECTION-STRING-HERE");

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