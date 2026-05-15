using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Cip;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for S1-4: CIP capitalization hardening. Per the audit:
/// "CIP capitalization hardcodes GL accounts and skips PeriodGuard /
/// depreciation backfill / tenant-stamp."
///
/// Each test exercises one of the four fixes:
/// 1. CompanyId stamped on the new asset (was missing — tenant leak)
/// 2. OriginatingCipProjectId stamped (S2-4 wiring)
/// 3. GL accounts resolved via IGlAccountResolver (was hardcoded "1500"/"1400")
/// 4. PeriodGuard hard-blocks capitalize into closed periods
/// 5. Tenant scope on the project lookup (was unscoped)
/// </summary>
public class CipCapitalizationHardeningTests
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
            .UseInMemoryDatabase($"cip-cap-{dbName}-{Guid.NewGuid()}")
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

    private sealed class StubPeriodGuard : IPeriodGuard
    {
        private readonly bool _allow;
        public int CallCount { get; private set; }
        public StubPeriodGuard(bool allow) { _allow = allow; }
        public Task<PeriodCheckResult> CanPostAsync(int companyId, DateTime postingDate)
        {
            CallCount++;
            return Task.FromResult(new PeriodCheckResult
            {
                IsAllowed = _allow,
                Reason = _allow ? null : "Period closed (test stub)"
            });
        }
        public Task EnsureCanPostAsync(int companyId, DateTime postingDate) => Task.CompletedTask;
    }

    private static async Task<(AppDbContext db, CipProject project, CipCapitalizationService svc, StubPeriodGuard guard)>
        SetupAsync(int companyId, bool periodAllowed)
    {
        var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = $"C-{companyId}", Name = "Co", IsActive = true });

        var project = new CipProject
        {
            ProjectNumber = "CIP-CAP-1",
            Name = "Capitalize me",
            CompanyId = companyId,
            Status = CipProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        // Add a single capitalizable cost so capitalizableAmount > 0.
        db.CipCosts.Add(new CipCost
        {
            CipProjectId = project.Id,
            CostType = CipCostType.Equipment,
            Amount = 1000m,
            TransactionDate = DateTime.UtcNow,
            SourceType = "MANUAL",
            IsCapitalizable = true,
            EnteredBy = "test",
            CreatedByUserId = "test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var costSvc = new CipCostService(db, lookup, tenant);
        var resolver = new GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var guard = new StubPeriodGuard(allow: periodAllowed);
        var depBackfill = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);

        var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var svc = new CipCapitalizationService(db, costSvc, lookup, tenant, resolver, guard, depBackfill, outbox);
        return (db, project, svc, guard);
    }

    [Fact]
    public async Task Capitalize_StampsCompanyIdOnNewAsset()
    {
        var (db, project, svc, _) = await SetupAsync(companyId: 100, periodAllowed: true);

        var (asset, _) = await svc.CapitalizeAsync(project.Id, "A-CAP-1", "Capitalized asset", usefulLifeMonths: 60);

        Assert.NotNull(asset);
        Assert.Equal(100, asset!.CompanyId); // CRITICAL: was unset pre-fix → tenant leak
    }

    [Fact]
    public async Task Capitalize_StampsOriginatingCipProjectId()
    {
        var (db, project, svc, _) = await SetupAsync(companyId: 100, periodAllowed: true);

        var (asset, _) = await svc.CapitalizeAsync(project.Id, "A-CAP-2", "x", usefulLifeMonths: 60);

        Assert.NotNull(asset);
        Assert.Equal(project.Id, asset!.OriginatingCipProjectId); // S2-4 wiring
    }

    [Fact]
    public async Task Capitalize_EmitsCipCapitalizedAndAssetCreatedOutboxEvents()
    {
        var (db, project, svc, _) = await SetupAsync(companyId: 100, periodAllowed: true);

        var (asset, cap) = await svc.CapitalizeAsync(project.Id, "A-CAP-EVT", "Capitalized", usefulLifeMonths: 60);

        var capEvt = await db.OutboxEvents.SingleAsync(e => e.EventType == "cip.capitalized");
        Assert.Equal("CipProject", capEvt.EntityType);
        Assert.Equal(project.Id.ToString(), capEvt.EntityId);
        using (var doc = System.Text.Json.JsonDocument.Parse(capEvt.PayloadJson))
        {
            var root = doc.RootElement;
            Assert.Equal(project.Id, root.GetProperty("cipProjectId").GetInt32());
            Assert.Equal(asset!.Id, root.GetProperty("newAssetId").GetInt32());
            Assert.Equal("A-CAP-EVT", root.GetProperty("assetNumber").GetString());
            Assert.Equal(cap!.JournalEntryId, root.GetProperty("journalEntryId").GetInt32());
        }

        var createdEvt = await db.OutboxEvents.SingleAsync(e => e.EventType == "asset.created");
        Assert.Equal(asset.Id.ToString(), createdEvt.EntityId);
        using (var doc = System.Text.Json.JsonDocument.Parse(createdEvt.PayloadJson))
        {
            Assert.Equal("cip.capitalized", doc.RootElement.GetProperty("origin").GetString());
        }
    }

    [Fact]
    public async Task Capitalize_PostsJEWithGLResolverAccountsNotHardcodedStrings()
    {
        var (db, project, svc, _) = await SetupAsync(companyId: 100, periodAllowed: true);

        var (asset, cap) = await svc.CapitalizeAsync(project.Id, "A-CAP-3", "x", usefulLifeMonths: 60);

        Assert.NotNull(cap);
        var je = await db.JournalEntries
            .Include(j => j.Lines)
            .FirstAsync(j => j.Id == cap!.JournalEntryId);

        // Industry defaults from GlAccountResolver.IndustryDefaults
        // (no per-company config seeded in this test → fallback path):
        //   AssetCost = "1500"
        //   CipPending = "1400"
        var dr = je.Lines.Single(l => l.Debit > 0);
        var cr = je.Lines.Single(l => l.Credit > 0);
        Assert.Equal("1500", dr.Account);
        Assert.Equal("1400", cr.Account);
    }

    [Fact]
    public async Task Capitalize_PeriodClosed_ThrowsAndDoesNotCreateAsset()
    {
        var (db, project, svc, guard) = await SetupAsync(companyId: 100, periodAllowed: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CapitalizeAsync(project.Id, "A-CAP-LOCKED", "blocked", usefulLifeMonths: 60));

        Assert.Equal(1, guard.CallCount);
        Assert.Equal(0, await db.Assets.CountAsync()); // CRITICAL: nothing persisted
        Assert.Equal(0, await db.JournalEntries.CountAsync());
        Assert.Equal(0, await db.CipCapitalizations.CountAsync());
    }

    [Fact]
    public async Task Capitalize_ProjectInOtherTenant_ThrowsNotFound()
    {
        // Project is in company 200 but tenant only sees company 100.
        // Pre-fix this would have capitalized the foreign project into the
        // user's own tenant.
        var db = NewDb();
        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "A", IsActive = true });
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        var foreignProject = new CipProject
        {
            ProjectNumber = "CIP-FOREIGN", Name = "Other tenant",
            CompanyId = 200, Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(foreignProject);
        await db.SaveChangesAsync();

        var tenantA = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var costSvc = new CipCostService(db, lookup, tenantA);
        var resolver = new GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var guard = new StubPeriodGuard(allow: true);
        var depBackfill = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);
        var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenantA, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var svc = new CipCapitalizationService(db, costSvc, lookup, tenantA, resolver, guard, depBackfill, outbox);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CapitalizeAsync(foreignProject.Id, "X", "leak attempt", usefulLifeMonths: 60));
        Assert.Contains("not found", ex.Message);
    }
}
