using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.AccountsPayable;
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
/// Regression tests for the cross-tenant data leak in
/// <c>Pages/AccountsPayable/Details.cshtml.cs</c> that the 2026-05-07
/// code review surfaced.
///
/// Pre-fix bug: <c>OnGetAsync</c> and <c>LoadInvoiceScopedAsync</c> applied
/// the company filter conditionally — only when <c>_tenantContext.CompanyId.HasValue</c>.
/// A user whose tenant resolved with <c>CompanyId = null</c> (e.g., a
/// site-only user, or a user during partial context resolution) skipped
/// the company filter entirely and could load any VendorInvoice across
/// the multi-tenant DB by ID.
///
/// Post-fix: company scope is mandatory. <c>VisibleCompanyIds</c> is the
/// source of truth — empty list returns no results, which is the correct
/// behavior for a user with no company access.
/// </summary>
public class AccountsPayableTenantScopeTests
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
            .UseInMemoryDatabase($"ap-tenant-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    /// <summary>Stub ITenantContext that returns whatever the test sets.</summary>
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

    private static (DetailsModel page, AppDbContext db) NewPage(ITenantContext tenant)
    {
        var db = NewDb();
        var lookupService = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var matchingService = new InvoiceMatchingService(db);
        var moduleGuard = new AlwaysEnabledModuleGuard();
        var page = new DetailsModel(db, moduleGuard, tenant, lookupService, matchingService);

        // Minimal PageContext so OnGetAsync can write to ViewData without NRE.
        // Same pattern as AssetConcurrencyTests.
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        page.PageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };
        return (page, db);
    }

    /// <summary>
    /// Seeds the minimum nav-prop fixture so the page's Include() chain
    /// has Vendor + Company to LEFT-JOIN against. Returns the invoice.
    /// </summary>
    private static async Task<VendorInvoice> SeedInvoiceWithFixturesAsync(AppDbContext db, int companyId)
    {
        // Company + Vendor must exist so Include(i => i.Company) and
        // Include(i => i.Vendor) materialize correctly. The InMemory
        // provider tolerates dangling FKs but explicit fixtures keep
        // the test deterministic.
        if (!await db.Companies.AnyAsync(c => c.Id == companyId))
        {
            db.Companies.Add(new Company
            {
                Id = companyId,
                CompanyCode = $"CO-{companyId}",
                Name = $"Test Company {companyId}",
                IsActive = true
            });
        }
        if (!await db.Vendors.AnyAsync(v => v.Id == 1))
        {
            db.Vendors.Add(new Vendor
            {
                Id = 1,
                Code = "V-1",
                Name = "Test Vendor",
                CompanyId = companyId,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();

        var invoice = new VendorInvoice
        {
            CompanyId = companyId,
            VendorId = 1,
            InvoiceNumber = $"INV-{companyId}-{Guid.NewGuid().ToString("N")[..6]}",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Currency = "USD",
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        db.VendorInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task OnGetAsync_TenantHasNullCompanyAndOtherCompanyVisible_DoesNotLoadCrossTenantInvoice()
    {
        // The exact bug condition: CompanyId is null on the tenant context,
        // and VisibleCompanyIds restricts to a different company than the
        // invoice we're trying to load. Before the fix, the conditional
        // `if (companyId.HasValue)` skipped the filter and loaded the invoice
        // anyway. Post-fix, the filter is mandatory and we return null.
        const int otherCompanyId   = 200;
        const int invoiceCompanyId = 100;

        var tenant = new StubTenantContext
        {
            TenantId = 1,
            CompanyId = null,                                 // bug trigger
            VisibleCompanyIds = new List<int> { otherCompanyId } // CANNOT see company 100
        };

        var (page, db) = NewPage(tenant);
        var invoice = await SeedInvoiceWithFixturesAsync(db, invoiceCompanyId);

        var result = await page.OnGetAsync(invoice.Id);

        Assert.Null(page.Invoice); // CRITICAL: must NOT have loaded the cross-tenant invoice
        Assert.IsType<PageResult>(result); // page renders empty, not throws
    }

    [Fact]
    public async Task OnGetAsync_TenantHasEmptyVisibleCompanies_ReturnsEmptyEvenWhenInvoiceExists()
    {
        // Edge case: user with no company access at all. Empty VisibleCompanyIds
        // should return no invoices regardless of the requested ID.
        var tenant = new StubTenantContext
        {
            TenantId = 1,
            CompanyId = null,
            VisibleCompanyIds = new List<int>() // empty access
        };

        var (page, db) = NewPage(tenant);
        var invoice = await SeedInvoiceWithFixturesAsync(db, companyId: 100);

        await page.OnGetAsync(invoice.Id);

        Assert.Null(page.Invoice);
    }

    [Fact]
    public async Task OnGetAsync_TenantHasMatchingCompanyVisible_LoadsInvoice()
    {
        // Happy path: tenant has access to the invoice's company, invoice loads.
        const int companyId = 100;
        var tenant = new StubTenantContext
        {
            TenantId = 1,
            CompanyId = companyId,
            VisibleCompanyIds = new List<int> { companyId }
        };

        var (page, db) = NewPage(tenant);
        var invoice = await SeedInvoiceWithFixturesAsync(db, companyId);

        await page.OnGetAsync(invoice.Id);

        Assert.NotNull(page.Invoice);
        Assert.Equal(invoice.Id, page.Invoice!.Id);
    }

    [Fact]
    public async Task OnGetAsync_NullCompanyIdButCorrectCompanyVisible_LoadsInvoice()
    {
        // Important: a user without an explicit CompanyId but WITH the
        // invoice's company in VisibleCompanyIds should still see it. This
        // proves the fix isn't over-restrictive — it only blocks genuine
        // cross-tenant access, not legitimate "I'm a multi-company user" reads.
        const int companyId = 100;
        var tenant = new StubTenantContext
        {
            TenantId = 1,
            CompanyId = null, // no explicit company set
            VisibleCompanyIds = new List<int> { companyId } // but has visibility
        };

        var (page, db) = NewPage(tenant);
        var invoice = await SeedInvoiceWithFixturesAsync(db, companyId);

        await page.OnGetAsync(invoice.Id);

        Assert.NotNull(page.Invoice);
        Assert.Equal(invoice.Id, page.Invoice!.Id);
    }
}
