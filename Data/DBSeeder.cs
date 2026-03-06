using System.Linq;
using MaintenanceSandbox.Models;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceSandbox.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        // Make sure DB and migrations are applied
        db.Database.Migrate();

        // Only seed if there are no users yet
        if (!db.AppUsers.Any())
        {
            db.AppUsers.AddRange(
                new AppUser
                {
                    Name = "Demo Supervisor",
                    Email = "supervisor@sentinel-demo.local",
                    PasswordHash = "demo",   // TODO: replace with real hashing on auth day
                    Role = "Supervisor",
                    TenantId = 1
                },
                new AppUser
                {
                    Name = "Demo Operator",
                    Email = "operator@sentinel-demo.local",
                    PasswordHash = "demo",
                    Role = "Operator",
                    TenantId = 1
                }
            );

            db.SaveChanges();
        }
    }
}

