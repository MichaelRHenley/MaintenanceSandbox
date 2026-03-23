using MaintenanceSandbox.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaintenanceSandbox.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> and
/// <c>dotnet ef database update</c>.  Reads the connection string from
/// appsettings.json and then overlays the environment-specific file
/// (appsettings.Development.json / appsettings.Production.json) so that
/// EF Core targets the same database as the running application.
///
/// Production connection strings are supplied via environment variables
/// (e.g. ConnectionStrings__DefaultConnection) and are picked up by
/// <see cref="Microsoft.Extensions.Configuration.EnvironmentVariablesExtensions.AddEnvironmentVariables"/>.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found in appsettings.json " +
                $"or appsettings.{environment}.json.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options, new DesignTimeTenantProvider());
    }

    /// <summary>
    /// Stub tenant provider used only during design-time tooling.
    /// Returns <see cref="Guid.Empty"/> so the global query filter is
    /// a no-op and EF Core can introspect the full schema.
    /// </summary>
    private sealed class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid GetTenantId() => Guid.Empty;
        public Guid TryGetTenantId() => Guid.Empty;
    }
}
