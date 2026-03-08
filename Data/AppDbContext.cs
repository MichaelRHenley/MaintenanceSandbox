using MaintenanceSandbox.Models;
using MaintenanceSandbox.Models.MasterData;
using MaintenanceSandbox.Models.Production;
using MaintenanceSandbox.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using MaintenanceSandbox.Models.Base;
using MaintenanceSandbox.Models.Onboarding;
using Area = MaintenanceSandbox.Models.MasterData.Area;
using Site = MaintenanceSandbox.Models.MasterData.Site;
using WorkCenter = MaintenanceSandbox.Models.MasterData.WorkCenter;


namespace MaintenanceSandbox.Data;

public sealed class AppDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    // Use TryGetTenantId so migrations / background contexts don’t explode
    private Guid CurrentTenantId => _tenantProvider.TryGetTenantId();




    // ============================
    // CORE BUSINESS DATA
    // ============================

    public DbSet<ProductionCountLog> ProductionCountLogs => Set<ProductionCountLog>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceMessage> MaintenanceMessages => Set<MaintenanceMessage>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<Part> Parts => Set<Part>();
    public DbSet<LocationBin> LocationBins => Set<LocationBin>();
    public DbSet<InventoryLevel> InventoryLevels => Set<InventoryLevel>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<BomItem> BomItems => Set<BomItem>();

    // ============================
    // MASTER DATA (SHARED)
    // ============================

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();
     
    public DbSet<OnboardingSession> OnboardingSessions => Set<OnboardingSession>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EquipmentRequest> EquipmentRequests => Set<EquipmentRequest>();
    public DbSet<Tenant> Tenants => Set<Tenant>();

    // ============================
    // MODEL CONFIGURATION
    // ============================

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();

            b.Property(x => x.Domain).HasMaxLength(200);
            b.Property(x => x.PlanTier).HasMaxLength(50);
        });

        // -------------------------------------------------
        // PART / INVENTORY CONSTRAINTS
        // -------------------------------------------------

        modelBuilder.Entity<Part>()
            .HasIndex(p => new { p.TenantId, p.PartNumber })
            .IsUnique();

        // Decimal precision (prevents silent truncation)
        modelBuilder.Entity<BomItem>()
            .Property(x => x.QuantityPerAsset)
            .HasPrecision(18, 4);

        modelBuilder.Entity<InventoryLevel>()
            .Property(x => x.QuantityOnHand)
            .HasPrecision(18, 4);

        modelBuilder.Entity<InventoryLevel>()
            .Property(x => x.ReorderPoint)
            .HasPrecision(18, 4);

        modelBuilder.Entity<InventoryLevel>()
            .Property(x => x.TargetQuantity)
            .HasPrecision(18, 4);
        modelBuilder.Entity<MaintenanceRequest>(entity =>
        {
            // Required WorkCenter
            entity.HasOne(r => r.WorkCenter)
                  .WithMany()
                  .HasForeignKey(r => r.WorkCenterId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Optional Equipment (but if present, should belong to same WorkCenter)
            entity.HasOne(r => r.Equipment)
                  .WithMany()
                  .HasForeignKey(r => r.EquipmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.TenantId, r.WorkCenterId });
        });

        // -------------------------------------------------
        // MASTER DATA RELATIONSHIPS
        // -------------------------------------------------

        modelBuilder.Entity<Site>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.SiteId, x.Name }).IsUnique();

            entity.HasOne(x => x.Site)
                  .WithMany(s => s.Areas)
                  .HasForeignKey(x => x.SiteId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkCenter>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.AreaId, x.Code }).IsUnique();

            entity.HasOne(x => x.Area)
                  .WithMany(a => a.WorkCenters)
                  .HasForeignKey(x => x.AreaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.WorkCenterId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<EquipmentRequest>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.WorkCenterId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.WorkCenterId, x.RequestedCode, x.Status });
        });

        modelBuilder.Entity<OnboardingSession>(b =>
        {
            b.ToTable("OnboardingSessions");
            b.HasKey(x => x.Id);

            b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            b.Property(x => x.Status).HasMaxLength(30).IsRequired();

            b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        });

 

        // -------------------------------------------------
        // MULTI-TENANT GLOBAL FILTER
        // -------------------------------------------------



        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(CreateTenantFilter(entityType.ClrType));
            }
        }
    }

    // ============================
    // TENANT FILTER BUILDER
    // ============================

    private LambdaExpression CreateTenantFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");

        // e.TenantId
        var tenantProp = Expression.Property(parameter, nameof(TenantEntity.TenantId));

        // this.CurrentTenantId  (EF will parameterize this per DbContext instance)
        var ctx = Expression.Constant(this);
        var currentTenantExpr = Expression.Property(ctx, nameof(CurrentTenantId));

        // CurrentTenantId != Guid.Empty
        var hasTenantExpr = Expression.NotEqual(
            currentTenantExpr,
            Expression.Constant(Guid.Empty)
        );

        // e.TenantId == this.CurrentTenantId
        var tenantMatchExpr = Expression.Equal(tenantProp, currentTenantExpr);

        // (CurrentTenantId != Guid.Empty) && (e.TenantId == CurrentTenantId)
        var body = Expression.AndAlso(hasTenantExpr, tenantMatchExpr);

        return Expression.Lambda(body, parameter);
    }



}
