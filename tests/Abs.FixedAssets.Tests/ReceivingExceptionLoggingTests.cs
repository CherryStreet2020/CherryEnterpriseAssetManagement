using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for PR-4 (2026-05-07 code review smell #6). The
/// receipt-save catch block in <c>Pages/Receiving/Receive.cshtml.cs</c>
/// used to swallow every exception with a generic "please try again"
/// TempData message and zero logging — operations had no way to debug
/// recurring save failures.
///
/// These tests prove:
/// 1. When SaveChangesAsync throws a generic Exception, the page logs
///    via <see cref="ILogger"/> with structured fields (PO id, company,
///    receipt number) and surfaces a useful TempData["Error"] including
///    a correlation reference.
/// 2. When SaveChangesAsync throws a <see cref="DbUpdateException"/>,
///    the page logs at Error level and surfaces the inner exception
///    message to the user.
/// 3. When SaveChangesAsync throws a <see cref="DbUpdateConcurrencyException"/>,
///    the page logs at Warning level (not Error — it's expected) and
///    tells the user to reload.
/// </summary>
public class ReceivingExceptionLoggingTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public Func<int>? SaveChangesOverride { get; set; }
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => SaveChangesOverride != null
                ? Task.FromResult(SaveChangesOverride())
                : base.SaveChangesAsync(cancellationToken);
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            => SaveChangesOverride != null
                ? Task.FromResult(SaveChangesOverride())
                : base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static TestAppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"recv-log-{dbName}-{Guid.NewGuid()}")
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

    /// <summary>Captures every log call with its level, exception, and message.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel level, Exception? ex, string message)> Records { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Records.Add((logLevel, exception, formatter(state, exception)));
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
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

    /// <summary>Seeds the minimum fixture: company, vendor, PO with one
    /// receivable line. Returns the PO so the test can post against it.</summary>
    private static async Task<PurchaseOrder> SeedReceivablePoAsync(TestAppDbContext db, int companyId)
    {
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-LOG-1",
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
        return po;
    }

    [Fact]
    public async Task SaveFailure_GenericException_LogsErrorAndSurfacesCorrelationInTempData()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var po = await SeedReceivablePoAsync(db, companyId);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var logger = new CapturingLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>();

        // After all the seeding is done, swap SaveChangesAsync to always throw.
        db.SaveChangesOverride = () => throw new InvalidOperationException("boom — simulated DB outage");

        var cipCostService = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCost = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(
            db, new AlwaysEnabledModuleGuard(), tenant, lookup, new AllowAllPeriodGuard(), logger, cipAutoCost);
        WirePageContext(page);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        var result = await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        // Page redirects, doesn't throw — bubbling would 500 the user.
        Assert.IsType<RedirectToPageResult>(result);

        // CRITICAL: the exception was logged at Error level with structured fields.
        var errorRecord = Assert.Single(logger.Records, r => r.level == LogLevel.Error);
        Assert.NotNull(errorRecord.ex);
        Assert.Contains("PO", errorRecord.message); // PONumber/POId mentioned
        Assert.Contains("100", errorRecord.message); // CompanyId rendered

        // CRITICAL: the user-facing TempData carries enough info to debug.
        Assert.True(page.TempData.TryGetValue("Error", out var msg));
        var msgStr = msg as string ?? "";
        Assert.Contains("Reference", msgStr); // correlation hint
        Assert.DoesNotContain("Please try again", msgStr); // not the old generic message
    }

    [Fact]
    public async Task SaveFailure_DbUpdateException_LogsErrorWithInnerMessage()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var po = await SeedReceivablePoAsync(db, companyId);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var logger = new CapturingLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>();
        var inner = new Exception("FK constraint violated on Vendor");
        db.SaveChangesOverride = () => throw new DbUpdateException("update failed", inner);

        var cipCostService = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCost = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(
            db, new AlwaysEnabledModuleGuard(), tenant, lookup, new AllowAllPeriodGuard(), logger, cipAutoCost);
        WirePageContext(page);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        var record = Assert.Single(logger.Records, r => r.level == LogLevel.Error);
        Assert.IsType<DbUpdateException>(record.ex);

        var msgStr = (page.TempData["Error"] as string) ?? "";
        Assert.Contains("FK constraint violated", msgStr); // inner message surfaced
    }

    [Fact]
    public async Task SaveFailure_DbUpdateConcurrencyException_LogsWarningAndAdvisesReload()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var po = await SeedReceivablePoAsync(db, companyId);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var logger = new CapturingLogger<Abs.FixedAssets.Pages.Receiving.ReceiveModel>();
        db.SaveChangesOverride = () => throw new DbUpdateConcurrencyException("optimistic lock");

        var cipCostService = new Abs.FixedAssets.Services.Cip.CipCostService(db, lookup, tenant);
        var cipAutoCost = new Abs.FixedAssets.Services.Cip.CipAutoCostPostingService(db, lookup, tenant, cipCostService);
        var page = new Abs.FixedAssets.Pages.Receiving.ReceiveModel(
            db, new AlwaysEnabledModuleGuard(), tenant, lookup, new AllowAllPeriodGuard(), logger, cipAutoCost);
        WirePageContext(page);

        var lines = new List<Abs.FixedAssets.Pages.Receiving.ReceiveModel.ReceiveLineViewModel>
        {
            new() { POLineId = po.Lines.First().Id, QuantityToReceive = 5 }
        };
        await page.OnPostReceiveAsync(po.Id, lines, DateTime.Today, null, null, null, null, null);

        // Concurrency conflict is expected, not exceptional — Warning, not Error.
        var record = Assert.Single(logger.Records, r => r.level == LogLevel.Warning);
        Assert.IsType<DbUpdateConcurrencyException>(record.ex);

        var msgStr = (page.TempData["Error"] as string) ?? "";
        Assert.Contains("Reload", msgStr, StringComparison.OrdinalIgnoreCase);
    }
}
