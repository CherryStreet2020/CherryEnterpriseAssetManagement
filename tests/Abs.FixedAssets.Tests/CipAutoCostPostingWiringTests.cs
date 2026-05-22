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
/// Regression tests for S1-3: Wire <see cref="CipAutoCostPostingService"/>
/// into Receiving + AP + Maintenance close-out so PO/Invoice/WO with
/// <c>CipProjectId</c> actually produce <c>CipCost</c> rows.
///
/// Per the 2026-05-08 structural audit (S1-3), these three Post-from-*
/// methods existed but were never invoked — making the entire CIP cost
/// accumulation chain dead. Tests prove the wiring is now live.
/// </summary>
public class CipAutoCostPostingWiringTests
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
            .UseInMemoryDatabase($"cip-wire-{dbName}-{Guid.NewGuid()}")
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

    private static (LookupService lookup, CipCostService costSvc, CipAutoCostPostingService autoSvc)
        BuildCipServices(AppDbContext db, ITenantContext tenant)
    {
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var costSvc = new CipCostService(db, lookup, tenant);
        var autoSvc = new CipAutoCostPostingService(db, lookup, tenant, costSvc);
        return (lookup, costSvc, autoSvc);
    }

    [Fact]
    public async Task Receive_LineWithCipProjectIdOnPoLine_PostsCipCost()
    {
        const int companyId = 100;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var project = new CipProject
        {
            ProjectNumber ="CIP-001",
            Name = "Test CIP",
            CompanyId = companyId,
            Status = CipProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-CIP-1",
            VendorId = 1,
            CompanyId = companyId,
            Status = POStatus.Approved,
            OrderDate = DateTime.UtcNow,
            Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1,
            Description = "Equipment for CIP",
            UOM = "EA",
            QuantityOrdered = 10,
            UnitPrice = 100m,
            LineTotal = 1000m,
            QuantityReceived = 0,
            CipProjectId = project.Id
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var (lookup, _, cipAutoCost) = BuildCipServices(db, tenant);

        var glResolverR = new Abs.FixedAssets.Services.GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var outboxR = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var receivingPostingR = new Abs.FixedAssets.Services.Receiving.ReceivingPostingService(db, tenant, glResolverR, outboxR, new PassthroughIdempotencyMediator(), new Abs.FixedAssets.Tests.TestHelpers.NullChainOfCustodyService(), NullLogger<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>.Instance);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(
            db, new AlwaysEnabledModuleGuard(), tenant, lookup, new AllowAllPeriodGuard(),
            NullLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>.Instance, cipAutoCost, receivingPostingR, outboxR);
        WirePageContext(page);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        var cipCosts = await db.CipCosts.Where(c => c.CipProjectId == project.Id).ToListAsync();
        Assert.Single(cipCosts); // CRITICAL: receipt routed to CIP
        // SourceType is uppercased on SaveChanges by AppDbContext.CapitalizeStringProperties
        // (the field isn't in the allowlist). Asserting post-save form.
        Assert.Equal("RECEIPT", cipCosts[0].SourceType);
        Assert.Equal(500m, cipCosts[0].Amount); // 5 × $100
    }

    [Fact]
    public async Task Receive_LineWithoutCipProjectId_DoesNotPostCipCost()
    {
        const int companyId = 100;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-NORMAL",
            VendorId = 1,
            CompanyId = companyId,
            Status = POStatus.Approved,
            OrderDate = DateTime.UtcNow,
            Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1,
            Description = "Normal stock",
            UOM = "EA",
            QuantityOrdered = 10,
            UnitPrice = 100m,
            LineTotal = 1000m,
            QuantityReceived = 0
            // No CipProjectId
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var (lookup, _, cipAutoCost) = BuildCipServices(db, tenant);

        var glResolverR = new Abs.FixedAssets.Services.GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var outboxR = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var receivingPostingR = new Abs.FixedAssets.Services.Receiving.ReceivingPostingService(db, tenant, glResolverR, outboxR, new PassthroughIdempotencyMediator(), new Abs.FixedAssets.Tests.TestHelpers.NullChainOfCustodyService(), NullLogger<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>.Instance);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(
            db, new AlwaysEnabledModuleGuard(), tenant, lookup, new AllowAllPeriodGuard(),
            NullLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>.Instance, cipAutoCost, receivingPostingR, outboxR);
        WirePageContext(page);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        Assert.Equal(0, await db.CipCosts.CountAsync());
    }

    [Fact]
    public async Task Receive_DuplicatePostFromSameLine_DoesNotDoublePost()
    {
        // The CipAutoCostPostingService's idempotency guard checks for an
        // existing (SourceType=Receipt, SourceLineId, CipProjectId) match
        // before inserting. Two consecutive receipt-saves against the same
        // line should produce exactly one CipCost.
        const int companyId = 100;
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        var project = new CipProject { ProjectNumber = "CIP-002", Name = "Test", CompanyId = companyId, Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var (_, _, cipAutoCost) = BuildCipServices(db, tenant);

        // Build a GR + line directly (skipping the page handler) so we can call
        // PostFromReceiptLineAsync twice on the same line id.
        var po = new PurchaseOrder
        {
            PONumber = "PO-IDEM", VendorId = 1, CompanyId = companyId, Status = POStatus.Approved,
            OrderDate = DateTime.UtcNow, Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1, Description = "x", UOM = "EA", QuantityOrdered = 10,
            UnitPrice = 100m, LineTotal = 1000m, CipProjectId = project.Id
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var receipt = new GoodsReceipt
        {
            ReceiptNumber = "GR-IDEM", PurchaseOrderId = po.Id,
            Status = ReceiptStatus.Received, ReceiptDate = DateTime.Today,
            ReceivedBy = "test", CompanyId = companyId, CreatedAt = DateTime.UtcNow
        };
        receipt.Lines.Add(new GoodsReceiptLine
        {
            PurchaseOrderLineId = po.Lines.First().Id, LineNumber = 1,
            QuantityReceived = 5, QuantityAccepted = 5
        });
        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync();

        var receiptLineId = receipt.Lines.First().Id;
        await cipAutoCost.PostFromReceiptLineAsync(receiptLineId);
        await cipAutoCost.PostFromReceiptLineAsync(receiptLineId);
        await cipAutoCost.PostFromReceiptLineAsync(receiptLineId);

        Assert.Equal(1, await db.CipCosts.CountAsync());
    }

    [Fact]
    public async Task Maintenance_OnPostCompleteAsync_WithCipProjectId_PostsCipCost()
    {
        const int companyId = 100;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });

        var project = new CipProject
        {
            ProjectNumber ="CIP-WO",
            Name = "WO Cost Project",
            CompanyId = companyId,
            Status = CipProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);

        var asset = new Asset
        {
            AssetNumber = "A-WO",
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
            WorkOrderNumber = "WO-CIP",
            AssetId = asset.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CipProjectId = project.Id
        };
        db.WorkOrders.Add(evt);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var (lookup, _, cipAutoCost) = BuildCipServices(db, tenant);

        var maintenanceService = new MaintenanceService(db, tenant, lookup);
        var attachmentService = new AttachmentService(db, new StubWebHostEnv(), tenant);
        var originService = new WorkOrderOriginService(db);

        var depBackfill = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);
        var page = new Abs.FixedAssets.Pages.Maintenance.DetailsModel(
            maintenanceService, attachmentService, db, originService,
            tenant, lookup, new AlwaysEnabledModuleGuard(), new AllowAllPeriodGuard(),
            cipAutoCost, depBackfill, NullLogger<Abs.FixedAssets.Pages.Maintenance.DetailsModel>.Instance);
        WirePageContext(page);

        // Close the WO with $250 in labor only. PR #268 (2026-05-20) refactored
        // PostFromWorkOrderAsync to also post OutsideServices when present —
        // but with materialsCost/outsideVendorCost = 0, only the Labor row
        // should land. Validates the existing labor-only path still works.
        await page.OnPostCompleteAsync(evt.Id, "fixed", laborCost: 250m, materialsCost: 0m, partsCost: 0m, outsideVendorCost: 0m);

        var cipCosts = await db.CipCosts.Where(c => c.CipProjectId == project.Id).ToListAsync();
        Assert.Single(cipCosts);
        Assert.Equal("WORKORDER", cipCosts[0].SourceType);
        Assert.Equal(250m, cipCosts[0].Amount);
        Assert.Equal(CipCostType.Labor, cipCosts[0].CostType);
    }

    /// <summary>
    /// PR #268 — verify the OutsideServices posting path. Capital project work
    /// order with $250 labor + $500 outside-vendor cost should produce TWO
    /// CipCost rows (Labor + OutsideServices), not one. Before this PR the
    /// outside-vendor portion was silently dropped (CIP under-capitalized).
    /// </summary>
    [Fact]
    public async Task ClosingCipWorkOrder_PostsLaborAndOutsideServicesAsTwoCipCostRows()
    {
        const int companyId = 268;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-268", Name = "Co 268", IsActive = true });

        var project = new CipProject
        {
            ProjectNumber = "CIP-OUT-001",
            Name = "Outside-Vendor Capital Build",
            CompanyId = companyId,
            Status = CipProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);

        var asset = new Asset
        {
            AssetNumber = "AST-CIP-OUT",
            Description = "Capital Press Outside-Vendor Build",
            CompanyId = companyId,
            AcquisitionCost = 5000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var evt = new WorkOrder
        {
            WorkOrderNumber = "WO-CIP-OUT",
            AssetId = asset.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CipProjectId = project.Id
        };
        db.WorkOrders.Add(evt);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var (lookup, _, cipAutoCost) = BuildCipServices(db, tenant);

        var maintenanceService = new MaintenanceService(db, tenant, lookup);
        var attachmentService = new AttachmentService(db, new StubWebHostEnv(), tenant);
        var originService = new WorkOrderOriginService(db);
        var depBackfill = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);
        var page = new Abs.FixedAssets.Pages.Maintenance.DetailsModel(
            maintenanceService, attachmentService, db, originService,
            tenant, lookup, new AlwaysEnabledModuleGuard(), new AllowAllPeriodGuard(),
            cipAutoCost, depBackfill, NullLogger<Abs.FixedAssets.Pages.Maintenance.DetailsModel>.Instance);
        WirePageContext(page);

        await page.OnPostCompleteAsync(evt.Id, "fixed",
            laborCost: 250m,
            materialsCost: 0m,
            partsCost: 0m,
            outsideVendorCost: 500m);

        var cipCosts = await db.CipCosts
            .Where(c => c.CipProjectId == project.Id)
            .OrderBy(c => c.CostType)
            .ToListAsync();

        Assert.Equal(2, cipCosts.Count);

        var laborRow = cipCosts.Single(c => c.CostType == CipCostType.Labor);
        Assert.Equal(250m, laborRow.Amount);
        Assert.Equal("WORKORDER", laborRow.SourceType);
        Assert.Contains("Labor from WO", laborRow.Description);

        var outsideRow = cipCosts.Single(c => c.CostType == CipCostType.OutsideServices);
        Assert.Equal(500m, outsideRow.Amount);
        Assert.Equal("WORKORDER", outsideRow.SourceType);
        Assert.Contains("Outside services from WO", outsideRow.Description);

        // Capitalized total should reflect both rows — the bug PR #268 fixed.
        Assert.Equal(750m, cipCosts.Sum(c => c.Amount));
    }

    /// <summary>
    /// PR #268 documents that stock-issued materials are NOT auto-posted from
    /// the WO to CIP — they require an accountant manual journal entry until
    /// the Sprint 14+ Controller Cockpit / CIP Capitalization Wizard ships
    /// dedupe-aware material posting. This test locks that deliberate gap
    /// so a future change either updates the test (with dedupe logic) or
    /// fails fast (catches regression).
    /// </summary>
    [Fact]
    public async Task ClosingCipWorkOrder_DoesNotAutoPostMaterialsFromWoHeader()
    {
        const int companyId = 269;
        await using var db = NewDb();

        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-269", Name = "Co 269", IsActive = true });

        var project = new CipProject
        {
            ProjectNumber = "CIP-MAT-001",
            Name = "Materials Gap Documentation",
            CompanyId = companyId,
            Status = CipProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);

        var asset = new Asset
        {
            AssetNumber = "AST-CIP-MAT",
            Description = "Capital Press Materials-Gap Test",
            CompanyId = companyId,
            AcquisitionCost = 5000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var evt = new WorkOrder
        {
            WorkOrderNumber = "WO-CIP-MAT",
            AssetId = asset.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CipProjectId = project.Id
        };
        db.WorkOrders.Add(evt);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var (lookup, _, cipAutoCost) = BuildCipServices(db, tenant);

        var maintenanceService = new MaintenanceService(db, tenant, lookup);
        var attachmentService = new AttachmentService(db, new StubWebHostEnv(), tenant);
        var originService = new WorkOrderOriginService(db);
        var depBackfill = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);
        var page = new Abs.FixedAssets.Pages.Maintenance.DetailsModel(
            maintenanceService, attachmentService, db, originService,
            tenant, lookup, new AlwaysEnabledModuleGuard(), new AllowAllPeriodGuard(),
            cipAutoCost, depBackfill, NullLogger<Abs.FixedAssets.Pages.Maintenance.DetailsModel>.Instance);
        WirePageContext(page);

        // WO has $100 labor + $300 materials (issued from stock). Per PR #268
        // materials must NOT auto-post — only the Labor row should land. The
        // accountant captures stock-issued materials via manual JE in the
        // meantime.
        await page.OnPostCompleteAsync(evt.Id, "fixed",
            laborCost: 100m,
            materialsCost: 300m,
            partsCost: 300m,
            outsideVendorCost: 0m);

        var cipCosts = await db.CipCosts.Where(c => c.CipProjectId == project.Id).ToListAsync();
        Assert.Single(cipCosts);
        Assert.Equal(CipCostType.Labor, cipCosts[0].CostType);
        Assert.Equal(100m, cipCosts[0].Amount);
        // Materials NOT in CIP table from this path:
        Assert.DoesNotContain(cipCosts, c => c.CostType == CipCostType.Materials);
    }
}
