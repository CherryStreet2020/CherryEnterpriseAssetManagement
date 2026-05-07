using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for the 2026-05-07 PR-3 hardening of MaintenanceService
/// and the WorkOrderParts query in <c>Pages/Maintenance/Details.cshtml.cs</c>.
///
/// PR-3 closed two issues found during the end-of-day code review:
/// 1. MaintenanceService had a no-arg ctor that left ITenantContext null —
///    every scoped query in the service crashed with NRE if it was used.
/// 2. CreateEventAsync / UpdateEventAsync / CompleteEventAsync skipped tenant
///    scoping when CompanyId was null on the tenant context (mirroring the
///    AccountsPayable leak fixed in PR #22). Now scoping is mandatory.
/// 3. The WorkOrderParts query in Details.cshtml.cs trusted the parent
///    event's scope-verification — a refactor could have opened a parts leak.
///    Now re-asserts tenant scope on its own.
/// </summary>
public class MaintenanceTenantScopingTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"maint-scope-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public int? TenantId { get; init; } = 1;
        public int? CompanyId { get; init; }
        public int? SiteId { get; init; }
        public int? AssignedCompanyId { get; init; }
        public int? AssignedSiteId { get; init; }
        public List<int> VisibleCompanyIds { get; init; } = new();
        public List<int> VisibleSiteIds { get; init; } = new();
        public bool IsResolved => true;
        public string? ResolutionError => null;
        public void SetContext(int? tenantId, int? companyId, int? siteId) { }
        public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
        public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
        public void SetError(string error) { }
    }

    /// <summary>
    /// PR-3 contract: constructing without an ITenantContext is a compile error.
    /// This test proves the runtime check that backs the contract — passing a
    /// null tenant explicitly throws ArgumentNullException, surfacing the
    /// dependency violation immediately rather than later in a query NRE.
    /// </summary>
    [Fact]
    public void Constructor_NullTenantContext_ThrowsArgumentNull()
    {
        using var db = NewDb();
        Assert.Throws<ArgumentNullException>(() => new MaintenanceService(db, null!));
    }

    /// <summary>
    /// CreateEventAsync used to skip tenant scoping when CompanyId was null
    /// (the AP-leak shape). Now scoping is mandatory: an asset that isn't in
    /// the tenant's visible set must produce a null result.
    /// </summary>
    [Fact]
    public async Task CreateEventAsync_AssetOutsideVisibleScope_ReturnsNull()
    {
        await using var db = NewDb();

        // Asset belongs to company 200; tenant only sees company 100.
        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "A", IsActive = true });
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        var foreignAsset = new Asset
        {
            AssetNumber = "A-200",
            Description = "Foreign asset",
            CompanyId = 200,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(foreignAsset);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext
        {
            CompanyId = 100,
            VisibleCompanyIds = new() { 100 } // 200 NOT visible
        };
        var svc = new MaintenanceService(db, tenant);

        var result = await svc.CreateEventAsync(new MaintenanceEvent
        {
            AssetId = foreignAsset.Id,
            Type = MaintenanceType.Corrective,
            Description = "should be rejected",
            ScheduledDate = DateTime.UtcNow,
            Status = MaintenanceStatus.Scheduled
        });

        Assert.Null(result); // CRITICAL: cross-tenant Create silently returns null
        Assert.Equal(0, await db.MaintenanceEvents.CountAsync()); // nothing persisted
    }

    /// <summary>
    /// CreateEventAsync happy path — when the asset IS visible to the tenant,
    /// the event persists. Pairs with the rejection test to prove the scope
    /// check is neither over- nor under-restrictive.
    /// </summary>
    [Fact]
    public async Task CreateEventAsync_AssetInVisibleScope_PersistsEvent()
    {
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "A", IsActive = true });
        var asset = new Asset
        {
            AssetNumber = "A-100",
            Description = "In-scope asset",
            CompanyId = 100,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext
        {
            CompanyId = 100,
            VisibleCompanyIds = new() { 100 }
        };
        var svc = new MaintenanceService(db, tenant);

        var result = await svc.CreateEventAsync(new MaintenanceEvent
        {
            AssetId = asset.Id,
            Type = MaintenanceType.Corrective,
            Description = "in-scope",
            ScheduledDate = DateTime.UtcNow,
            Status = MaintenanceStatus.Scheduled
        });

        Assert.NotNull(result);
        Assert.Equal(1, await db.MaintenanceEvents.CountAsync());
    }

    /// <summary>
    /// The defensive WorkOrderParts query in Pages/Maintenance/Details.cshtml.cs
    /// must filter by the tenant's VisibleCompanyIds, not just the parent
    /// event's MaintenanceEventId. This test mirrors the LINQ predicate
    /// directly and proves cross-tenant parts cannot leak even if they share
    /// a part-level relationship through an attacker-supplied event id.
    /// </summary>
    [Fact]
    public async Task WorkOrderPartsQuery_TenantScopedPredicate_ExcludesForeignTenantParts()
    {
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "A", IsActive = true });
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });

        var assetA = new Asset
        {
            AssetNumber = "A-100",
            Description = "Tenant A asset",
            CompanyId = 100,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        var assetB = new Asset
        {
            AssetNumber = "A-200",
            Description = "Tenant B asset",
            CompanyId = 200,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.AddRange(assetA, assetB);
        await db.SaveChangesAsync();

        var evtA = new MaintenanceEvent
        {
            WorkOrderNumber = "WO-A",
            AssetId = assetA.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        var evtB = new MaintenanceEvent
        {
            WorkOrderNumber = "WO-B",
            AssetId = assetB.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.MaintenanceEvents.AddRange(evtA, evtB);
        await db.SaveChangesAsync();

        var item = new Item { PartNumber = "ITM-1", Description = "Widget" };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.WorkOrderParts.Add(new WorkOrderPart { MaintenanceEventId = evtA.Id, ItemId = item.Id, QuantityPlanned = 1 });
        db.WorkOrderParts.Add(new WorkOrderPart { MaintenanceEventId = evtB.Id, ItemId = item.Id, QuantityPlanned = 1 });
        await db.SaveChangesAsync();

        // Tenant only sees company 100.
        var tenant = new StubTenantContext
        {
            CompanyId = 100,
            VisibleCompanyIds = new() { 100 }
        };

        // Replicates the predicate from Pages/Maintenance/Details.cshtml.cs::OnGetAsync.
        // If a refactor changes the page query, update this test in lockstep —
        // its purpose is to lock down the WHERE-clause shape.
        async Task<List<WorkOrderPart>> ScopedQuery(int eventId) => await db.WorkOrderParts
            .Where(p => p.MaintenanceEventId == eventId
                && p.MaintenanceEvent != null
                && p.MaintenanceEvent.Asset != null
                && tenant.VisibleCompanyIds.Contains(p.MaintenanceEvent.Asset.CompanyId ?? 0)
                && (!tenant.SiteId.HasValue || p.MaintenanceEvent.Asset.SiteId == tenant.SiteId.Value))
            .ToListAsync();

        // Querying tenant A's event returns the one A-scoped part.
        var partsForA = await ScopedQuery(evtA.Id);
        Assert.Single(partsForA);

        // CRITICAL: querying tenant B's event ID with tenant A's scope yields ZERO parts —
        // even though the part exists in the DB and matches MaintenanceEventId, the
        // tenant predicate filters it out. This is the leak vector PR-3 closes.
        var partsForBFromTenantA = await ScopedQuery(evtB.Id);
        Assert.Empty(partsForBFromTenantA);
    }
}
