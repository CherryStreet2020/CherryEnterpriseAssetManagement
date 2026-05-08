using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Maintenance;
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for S1-7: WO part issuance + return now decrements
/// (or increments) ItemInventory and creates an ItemTransaction audit
/// row. Plus the cross-tenant Items leak in the picker is closed.
///
/// Per audit S1-7: "OnPostIssueMaterialAsync only updates the
/// WorkOrderPart counter columns. No ItemInventory.QuantityOnHand
/// decrement, no ItemTransaction(Type=Issue), no GL post."
/// </summary>
public class WorkOrderPartIssuanceLedgerTests
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
            .UseInMemoryDatabase($"wo-issue-{dbName}-{Guid.NewGuid()}")
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

    /// <summary>Minimal ICloseoutService stub — none of the issuance handlers
    /// call into this, so a throwing implementation surfaces any unintended
    /// dependency immediately.</summary>
    private sealed class UnusedCloseoutService : ICloseoutService
    {
        public Task<CloseoutResult> CloseWorkOrderAsync(int workOrderId, string? lessonsLearned, string username, bool allowIncompleteOperations = false)
            => throw new NotImplementedException("Test does not exercise the close path");
        public Task<LessonSaveResult> SaveLessonAsync(int workOrderId, string lessonText, string? tags, string username)
            => throw new NotImplementedException();
        public Task<List<RecurringFailure>> GetRecurringFailuresAsync(int days = 30, int limit = 5)
            => throw new NotImplementedException();
        public string GenerateCloseoutSummary(MaintenanceEvent workOrder, List<WorkOrderOperation>? operations = null)
            => throw new NotImplementedException();
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
        http.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "alice")
            }, "TestAuth"));
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(http, new RouteData(), new PageActionDescriptor(), modelState);
        page.PageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };
        page.TempData = new TempDataDictionary(http, new InMemoryTempDataProvider());
    }

    private static async Task<(AppDbContext db, WorkOrderPart part, Abs.FixedAssets.Pages.WorkOrders.DetailsModel page)>
        SetupAsync(int companyId, decimal initialOnHand)
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

        var location = new Location { Name = "STOCKROOM", IsActive = true };
        db.Locations.Add(location);

        var item = new Item
        {
            PartNumber = "P-1",
            Description = "Widget",
            CompanyId = companyId,
            StandardCost = 12.50m
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.Set<ItemInventory>().Add(new ItemInventory
        {
            ItemId = item.Id,
            LocationId = location.Id,
            CompanyId = companyId,
            QuantityOnHand = initialOnHand,
            CreatedAt = DateTime.UtcNow
        });

        var wo = new MaintenanceEvent
        {
            WorkOrderNumber = "WO-1",
            AssetId = asset.Id,
            Status = MaintenanceStatus.InProgress,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.MaintenanceEvents.Add(wo);
        await db.SaveChangesAsync();

        var part = new WorkOrderPart
        {
            MaintenanceEventId = wo.Id,
            ItemId = item.Id,
            QuantityPlanned = 10m,
            UnitCost = 12.50m,
            IssuedFromLocationId = location.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.WorkOrderParts.Add(part);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var page = new Abs.FixedAssets.Pages.WorkOrders.DetailsModel(
            db, new UnusedCloseoutService(), lookup, tenant, new AlwaysEnabledModuleGuard());
        WirePageContext(page);

        return (db, part, page);
    }

    [Fact]
    public async Task OnPostIssueMaterial_DecrementsItemInventoryAndCreatesItemTransaction()
    {
        var (db, part, page) = await SetupAsync(companyId: 100, initialOnHand: 10m);

        await page.OnPostIssueMaterialAsync(part.Id, quantityIssue: 3m);

        // CRITICAL: inventory decremented
        var inv = await db.Set<ItemInventory>().AsNoTracking().FirstAsync(i => i.ItemId == part.ItemId);
        Assert.Equal(7m, inv.QuantityOnHand);
        Assert.NotNull(inv.LastIssueDate);

        // CRITICAL: ItemTransaction audit row created
        var txn = await db.Set<ItemTransaction>().AsNoTracking().SingleAsync();
        Assert.Equal(TransactionType.Issue, txn.Type);
        Assert.Equal(3m, txn.Quantity);
        Assert.Equal(part.UnitCost, txn.UnitCost);
        Assert.Equal(part.IssuedFromLocationId, txn.FromLocationId);

        // Counter still updated.
        var partAfter = await db.WorkOrderParts.AsNoTracking().FirstAsync(p => p.Id == part.Id);
        Assert.Equal(3m, partAfter.QuantityIssued);
        Assert.Equal(3m, partAfter.QuantityUsed);
    }

    [Fact]
    public async Task OnPostReturnMaterial_IncrementsItemInventoryAndCreatesReturnTransaction()
    {
        var (db, part, page) = await SetupAsync(companyId: 100, initialOnHand: 10m);

        // Issue 5, then return 2 → net 3 used, inventory 10-5+2=7.
        await page.OnPostIssueMaterialAsync(part.Id, 5m);
        await page.OnPostReturnMaterialAsync(part.Id, 2m);

        var inv = await db.Set<ItemInventory>().AsNoTracking().FirstAsync(i => i.ItemId == part.ItemId);
        Assert.Equal(7m, inv.QuantityOnHand); // 10 - 5 + 2

        var txns = await db.Set<ItemTransaction>().AsNoTracking().ToListAsync();
        Assert.Equal(2, txns.Count);
        Assert.Single(txns, t => t.Type == TransactionType.Issue && t.Quantity == 5m);
        Assert.Single(txns, t => t.Type == TransactionType.Return && t.Quantity == 2m);

        var partAfter = await db.WorkOrderParts.AsNoTracking().FirstAsync(p => p.Id == part.Id);
        Assert.Equal(5m, partAfter.QuantityIssued);
        Assert.Equal(2m, partAfter.QuantityReturned);
        Assert.Equal(3m, partAfter.QuantityUsed);
    }

    [Fact]
    public async Task OnPostIssueMaterial_NoSourceLocation_StillCreatesTransactionButNoInventoryRow()
    {
        // Edge case: WorkOrderPart without IssuedFromLocationId. We log
        // the transaction (audit) but skip the per-location inventory
        // update — ops can reconcile later.
        var (db, part, page) = await SetupAsync(companyId: 100, initialOnHand: 10m);
        part.IssuedFromLocationId = null;
        await db.SaveChangesAsync();

        await page.OnPostIssueMaterialAsync(part.Id, 4m);

        // Inventory row at that location is unchanged (10).
        var inv = await db.Set<ItemInventory>().AsNoTracking().FirstAsync(i => i.ItemId == part.ItemId);
        Assert.Equal(10m, inv.QuantityOnHand);

        // Transaction still logged.
        var txn = await db.Set<ItemTransaction>().AsNoTracking().SingleAsync();
        Assert.Equal(TransactionType.Issue, txn.Type);
        Assert.Equal(4m, txn.Quantity);
        Assert.Null(txn.FromLocationId);
    }
}
