using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for PR-5 (2026-05-07 code review finding #7). Before
/// this fix, <c>Pages/Assets/Improve.cshtml.cs::OnPostAsync</c> mutated
/// <see cref="Asset.AcquisitionCost"/> and <see cref="Asset.UsefulLifeMonths"/>
/// in place but did not refresh the depreciation snapshot stamped on
/// <see cref="Asset"/> and each <see cref="AssetBookSettings"/>. The
/// asset detail page, KPI dashboard, and schedule report all read from
/// those cached fields — so improvements silently created stale balances
/// until the next full <see cref="DepreciationBackfillService.RunAsync"/>.
///
/// Post-fix the page invokes
/// <see cref="DepreciationBackfillService.RecomputeAssetAsync"/> for the
/// improved asset only, which restamps the snapshot using the new cost
/// basis and useful life. Posted <see cref="JournalEntry"/> rows remain
/// untouched (append-only ledger).
/// </summary>
public class CapitalImprovementRecalcTests
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
            .UseInMemoryDatabase($"cap-impr-{dbName}-{Guid.NewGuid()}")
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

    private sealed class AlwaysEnabledModuleGuard : IModuleGuardService
    {
        public Task<bool> IsModuleEnabledAsync(string moduleName) => Task.FromResult(true);
        public Task<ModuleStatus> GetModuleStatusAsync() => Task.FromResult(new ModuleStatus());
    }

    private sealed class AllowAllPeriodGuard : IPeriodGuard
    {
        public Task<PeriodCheckResult> CanPostAsync(int companyId, DateTime postingDate)
            => Task.FromResult(new PeriodCheckResult { IsAllowed = true });
        public Task EnsureCanPostAsync(int companyId, DateTime postingDate) => Task.CompletedTask;
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object> _store = new();
        public IDictionary<string, object> LoadTempData(HttpContext context) => _store;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _store.Clear();
            foreach (var kvp in values) _store[kvp.Key] = kvp.Value;
        }
    }

    private static void WirePageContext(PageModel page)
    {
        var http = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(http, new RouteData(), new PageActionDescriptor(), modelState);
        page.PageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };
        page.TempData = new TempDataDictionary(http, new InMemoryTempDataProvider());
    }

    private static DepreciationBackfillService MakeBackfill(AppDbContext db) =>
        new DepreciationBackfillService(db, new DepreciationService(), NullLogger<DepreciationBackfillService>.Instance);

    private static Abs.FixedAssets.Services.Webhooks.OutboxWriter MakeOutbox(AppDbContext db, ITenantContext tenant) =>
        new(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);

    /// <summary>No-op stub for <see cref="ICapitalImprovementPostingService"/>.
    /// These tests don't care about JE posting; they care about depreciation
    /// recalculation. The real posting service hits ledger code we don't
    /// exercise here.</summary>
    private sealed class NoopImprovementPosting : ICapitalImprovementPostingService
    {
        public Task<int?> PostImprovementJeAsync(
            int improvementId,
            int assetId,
            int companyId,
            decimal amount,
            DateTime improvementDate,
            string? description = null)
            => Task.FromResult<int?>(null);
    }

    /// <summary>Seeds a vanilla company + asset + GAAP book + AssetBookSettings.
    /// Returns the asset.</summary>
    private static async Task<Asset> SeedAssetWithBookAsync(
        AppDbContext db,
        int companyId,
        decimal cost,
        int lifeMonths,
        DateTime inServiceDate)
    {
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-1", Name = "Co", IsActive = true });

        var asset = new Asset
        {
            AssetNumber = "A-001",
            Description = "Test asset",
            CompanyId = companyId,
            AcquisitionCost = cost,
            SalvageValue = 0,
            UsefulLifeMonths = lifeMonths,
            DepreciationMethod = DepreciationMethod.StraightLine,
            InServiceDate = inServiceDate,
            CreatedAt = DateTime.UtcNow,
            Active = true
        };
        db.Assets.Add(asset);

        var book = new Book
        {
            Code = "GAAP",
            Name = "GAAP Book",
            CompanyId = companyId,
            BookType = BookType.Financial,
            Method = DepreciationMethod.StraightLine,
            Convention = DepreciationConvention.FullMonth,
            IsActive = true,
            GlAccountDepExp = "6500",
            GlAccountAccumDep = "1510"
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();

        db.AssetBookSettings.Add(new AssetBookSettings
        {
            AssetId = asset.Id,
            BookId = book.Id,
            ConventionOverride = DepreciationConvention.FullMonth,
            AccumulatedDepreciation = 0,
            BookValue = cost,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return asset;
    }

    [Fact]
    public async Task RecomputeAssetAsync_AfterCostBumpAndLifeExtension_UpdatesAssetBookSettingsBookValue()
    {
        // Original: $12,000 over 36 months from 2024-01-01. As-of 2025-12-31 (24 months elapsed).
        // Original schedule: monthly = 12000/36 = 333.33; 24 × 333.33 = 7999.92 accumulated, BV = 4000.08.
        // After improvement: cost += 6000, life += 12. New: $18,000 over 48 months.
        // New schedule asOf 2025-12-31 (still 24 months elapsed): monthly = 18000/48 = 375;
        // 24 × 375 = 9000 accumulated, BV = 9000.
        const int companyId = 100;
        await using var db = NewDb();
        var asset = await SeedAssetWithBookAsync(db, companyId, cost: 12000m, lifeMonths: 36,
            inServiceDate: new DateTime(2024, 1, 1));

        // Mutate (mirroring what Improve.cshtml.cs does in production).
        asset.AcquisitionCost += 6000m;
        asset.UsefulLifeMonths += 12;
        await db.SaveChangesAsync();

        var recomputed = await MakeBackfill(db).RecomputeAssetAsync(asset.Id, new DateTime(2025, 12, 31));

        Assert.Equal(1, recomputed); // one AssetBookSettings touched
        var fromDb = await db.AssetBookSettings.AsNoTracking().FirstAsync(s => s.AssetId == asset.Id);
        Assert.Equal(9000m, Math.Round(fromDb.AccumulatedDepreciation, 2));
        Assert.Equal(9000m, Math.Round(fromDb.BookValue, 2));

        // Asset row mirrors the financial book.
        var assetAfter = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        Assert.Equal(9000m, Math.Round(assetAfter.AccumulatedDepreciation, 2));
        Assert.NotNull(assetAfter.BookValue);
        Assert.Equal(9000m, Math.Round(assetAfter.BookValue!.Value, 2));
    }

    [Fact]
    public async Task RecomputeAssetAsync_NoBookSettings_ReturnsZeroAndDoesNotThrow()
    {
        const int companyId = 100;
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-1", Name = "Co", IsActive = true });
        var asset = new Asset
        {
            AssetNumber = "A-NOBOOK",
            Description = "No book settings",
            CompanyId = companyId,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 12,
            DepreciationMethod = DepreciationMethod.StraightLine,
            InServiceDate = new DateTime(2024, 1, 1),
            CreatedAt = DateTime.UtcNow,
            Active = true
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var recomputed = await MakeBackfill(db).RecomputeAssetAsync(asset.Id, new DateTime(2025, 1, 1));
        Assert.Equal(0, recomputed);
    }

    [Fact]
    public async Task RecomputeAssetAsync_NonDepreciableAsset_ReturnsZero()
    {
        // Asset with zero cost is not depreciable; recompute should no-op.
        const int companyId = 100;
        await using var db = NewDb();
        var asset = await SeedAssetWithBookAsync(db, companyId, cost: 0m, lifeMonths: 36,
            inServiceDate: new DateTime(2024, 1, 1));

        var recomputed = await MakeBackfill(db).RecomputeAssetAsync(asset.Id, new DateTime(2025, 12, 31));
        Assert.Equal(0, recomputed);
    }

    [Fact]
    public async Task RecomputeAssetAsync_IsIdempotent_RunningTwiceProducesSameSnapshot()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var asset = await SeedAssetWithBookAsync(db, companyId, cost: 12000m, lifeMonths: 36,
            inServiceDate: new DateTime(2024, 1, 1));

        var asOf = new DateTime(2025, 12, 31);
        var first = await MakeBackfill(db).RecomputeAssetAsync(asset.Id, asOf);
        var snapshot1 = await db.AssetBookSettings.AsNoTracking().FirstAsync(s => s.AssetId == asset.Id);

        var second = await MakeBackfill(db).RecomputeAssetAsync(asset.Id, asOf);
        var snapshot2 = await db.AssetBookSettings.AsNoTracking().FirstAsync(s => s.AssetId == asset.Id);

        Assert.Equal(first, second);
        Assert.Equal(snapshot1.AccumulatedDepreciation, snapshot2.AccumulatedDepreciation);
        Assert.Equal(snapshot1.BookValue, snapshot2.BookValue);
    }

    [Fact]
    public async Task ImproveModel_OnPost_RestampsSnapshotAndDoesNotMutatePostedJournalEntries()
    {
        // End-to-end: Improve.cshtml.cs::OnPostAsync runs the recompute itself
        // post-mutation. Pre-existing JournalEntries (representing prior posted
        // depreciation runs) must be unchanged afterwards — they're an
        // append-only ledger.
        const int companyId = 100;
        await using var db = NewDb();
        var asset = await SeedAssetWithBookAsync(db, companyId, cost: 12000m, lifeMonths: 36,
            inServiceDate: new DateTime(2024, 1, 1));
        var book = await db.Books.FirstAsync();

        // Stub a prior posted JournalEntry. Improve must not re-post or mutate it.
        var preExisting = new JournalEntry
        {
            BookId = book.Id,
            Period = 2024 * 100 + 6,
            Batch = $"DEP-{book.Code}-202406",
            PostingDate = new DateTime(2024, 6, 30),
            Reference = $"DEP {book.Code} 2024-06",
            Source = "DEP",
            Description = "Monthly depreciation — pre-existing",
            CreatedUtc = DateTime.UtcNow.AddMonths(-12),
            Lines = new List<JournalLine>
            {
                new() { LineNo = 1, Account = "6500", Debit = 333.33m, Credit = 0m, Description = "Dep exp" },
                new() { LineNo = 2, Account = "1510", Debit = 0m, Credit = 333.33m, Description = "Acc dep" }
            }
        };
        db.JournalEntries.Add(preExisting);
        await db.SaveChangesAsync();
        var preExistingId = preExisting.Id;

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = new Abs.FixedAssets.Pages.Assets.ImproveModel(
            db, tenant, new AlwaysEnabledModuleGuard(), new AllowAllPeriodGuard(), MakeBackfill(db), MakeOutbox(db, tenant), new NoopImprovementPosting())
        {
            AssetId = asset.Id,
            ImprovementDate = new DateTime(2025, 1, 15),
            Description = "New compressor",
            Cost = 6000m,
            UsefulLifeExtension = 12,
            Capitalize = true
        };
        WirePageContext(page);

        var result = await page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);

        // CRITICAL: snapshot reflects the new cost basis.
        var settingsAfter = await db.AssetBookSettings.AsNoTracking().FirstAsync(s => s.AssetId == asset.Id);
        Assert.True(settingsAfter.BookValue > 0, "BookValue should be re-stamped from the new schedule");
        Assert.True(settingsAfter.AccumulatedDepreciation >= 0);
        Assert.NotNull(settingsAfter.LastDepreciationDate);

        // CRITICAL: the asset got the new cost + new life persisted.
        var assetAfter = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        Assert.Equal(18000m, assetAfter.AcquisitionCost);
        Assert.Equal(48, assetAfter.UsefulLifeMonths);

        // CRITICAL: the pre-existing JournalEntry is untouched (same id, same Debit/Credit).
        var jeAfter = await db.JournalEntries.Include(j => j.Lines).AsNoTracking()
            .FirstAsync(j => j.Id == preExistingId);
        Assert.Equal("DEP", jeAfter.Source);
        Assert.Equal(2, jeAfter.Lines.Count);
        Assert.Equal(333.33m, jeAfter.Lines.OrderBy(l => l.LineNo).First().Debit);
    }

    [Fact]
    public async Task ImproveModel_OnPost_NoLifeExtension_StillUpdatesSnapshotForCostBump()
    {
        // Edge case: capital improvement adds cost but no life extension.
        // The snapshot must still recompute — adding $5k to the basis on a
        // 36-month asset accelerates monthly depreciation from $333.33 to
        // $472.22 ((12000+5000)/36).
        const int companyId = 100;
        await using var db = NewDb();
        var asset = await SeedAssetWithBookAsync(db, companyId, cost: 12000m, lifeMonths: 36,
            inServiceDate: new DateTime(2024, 1, 1));

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = new Abs.FixedAssets.Pages.Assets.ImproveModel(
            db, tenant, new AlwaysEnabledModuleGuard(), new AllowAllPeriodGuard(), MakeBackfill(db), MakeOutbox(db, tenant), new NoopImprovementPosting())
        {
            AssetId = asset.Id,
            ImprovementDate = new DateTime(2025, 1, 15),
            Description = "Cost-only improvement",
            Cost = 5000m,
            UsefulLifeExtension = null,
            Capitalize = true
        };
        WirePageContext(page);

        await page.OnPostAsync();

        var assetAfter = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        Assert.Equal(17000m, assetAfter.AcquisitionCost);
        Assert.Equal(36, assetAfter.UsefulLifeMonths); // life unchanged

        var settingsAfter = await db.AssetBookSettings.AsNoTracking().FirstAsync(s => s.AssetId == asset.Id);
        Assert.NotNull(settingsAfter.LastDepreciationDate); // recompute did run
    }

    [Fact]
    public async Task ImproveModel_OnPost_EmitsAssetImprovedV1OutboxEvent()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var asset = await SeedAssetWithBookAsync(db, companyId, cost: 12000m, lifeMonths: 36,
            inServiceDate: new DateTime(2024, 1, 1));

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = new Abs.FixedAssets.Pages.Assets.ImproveModel(
            db, tenant, new AlwaysEnabledModuleGuard(), new AllowAllPeriodGuard(), MakeBackfill(db), MakeOutbox(db, tenant), new NoopImprovementPosting())
        {
            AssetId = asset.Id,
            ImprovementDate = new DateTime(2025, 6, 1),
            Description = "New blower motor",
            Cost = 1500m,
            UsefulLifeExtension = 6,
            Vendor = "Acme",
            InvoiceNumber = "INV-7",
            Capitalize = true
        };
        WirePageContext(page);

        await page.OnPostAsync();

        var evt = await db.OutboxEvents.SingleAsync(e => e.EventType == "asset.improved");
        Assert.Equal("Asset", evt.EntityType);
        Assert.Equal(asset.Id.ToString(), evt.EntityId);
        Assert.Equal(companyId, evt.CompanyId);

        using var doc = System.Text.Json.JsonDocument.Parse(evt.PayloadJson);
        var root = doc.RootElement;
        Assert.Equal(asset.Id, root.GetProperty("assetId").GetInt32());
        Assert.Equal(1500m, root.GetProperty("cost").GetDecimal());
        Assert.Equal(13500m, root.GetProperty("newAcquisitionCost").GetDecimal());
        Assert.Equal(42, root.GetProperty("newUsefulLifeMonths").GetInt32());
        Assert.True(root.GetProperty("capitalized").GetBoolean());
        Assert.Equal("Acme", root.GetProperty("vendor").GetString());
        Assert.Equal("INV-7", root.GetProperty("invoiceNumber").GetString());
    }
}
