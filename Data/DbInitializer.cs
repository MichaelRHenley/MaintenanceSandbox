using Bogus;
using MaintenanceSandbox.Models;
using MaintenanceSandbox.Models.MasterData;
using Microsoft.EntityFrameworkCore;
using Area = MaintenanceSandbox.Models.MasterData.Area;
using Site = MaintenanceSandbox.Models.MasterData.Site;
using WorkCenter = MaintenanceSandbox.Models.MasterData.WorkCenter;

namespace MaintenanceSandbox.Data;

public static class DbInitializer
{
    public static readonly Guid SandboxTenantId = Guid.Parse("5EFA6386-80F0-4565-87D2-5170079B6BE0");

    internal const string SandboxTenantName = "Sandbox Tenant";
    private const string SandboxDomain = "sandbox.sentinel.local";
    private const string SandboxPlanTier = "Standard";

    public static async Task SeedAsync(AppDbContext db)
    {
        // 1) Tenant is the source of truth (Tenant is NOT tenant-filtered)
        var tenant = await GetOrCreateTenantAsync(db, SandboxTenantName, SandboxDomain, SandboxPlanTier);
        var tenantId = tenant.Id;

        // 2) Master data (tenant-scoped; idempotent)
        await EnsureMasterDataAsync(db, tenantId);

        // 3) Reset sandbox operational data only (tenant-scoped)
        await ResetSandboxRequestsAsync(db, tenantId);

        // 4) Tenant-scoped lists for faker
        var workCenters = await db.WorkCenters
            .AsNoTracking()
            .IgnoreQueryFilters() // explicit because seeding often runs without tenant provider being set
            .Where(wc => wc.TenantId == tenantId)
            .Select(wc => new { wc.Id, wc.Code })
            .ToListAsync();

        if (workCenters.Count == 0)
            throw new Exception("No WorkCenters found for sandbox tenant. Ensure master data seeding ran.");

        var equipments = await db.Equipment
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId)
            .Select(e => new { e.Id, e.WorkCenterId, e.Code })
            .ToListAsync();

        if (equipments.Count == 0)
            throw new Exception("No Equipment found for sandbox tenant. Ensure master data seeding ran.");

        // 5) Generate demo requests/messages
        var statuses = new[] { "New", "In Progress", "Waiting on Parts", "Resolved", "Closed" };
        var priorities = new[] { "Low", "Medium", "High" };

        Randomizer.Seed = new Random(12345);
        var rand = new Random(12345);

        var requestFaker = new Faker<MaintenanceRequest>("en")
            .RuleFor(r => r.TenantId, _ => tenantId)
            .RuleFor(r => r.WorkCenterId, f => f.PickRandom(workCenters).Id)
            .RuleFor(r => r.EquipmentId, (f, r) =>
            {
                var eqForWc = equipments.Where(e => e.WorkCenterId == r.WorkCenterId).ToList();
                return eqForWc.Count == 0 ? (int?)null : f.PickRandom(eqForWc).Id;
            })
            .RuleFor(r => r.Status, f => f.PickRandom(statuses))
            .RuleFor(r => r.Priority, f => f.PickRandom(priorities))
            .RuleFor(r => r.RequestedBy, f => f.Name.FullName())
            .RuleFor(r => r.Description, (f, r) =>
                $"Simulated issue on WC#{r.WorkCenterId}: {f.Commerce.ProductAdjective()} {f.Hacker.Noun()}.")
            .RuleFor(r => r.CreatedAt, f => f.Date.Recent(5));

        var messageFaker = new Faker<MaintenanceMessage>("en")
            .RuleFor(m => m.SentAt, f => f.Date.Recent(3))
            .RuleFor(m => m.Sender, f => f.PickRandom("Operator", "Technician", "Supervisor"))
            .RuleFor(m => m.Message, f => $"Simulated comment: {f.Hacker.Phrase()}");

        var requests = requestFaker.Generate(40);

