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
/// Regression tests for S1-6: WO ActualCost is now rolled up from
/// per-operation labor entries (Hours × HourlyRate) + per-operation
/// parts (QuantityUsed × UnitCost) + WO-level WorkOrderParts. Manual
/// fields stay as fallback when operation-level data is absent.
///
/// Per audit S1-6: "Per-operation labor and parts data is computed and
/// displayed but ignored when finalizing. WO ActualCost is whatever the
/// user types... cost flow to Asset uses an unreliable manually-keyed value."
/// </summary>
public class WorkOrderCostRollupTests
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
            .UseInMemoryDatabase($"wo-rollup-{dbName}-{Guid.NewGuid()}")
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

    private static async Task<(AppDbContext db, WorkOrder wo, Abs.FixedAssets.Pages.Maintenance.DetailsModel page)>
        SetupWoAsync(int companyId)
    {
        var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        var asset = new Asset
        {
            AssetNumber = "A-1", Description = "x", CompanyId = companyId,
            AcquisitionCost = 1000m, UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var wo = new WorkOrder
        {
            WorkOrderNumber = "WO-ROLLUP",
            AssetId = asset.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Description = "Rollup test"
        };
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var maintenanceService = new MaintenanceService(db, tenant, lookup);
        var attachmentService = new AttachmentService(db, new StubWebHostEnv(), tenant);
        var originService = new WorkOrderOriginService(db);
        var cipCostService = new CipCostService(db, lookup, tenant);
        var cipAutoCost = new CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var depBackfill = new DepreciationBackfillService(db, new DepreciationService(),
            NullLogger<DepreciationBackfillService>.Instance);

        var page = new Abs.FixedAssets.Pages.Maintenance.DetailsModel(
            maintenanceService, attachmentService, db, originService,
            tenant, lookup, new AlwaysEnabledModuleGuard(), new AllowAllPeriodGuard(),
            cipAutoCost, depBackfill,
            NullLogger<Abs.FixedAssets.Pages.Maintenance.DetailsModel>.Instance);
        WirePageContext(page);

        return (db, wo, page);
    }

    [Fact]
    public async Task OnPostComplete_OperationLaborAndParts_RollUpIntoActualCost()
    {
        var (db, wo, page) = await SetupWoAsync(companyId: 100);

        // Two operations, each with labor entries (Hours × HourlyRate) and parts (Qty × UnitCost).
        var op1 = new WorkOrderOperation
        {
            WorkOrderId = wo.Id,
            OperationNumber = "OP-001",
            Sequence = 10,
            Title = "Inspect",
            Type = OperationType.Inspection,
            Status = OperationStatus.Completed
        };
        var op2 = new WorkOrderOperation
        {
            WorkOrderId = wo.Id,
            OperationNumber = "OP-002",
            Sequence = 20,
            Title = "Replace",
            Type = OperationType.Mechanical,
            Status = OperationStatus.Completed
        };
        db.Set<WorkOrderOperation>().AddRange(op1, op2);
        await db.SaveChangesAsync();

        // Op1: 2 labor entries × $50/hr × 3hr each = 300; 1 part × 5 × $20 = 100. Total: 400.
        // Op2: 1 labor entry  × $80/hr × 4hr     = 320; 0 parts.                       Total: 320.
        // Sum operations: labor=620, parts=100.
        db.Set<WorkOrderOperationLabor>().AddRange(
            new WorkOrderOperationLabor { WorkOrderOperationId = op1.Id, Hours = 3m, HourlyRate = 50m },
            new WorkOrderOperationLabor { WorkOrderOperationId = op1.Id, Hours = 3m, HourlyRate = 50m },
            new WorkOrderOperationLabor { WorkOrderOperationId = op2.Id, Hours = 4m, HourlyRate = 80m }
        );
        // Need an Item for the parts FK to satisfy.
        var item = new Item { PartNumber = "P-1", Description = "Widget" };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        db.Set<WorkOrderOperationPart>().Add(new WorkOrderOperationPart
        {
            WorkOrderOperationId = op1.Id,
            ItemId = item.Id,
            QuantityUsed = 5m,
            UnitCost = 20m
        });
        // Plus a WO-level WorkOrderPart: 2 × $35 = 70.
        db.WorkOrderParts.Add(new WorkOrderPart
        {
            WorkOrderId = wo.Id,
            ItemId = item.Id,
            QuantityUsed = 2m,
            UnitCost = 35m
        });
        await db.SaveChangesAsync();

        // User types $0 for everything; rollup should override labor + parts.
        await page.OnPostCompleteAsync(wo.Id,
            resolution: "done",
            laborCost: 0m,
            materialsCost: 99m,           // user-typed; no operation source
            partsCost: 0m,
            outsideVendorCost: 50m);       // user-typed; no operation source

        var fromDb = await db.WorkOrders.AsNoTracking().FirstAsync(e => e.Id == wo.Id);
        Assert.Equal(620m, fromDb.LaborCost);          // 2×3×50 + 1×4×80
        Assert.Equal(170m, fromDb.PartsCost);          // op-parts 100 + WO parts 70
        Assert.Equal(99m, fromDb.MaterialsCost);       // user input passes through
        Assert.Equal(50m, fromDb.OutsideVendorCost);   // user input passes through
        Assert.Equal(620m + 170m + 99m + 50m, fromDb.ActualCost); // 939
    }

    [Fact]
    public async Task OnPostComplete_NoOperationData_FallsBackToManualInput()
    {
        // When a WO has no operations (or operations with zero labor/parts),
        // the manual user input is preserved — same behavior as pre-S1-6.
        var (db, wo, page) = await SetupWoAsync(companyId: 100);

        await page.OnPostCompleteAsync(wo.Id,
            resolution: "done",
            laborCost: 100m,
            materialsCost: 50m,
            partsCost: 25m,
            outsideVendorCost: 10m);

        var fromDb = await db.WorkOrders.AsNoTracking().FirstAsync(e => e.Id == wo.Id);
        Assert.Equal(100m, fromDb.LaborCost);
        Assert.Equal(50m, fromDb.MaterialsCost);
        Assert.Equal(25m, fromDb.PartsCost);
        Assert.Equal(10m, fromDb.OutsideVendorCost);
        Assert.Equal(185m, fromDb.ActualCost);
    }
}
