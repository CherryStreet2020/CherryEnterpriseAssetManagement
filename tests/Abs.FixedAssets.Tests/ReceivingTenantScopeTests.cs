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
/// Regression tests for PR-6 (2026-05-07 code review followup). The
/// Receiving page had the same conditional tenant-scope shape as the
/// AccountsPayable leak fixed in PR #22:
///
///     var query = ...;
///     if (_tenantContext.CompanyId.HasValue)
///         query = query.Where(p => visibleCompanyIds.Contains(...));
///
/// A user with CompanyId=null but VisibleCompanyIds=[some other company]
/// could load any PO across tenants. Both <c>OnGetAsync</c> and
/// <c>OnPostReceiveAsync</c> had the bug.
///
/// Post-fix the company filter is mandatory on both handlers; site
/// scoping remains conditional (it's a sub-scope of company).
/// </summary>
public class ReceivingTenantScopeTests
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
            .UseInMemoryDatabase($"recv-scope-{dbName}-{Guid.NewGuid()}")
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

    private static async Task<PurchaseOrder> SeedPoAsync(AppDbContext db, int companyId, string poNumber = "PO-A")
    {
        if (!await db.Companies.AnyAsync(c => c.Id == companyId))
            db.Companies.Add(new Company { Id = companyId, CompanyCode = $"C-{companyId}", Name = $"Co{companyId}", IsActive = true });
        if (!await db.Vendors.AnyAsync(v => v.CompanyId == companyId))
            db.Vendors.Add(new Vendor { Code = $"V-{companyId}", Name = $"V{companyId}", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var vendor = await db.Vendors.FirstAsync(v => v.CompanyId == companyId);
        var po = new PurchaseOrder
        {
            PONumber = poNumber,
            VendorId = vendor.Id,
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
        return po;
    }

    private static Abs.FixedAssets.Pages.Receiving.ReceiveModel NewPage(AppDbContext db, ITenantContext tenant)
    {
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var cipCostService = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCost = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var glResolver = new Abs.FixedAssets.Services.GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        var receivingPosting = new Abs.FixedAssets.Services.Receiving.ReceivingPostingService(db, tenant, glResolver, outbox, new PassthroughIdempotencyMediator(), new Abs.FixedAssets.Tests.TestHelpers.NullChainOfCustodyService(), NullLogger<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>.Instance);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(
            db, new AlwaysEnabledModuleGuard(), tenant, lookup,
            new AllowAllPeriodGuard(), NullLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>.Instance,
            cipAutoCost, receivingPosting, outbox);
        WirePageContext(page);
        return page;
    }

    [Fact]
    public async Task OnGetAsync_TenantHasNullCompanyAndOtherCompanyVisible_DoesNotLoadCrossTenantPo()
    {
        // The exact bug shape: tenant has CompanyId=null, VisibleCompanyIds
        // restricted to a company that does NOT own the PO. Pre-fix this
        // skipped the company filter and returned the PO. Post-fix it 404s.
        const int otherCompanyId = 200;
        const int poCompanyId = 100;

        await using var db = NewDb();
        var po = await SeedPoAsync(db, poCompanyId, "PO-100");

        var tenant = new StubTenantContext
        {
            CompanyId = null,                                     // bug trigger
            VisibleCompanyIds = new() { otherCompanyId }          // CANNOT see company 100
        };
        var page = NewPage(db, tenant);

        var result = await page.OnGetAsync(po.Id);

        Assert.IsType<NotFoundResult>(result); // CRITICAL: do not load cross-tenant
        Assert.Null(page.PO); // PO model not populated
    }

    [Fact]
    public async Task OnGetAsync_TenantHasEmptyVisibleCompanies_ReturnsNotFound()
    {
        // Edge case: user with no company access at all.
        await using var db = NewDb();
        var po = await SeedPoAsync(db, 100, "PO-100");

        var tenant = new StubTenantContext
        {
            CompanyId = null,
            VisibleCompanyIds = new()
        };
        var page = NewPage(db, tenant);

        var result = await page.OnGetAsync(po.Id);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_TenantHasMatchingCompanyVisible_LoadsPo()
    {
        // Happy path: tenant CAN see the PO's company, page renders.
        const int companyId = 100;
        await using var db = NewDb();
        var po = await SeedPoAsync(db, companyId, "PO-100");

        var tenant = new StubTenantContext
        {
            CompanyId = companyId,
            VisibleCompanyIds = new() { companyId }
        };
        var page = NewPage(db, tenant);

        var result = await page.OnGetAsync(po.Id);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(page.PO);
        Assert.Equal("PO-100", page.PO.PONumber);
    }

    [Fact]
    public async Task OnPostReceiveAsync_TenantHasNullCompanyAndOtherCompanyVisible_DoesNotPostReceiptCrossTenant()
    {
        // Same bug shape as OnGetAsync — must be fixed in BOTH handlers.
        // Without the fix, a CompanyId=null user could POST a receipt
        // against a PO in another company, creating a cross-tenant
        // GoodsReceipt + bumping QuantityReceived on a foreign PO line.
        const int otherCompanyId = 200;
        const int poCompanyId = 100;

        await using var db = NewDb();
        var po = await SeedPoAsync(db, poCompanyId, "PO-100");

        var tenant = new StubTenantContext
        {
            CompanyId = null,
            VisibleCompanyIds = new() { otherCompanyId }
        };
        var page = NewPage(db, tenant);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        var result = await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, await db.GoodsReceipts.CountAsync()); // CRITICAL: nothing posted
        var poAfter = await db.PurchaseOrders.Include(p => p.Lines).AsNoTracking().FirstAsync(p => p.Id == po.Id);
        Assert.Equal(0m, poAfter.Lines.First().QuantityReceived); // line not mutated
    }
}