        foreach (var req in requests)
        {
            var count = rand.Next(0, 4); // 0–3 messages
            for (int i = 0; i < count; i++)
            {
                var msg = messageFaker.Generate();

                // If your MaintenanceMessage inherits TenantEntity or has TenantId, set it.
                TrySetTenantId(msg, tenantId);

                req.Messages.Add(msg);
            }
        }

        db.MaintenanceRequests.AddRange(requests);
        await db.SaveChangesAsync();
    }

    // ============================================================
    // TENANT (NOT tenant-filtered)
    // ============================================================
    private static async Task<Tenant> GetOrCreateTenantAsync(
        AppDbContext db,
        string name,
        string? domain,
        string? planTier)
    {
        // Local first
        var local = db.Tenants.Local.FirstOrDefault(t => t.Name == name);
        if (local != null) return local;

        // DB
        var existing = await db.Tenants
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Name == name);

        if (existing != null)
        {
            var changed = false;

            if (!string.Equals(existing.Domain, domain, StringComparison.Ordinal))
            {
                existing.Domain = domain;
                changed = true;
            }
            if (!string.Equals(existing.PlanTier, planTier, StringComparison.Ordinal))
            {
                existing.PlanTier = planTier;
                changed = true;
            }
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                changed = true;
            }

            if (changed)
                await db.SaveChangesAsync();

            return existing;
        }

        var created = new Tenant
        {
            Id = name == SandboxTenantName ? SandboxTenantId : Guid.NewGuid(),
            Name = name,
            Domain = domain,
            PlanTier = planTier,
            IsActive = true
        };

        db.Tenants.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    // ============================================================
    // MASTER DATA (tenant-scoped, idempotent)
    // ============================================================
    private static async Task EnsureMasterDataAsync(AppDbContext db, Guid tenantId)
    {
        // IMPORTANT: seeding should not depend on ITenantProvider being set.
        // So we always IgnoreQueryFilters() and explicitly filter by tenantId.

        // Sites
        var siteAlpha = await GetOrCreateSiteAsync(db, tenantId, "Site Alpha");
        var siteBeta = await GetOrCreateSiteAsync(db, tenantId, "Site Beta");
        await db.SaveChangesAsync();

        // Areas under Site Alpha
        var area1 = await GetOrCreateAreaAsync(db, tenantId, siteAlpha.Id, "Area 1");
        var area2 = await GetOrCreateAreaAsync(db, tenantId, siteAlpha.Id, "Area 2");
        await db.SaveChangesAsync();

        // WorkCenters under Area 1
        var wc1 = await GetOrCreateWorkCenterAsync(db, tenantId, area1.Id, code: "WC-001", displayName: "WorkCenter 001");
        var wc2 = await GetOrCreateWorkCenterAsync(db, tenantId, area1.Id, code: "WC-002", displayName: "WorkCenter 002");
        await db.SaveChangesAsync();

        // Equipment under each WC
        await GetOrCreateEquipmentAsync(db, tenantId, wc1.Id, code: "EQ-001-01", displayName: "Equipment 01");
        await GetOrCreateEquipmentAsync(db, tenantId, wc1.Id, code: "EQ-001-02", displayName: "Equipment 02");
        await GetOrCreateEquipmentAsync(db, tenantId, wc2.Id, code: "EQ-002-01", displayName: "Equipment 01");
        await GetOrCreateEquipmentAsync(db, tenantId, wc2.Id, code: "EQ-002-02", displayName: "Equipment 02");
        await db.SaveChangesAsync();
    }

    // ============================================================
    // RESET sandbox operational data (tenant-scoped)
    // ============================================================
    private static async Task ResetSandboxRequestsAsync(AppDbContext db, Guid tenantId)
    {
        var reqIds = await db.MaintenanceRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync();

        if (reqIds.Count == 0)
            return;

        // Delete children first
        var msgs = db.MaintenanceMessages
            .IgnoreQueryFilters()
            .Where(m => reqIds.Contains(m.MaintenanceRequestId));
        db.MaintenanceMessages.RemoveRange(msgs);

        var reqs = db.MaintenanceRequests
            .IgnoreQueryFilters()
            .Where(r => reqIds.Contains(r.Id));
        db.MaintenanceRequests.RemoveRange(reqs);

        await db.SaveChangesAsync();
    }

    // ============================================================
    // GET-OR-CREATE HELPERS (tenant-scoped)
    // ============================================================

    private static async Task<Site> GetOrCreateSiteAsync(AppDbContext db, Guid tenantId, string name)
    {
        var local = db.Sites.Local.FirstOrDefault(s => s.TenantId == tenantId && s.Name == name);
        if (local != null) return local;

        var existing = await db.Sites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Name == name);

        if (existing != null)
        {
            if (!db.ChangeTracker.Entries<Site>().Any(e => e.Entity.Id == existing.Id))
                db.Sites.Attach(existing);

            return existing;
        }

        var created = new Site { TenantId = tenantId, Name = name };
        db.Sites.Add(created);
        return created;
    }

    private static async Task<Area> GetOrCreateAreaAsync(AppDbContext db, Guid tenantId, int siteId, string name)
    {
        var local = db.Areas.Local.FirstOrDefault(a => a.TenantId == tenantId && a.SiteId == siteId && a.Name == name);
        if (local != null) return local;

        var existing = await db.Areas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.SiteId == siteId && a.Name == name);

        if (existing != null)
        {
            if (!db.ChangeTracker.Entries<Area>().Any(e => e.Entity.Id == existing.Id))
                db.Areas.Attach(existing);

            return existing;
        }

        var created = new Area { TenantId = tenantId, SiteId = siteId, Name = name };
        db.Areas.Add(created);
        return created;
    }

    private static async Task<WorkCenter> GetOrCreateWorkCenterAsync(
        AppDbContext db,
        Guid tenantId,
        int areaId,
        string code,
        string displayName)
    {
        var local = db.WorkCenters.Local.FirstOrDefault(w => w.TenantId == tenantId && w.AreaId == areaId && w.Code == code);
        if (local != null) return local;

        var existing = await db.WorkCenters
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.AreaId == areaId && w.Code == code);

        if (existing != null)
        {
            if (!string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
                existing.DisplayName = displayName;

            if (!db.ChangeTracker.Entries<WorkCenter>().Any(e => e.Entity.Id == existing.Id))
                db.WorkCenters.Attach(existing);

            return existing;
        }

        var created = new WorkCenter
        {
            TenantId = tenantId,
            AreaId = areaId,
            Code = code,
            DisplayName = displayName
        };

        db.WorkCenters.Add(created);
        return created;
    }

    private static async Task<Equipment> GetOrCreateEquipmentAsync(
        AppDbContext db,
        Guid tenantId,
        int workCenterId,
        string code,
        string displayName)
    {
        var local = db.Equipment.Local.FirstOrDefault(e => e.TenantId == tenantId && e.WorkCenterId == workCenterId && e.Code == code);
        if (local != null) return local;

        var existing = await db.Equipment
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.WorkCenterId == workCenterId && e.Code == code);

        if (existing != null)
        {
            if (!string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
                existing.DisplayName = displayName;

            if (!db.ChangeTracker.Entries<Equipment>().Any(en => en.Entity.Id == existing.Id))
                db.Equipment.Attach(existing);

            return existing;
        }

        var created = new Equipment
        {
            TenantId = tenantId,
            WorkCenterId = workCenterId,
            Code = code,
            DisplayName = displayName
        };

        db.Equipment.Add(created);
        return created;
    }

    // ============================================================
    // OPTIONAL: set TenantId on message if the model has it
    // ============================================================
    private static void TrySetTenantId(object entity, Guid tenantId)
    {
        var prop = entity.GetType().GetProperty("TenantId");
        if (prop?.PropertyType == typeof(Guid) && prop.CanWrite)
            prop.SetValue(entity, tenantId);
    }
}