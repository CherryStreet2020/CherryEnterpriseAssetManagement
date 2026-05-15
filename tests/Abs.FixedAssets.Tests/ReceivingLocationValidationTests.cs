using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
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
/// DEF-008: best-in-class item-location preference.
///
/// Pre-fix: the receive form silently accepted submission with no
/// ReceivingLocationId on stock items. The GR persisted but the posting
/// service skipped inventory movement (no ItemInventory update, no
/// item.received outbox event). Discovered during the 2026-05-15 smoke
/// run after PR #63's BookId fix unblocked the JE posting.
///
/// Post-fix: stock items REQUIRE a put-away location either via the
/// per-line picker or the form-level default. Service items remain
/// optional. The cascade for the pre-filled per-line suggestion is
/// ItemCompanyStocking.DefaultLocationId → Item.DefaultLocationId.
/// </summary>
public class ReceivingLocationValidationTests
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
            .UseInMemoryDatabase($"recv-loc-{dbName}-{Guid.NewGuid()}")
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

    private sealed class AllowAllPeriodGuard : IPeriodGuard
    {
        public Task<PeriodCheckResult> CanPostAsync(int companyId, DateTime postingDate)
            => Task.FromResult(new PeriodCheckResult { IsAllowed = true });
        public Task EnsureCanPostAsync(int companyId, DateTime postingDate) => Task.CompletedTask;
    }

    private sealed class AlwaysEnabledModuleGuard : IModuleGuardService
    {
        public Task<bool> IsModuleEnabledAsync(string moduleName) => Task.FromResult(true);
        public Task<ModuleStatus> GetModuleStatusAsync() => Task.FromResult(new ModuleStatus());
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

    private static async Task<(AppDbContext db, PurchaseOrder po, Item item, Location location)>
        SeedStockPoAsync(int companyId)
    {
        var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = $"C-{companyId}", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var item = new Item
        {
            PartNumber = "BOLT-M8",
            Description = "M8 bolt",
            Type = ItemType.Part,   // ← STOCK
            CompanyId = companyId,
            StandardCost = 1m
        };
        db.Items.Add(item);
        var loc = new Location { Code = "BIN-A1", Name = "Bin A1", IsActive = true };
        db.Locations.Add(loc);
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-STOCK-1",
            VendorId = 1,
            CompanyId = companyId,
            Status = POStatus.Approved,
            OrderDate = DateTime.UtcNow,
            Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1,
            Description = "M8 bolts",
            UOM = "EA",
            QuantityOrdered = 10,
            UnitPrice = 1m,
            LineTotal = 10m,
            QuantityReceived = 0,
            ItemId = item.Id
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        return (db, po, item, loc);
    }

    private static Abs.FixedAssets.Pages.Receiving.ReceiveModel BuildPage(AppDbContext db, ITenantContext tenant)
    {
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var glResolver = new GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var receivingPosting = new Abs.FixedAssets.Services.Receiving.ReceivingPostingService(db, tenant, glResolver, outbox, NullLogger<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>.Instance);
        var cipCostSvc = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCost = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostSvc);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(
            db, new AlwaysEnabledModuleGuard(), tenant, lookup, new AllowAllPeriodGuard(),
            NullLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>.Instance,
            cipAutoCost, receivingPosting, outbox);
        WirePageContext(page);
        return page;
    }

    [Fact]
    public async Task OnPostReceive_StockItem_NoLocation_BlocksAndDoesNotPersistReceipt()
    {
        const int companyId = 100;
        var (db, po, _, _) = await SeedStockPoAsync(companyId);
        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = BuildPage(db, tenant);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            // NO ReceivingLocationId — should fail validation
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        var result = await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(0, await db.GoodsReceipts.CountAsync());
    }

    [Fact]
    public async Task OnPostReceive_StockItem_PerLineLocation_PersistsReceipt()
    {
        const int companyId = 100;
        var (db, po, _, loc) = await SeedStockPoAsync(companyId);
        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = BuildPage(db, tenant);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5, ReceivingLocationId = loc.Id }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        var gr = await db.GoodsReceipts.Include(g => g.Lines).FirstOrDefaultAsync();
        Assert.NotNull(gr);
        Assert.Single(gr!.Lines);
        Assert.Equal(loc.Id, gr.Lines.First().ReceivingLocationId);
    }

    [Fact]
    public async Task OnPostReceive_StockItem_FormLevelDefaultFallback_PersistsReceipt()
    {
        const int companyId = 100;
        var (db, po, _, loc) = await SeedStockPoAsync(companyId);
        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = BuildPage(db, tenant);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            // No per-line, but form-level default supplied below
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, loc.Id, null);

        var gr = await db.GoodsReceipts.Include(g => g.Lines).FirstOrDefaultAsync();
        Assert.NotNull(gr);
        Assert.Equal(loc.Id, gr!.Lines.First().ReceivingLocationId);
    }

    [Fact]
    public async Task OnPostReceive_ServiceItem_NoLocation_StillPersists()
    {
        const int companyId = 100;
        var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();
        var item = new Item { PartNumber = "SVC-1", Description = "Service call", Type = ItemType.Service, CompanyId = companyId };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        var po = new PurchaseOrder
        {
            PONumber = "PO-SVC-1", VendorId = 1, CompanyId = companyId,
            Status = POStatus.Approved, OrderDate = DateTime.UtcNow, Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1, Description = "Inspection", UOM = "EA",
            QuantityOrdered = 1, UnitPrice = 100m, LineTotal = 100m, ItemId = item.Id
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = BuildPage(db, tenant);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 1 }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        Assert.Equal(1, await db.GoodsReceipts.CountAsync());
    }

    [Fact]
    public async Task OnGet_PreFillsCascade_FromItemCompanyStockingThenItem()
    {
        const int companyId = 100;
        var (db, po, item, locA) = await SeedStockPoAsync(companyId);

        var locB = new Location { Code = "BIN-B2", Name = "Bin B2", IsActive = true };
        db.Locations.Add(locB);
        await db.SaveChangesAsync();

        // Set Item.DefaultLocationId = locA, ItemCompanyStocking.DefaultLocationId = locB.
        // Cascade should pick locB (per-company wins).
        item.DefaultLocationId = locA.Id;
        db.ItemCompanyStockings.Add(new ItemCompanyStocking
        {
            ItemId = item.Id,
            CompanyId = companyId,
            DefaultLocationId = locB.Id
        });
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var page = BuildPage(db, tenant);

        await page.OnGetAsync(po.Id);

        var line = page.Lines.Single();
        Assert.True(line.IsStockItem);
        Assert.Equal(locB.Id, line.ReceivingLocationId);  // per-company wins
        Assert.Equal(locB.Id, line.SuggestedDefaultLocationId);
    }
}
