using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Cip;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Maintenance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for S2-9: the WO→Asset capitalize button on
/// Pages/Maintenance/Details.cshtml.cs::OnPostCapitalizeAsync now
/// respects PeriodGuard and triggers DepreciationBackfillService.
/// RecomputeAssetAsync — same shape as Pages/Assets/Improve.cshtml.cs.
///
/// Per audit S2-9: "Asset.AcquisitionCost is incremented and
/// CapitalImprovement created with no period gate and no
/// _depBackfill.RecomputeAssetAsync(...) follow-up."
/// </summary>
public class MaintenanceCapitalizePeriodLockTests
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
            .UseInMemoryDatabase($"maint-cap-{dbName}-{Guid.NewGuid()}")
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

    private sealed class StubWebHostEnv : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = System.IO.Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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

    private static async Task<(AppDbContext db, Asset asset, WorkOrder wo, Abs.FixedAssets.Pages.Maintenance.DetailsModel page, StubPeriodGuard guard)>
        SetupAsync(int companyId, bool periodAllowed)
    {
        var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });

        var asset = new Asset
        {
            AssetNumber = "A-1",
            Description = "Asset",
            CompanyId = companyId,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow,
            InServiceDate = new DateTime(2024, 1, 1)
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

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
            BookValue = 1000m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var wo = new WorkOrder
        {
            WorkOrderNumber = "WO-CAP",
            AssetId = asset.Id,
            Status = MaintenanceStatus.Completed,
            CompletedDate = DateTime.UtcNow,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Description = "Compressor swap",
            Type = MaintenanceType.Corrective
        };
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var periodGuard = new StubPeriodGuard(allow: periodAllowed);
        var maintenanceService = new MaintenanceService(db, tenant, lookup);
        var attachmentService = new AttachmentService(db, new StubWebHostEnv(), tenant);
        var originService = new WorkOrderOriginService(db);
        var cipCostService = new CipCostService(db, lookup, tenant);
        var cipAutoCost = new CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var depBackfill = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);

        var page = new Abs.FixedAssets.Pages.Maintenance.DetailsModel(
            maintenanceService, attachmentService, db, originService,
            tenant, lookup, new AlwaysEnabledModuleGuard(), periodGuard,
            cipAutoCost, depBackfill,
            NullLogger<Abs.FixedAssets.Pages.Maintenance.DetailsModel>.Instance);
        WirePageContext(page);

        return (db, asset, wo, page, periodGuard);
    }

    [Fact]
    public async Task OnPostCapitalize_PeriodClosed_DoesNotIncrementAssetCost()
    {
        var (db, asset, wo, page, guard) = await SetupAsync(companyId: 100, periodAllowed: false);

        var result = await page.OnPostCapitalizeAsync(wo.Id, amount: 5000m, description: "improvement");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(1, guard.CallCount);

        var fromDb = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        Assert.Equal(1000m, fromDb.AcquisitionCost); // CRITICAL: unchanged
        Assert.Equal(0, await db.CapitalImprovements.CountAsync()); // no row written
        Assert.Contains("Error", page.TempData.Keys);
    }

    [Fact]
    public async Task OnPostCapitalize_PeriodOpen_IncrementsCostAndRefreshesSnapshot()
    {
        var (db, asset, wo, page, guard) = await SetupAsync(companyId: 100, periodAllowed: true);

        await page.OnPostCapitalizeAsync(wo.Id, amount: 5000m, description: "improvement");

        Assert.Equal(1, guard.CallCount);
        var assetAfter = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        Assert.Equal(6000m, assetAfter.AcquisitionCost); // 1000 + 5000
        Assert.Equal(1, await db.CapitalImprovements.CountAsync());

        // CRITICAL: depreciation snapshot was refreshed (LastDepreciationDate stamped).
        var settingsAfter = await db.AssetBookSettings.AsNoTracking().FirstAsync(s => s.AssetId == asset.Id);
        Assert.NotNull(settingsAfter.LastDepreciationDate);
    }
}
