using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Maintenance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for period-lock enforcement on financial-impacting
/// writes outside the depreciation paths. The 2026-05-07 code review
/// found that <c>Pages/Receiving/Receive.cshtml.cs</c> and
/// <c>Pages/Maintenance/Details.cshtml.cs::OnPostCompleteAsync</c> both
/// posted state without consulting <see cref="IPeriodGuard"/>. Both fixed.
///
/// These tests prove:
/// 1. With the period closed, neither handler commits the financial state.
/// 2. With the period open, both handlers proceed normally.
/// </summary>
public class PeriodLockEnforcementTests
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
            .UseInMemoryDatabase($"period-lock-{dbName}-{Guid.NewGuid()}")
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

    private sealed class StubWebHostEnv : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>Period guard stub returning a fixed verdict — counts calls.</summary>
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
                Reason = _allow ? null : $"Period for {postingDate:yyyy-MM-dd} is closed (test stub)."
            });
        }

        public Task EnsureCanPostAsync(int companyId, DateTime postingDate) => Task.CompletedTask;
    }

    /// <summary>In-memory TempData provider for tests — avoids the
    /// TempDataSerializer dependency that ASP.NET's SessionStateTempDataProvider
    /// pulls in from the request pipeline.</summary>
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

    // ── Receiving ──────────────────────────────────────────────────────

    [Fact]
    public async Task Receive_OnPostAsync_PeriodClosed_DoesNotCreateGoodsReceipt()
    {
        const int companyId = 100;
        await using var db = NewDb();

        // Seed a PO with one line in a receivable status.
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-RECV-1",
            VendorId = 1,
            CompanyId = companyId,
            Status = POStatus.Approved,
            OrderDate = DateTime.UtcNow,
            Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1,
            Description = "Widget",
            UOM = "EA",
            QuantityOrdered = 10,
            UnitPrice = 1m,
            LineTotal = 10m,
            QuantityReceived = 0
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var periodGuard = new StubPeriodGuard(allow: false);
        var cipCostService = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCost = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var glResolver = new Abs.FixedAssets.Services.GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var receivingPosting = new Abs.FixedAssets.Services.Receiving.ReceivingPostingService(db, tenant, glResolver, outbox, new PassthroughIdempotencyMediator(), NullLogger<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>.Instance);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(db, new AlwaysEnabledModuleGuard(), tenant, lookup, periodGuard, NullLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>.Instance, cipAutoCost, receivingPosting, outbox);
        WirePageContext(page);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        var result = await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(1, periodGuard.CallCount); // proves the guard was consulted
        var receiptCount = await db.GoodsReceipts.CountAsync();
        Assert.Equal(0, receiptCount); // CRITICAL: no receipt persisted
        Assert.Contains("Error", page.TempData.Keys);
    }

    [Fact]
    public async Task Receive_OnPostAsync_PeriodOpen_CreatesGoodsReceipt()
    {
        // Negative test: with the period open, the guard is consulted but
        // doesn't block — the receipt persists.
        const int companyId = 100;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-RECV-2",
            VendorId = 1,
            CompanyId = companyId,
            Status = POStatus.Approved,
            OrderDate = DateTime.UtcNow,
            Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1,
            Description = "Widget",
            UOM = "EA",
            QuantityOrdered = 10,
            UnitPrice = 1m,
            LineTotal = 10m,
            QuantityReceived = 0
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var periodGuard = new StubPeriodGuard(allow: true);
        var cipCostService = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCost = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var glResolver = new Abs.FixedAssets.Services.GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var receivingPosting = new Abs.FixedAssets.Services.Receiving.ReceivingPostingService(db, tenant, glResolver, outbox, new PassthroughIdempotencyMediator(), NullLogger<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>.Instance);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(db, new AlwaysEnabledModuleGuard(), tenant, lookup, periodGuard, NullLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>.Instance, cipAutoCost, receivingPosting, outbox);
        WirePageContext(page);

        var lines = new List<Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        Assert.Equal(1, periodGuard.CallCount);
        var receiptCount = await db.GoodsReceipts.CountAsync();
        Assert.Equal(1, receiptCount); // happy path: receipt persisted
    }

    // ── Maintenance close-out ─────────────────────────────────────────

    [Fact]
    public async Task Maintenance_OnPostCompleteAsync_PeriodClosed_DoesNotCloseEvent()
    {
        const int companyId = 100;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        var asset = new Asset
        {
            AssetNumber = "A-001",
            Description = "Asset",
            CompanyId = companyId,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var evt = new WorkOrder
        {
            WorkOrderNumber = "WO-001",
            AssetId = asset.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.WorkOrders.Add(evt);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var periodGuard = new StubPeriodGuard(allow: false);

        var maintenanceService = new MaintenanceService(db, tenant, lookup);
        var attachmentService = new AttachmentService(db, new StubWebHostEnv(), tenant);
        var originService = new WorkOrderOriginService(db);

        var cipCostServiceM = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCostM = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostServiceM);
        var depBackfillM = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);
        var page = new Abs.FixedAssets.Pages.Maintenance.DetailsModel(
            maintenanceService, attachmentService, db, originService,
            tenant, lookup, new AlwaysEnabledModuleGuard(), periodGuard,
            cipAutoCostM, depBackfillM,
            NullLogger<Abs.FixedAssets.Pages.Maintenance.DetailsModel>.Instance);
        WirePageContext(page);

        var result = await page.OnPostCompleteAsync(evt.Id, "fixed it", 100m, 50m, 25m, 0m);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(1, periodGuard.CallCount);

        // CRITICAL: status must NOT have flipped to Completed, costs NOT written.
        // LaborCost/MaterialsCost are decimal? on WorkOrder — "not written"
        // means they remain null (the seed event never set them).
        var fromDb = await db.WorkOrders.AsNoTracking().FirstAsync(e => e.Id == evt.Id);
        Assert.Equal(MaintenanceStatus.InProgress, fromDb.Status);
        Assert.Null(fromDb.CompletedDate);
        Assert.Null(fromDb.LaborCost);
        Assert.Null(fromDb.MaterialsCost);
        Assert.Contains("Error", page.TempData.Keys);
    }

    [Fact]
    public async Task Maintenance_OnPostCompleteAsync_PeriodOpen_ClosesEventAndPersistsCosts()
    {
        const int companyId = 100;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        var asset = new Asset
        {
            AssetNumber = "A-001",
            Description = "Asset",
            CompanyId = companyId,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var evt = new WorkOrder
        {
            WorkOrderNumber = "WO-002",
            AssetId = asset.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.WorkOrders.Add(evt);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var periodGuard = new StubPeriodGuard(allow: true);

        var maintenanceService = new MaintenanceService(db, tenant, lookup);
        var attachmentService = new AttachmentService(db, new StubWebHostEnv(), tenant);
        var originService = new WorkOrderOriginService(db);

        var cipCostServiceM = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCostM = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostServiceM);
        var depBackfillM = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);
        var page = new Abs.FixedAssets.Pages.Maintenance.DetailsModel(
            maintenanceService, attachmentService, db, originService,
            tenant, lookup, new AlwaysEnabledModuleGuard(), periodGuard,
            cipAutoCostM, depBackfillM,
            NullLogger<Abs.FixedAssets.Pages.Maintenance.DetailsModel>.Instance);
        WirePageContext(page);

        await page.OnPostCompleteAsync(evt.Id, "fixed it", 100m, 50m, 25m, 0m);

        Assert.Equal(1, periodGuard.CallCount);
        var fromDb = await db.WorkOrders.AsNoTracking().FirstAsync(e => e.Id == evt.Id);
        Assert.Equal(MaintenanceStatus.Completed, fromDb.Status);
        Assert.NotNull(fromDb.CompletedDate);
        Assert.Equal(100m, fromDb.LaborCost);
        Assert.Equal(50m, fromDb.MaterialsCost);
        Assert.Equal(175m, fromDb.ActualCost); // 100 + 50 + 25 + 0
    }
}
