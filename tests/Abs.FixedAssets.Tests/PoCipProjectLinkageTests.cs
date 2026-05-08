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
/// Regression tests for S2-11: PO Create + Update Header now accept a
/// CipProjectId. Tenant-scoped against the CIP project's company.
///
/// Per audit S2-11: "PO has CipProjectId field but no UI sets it. The
/// CIP-PO linkage is unreachable from the UI."
/// </summary>
public class PoCipProjectLinkageTests
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
            .UseInMemoryDatabase($"po-cip-{dbName}-{Guid.NewGuid()}")
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

    [Fact]
    public async Task Create_PoWithValidCipProjectId_LinksToProject()
    {
        const int companyId = 100;
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = companyId, IsActive = true });
        var project = new CipProject
        {
            ProjectNumber = "CIP-100", Name = "Proj",
            CompanyId = companyId, Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var page = new Abs.FixedAssets.Pages.Purchasing.CreateModel(db, new AlwaysEnabledModuleGuard(), lookup, tenant);
        WirePageContext(page);

        await page.OnPostAsync(
            vendorId: 1,
            poTypeLookupValueId: 0,
            orderDate: DateTime.UtcNow,
            requiredDate: null,
            notes: null,
            cipProjectId: project.Id);

        var po = await db.PurchaseOrders.AsNoTracking().SingleAsync();
        Assert.Equal(project.Id, po.CipProjectId);
    }

    [Fact]
    public async Task Create_PoWithForeignTenantCipProjectId_DoesNotLink()
    {
        // Project in company 200; tenant only sees 100. The foreign id
        // is rejected — the PO is created without a CIP linkage.
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "A", IsActive = true });
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = 100, IsActive = true });
        var foreignProject = new CipProject
        {
            ProjectNumber = "CIP-FOREIGN", Name = "Other",
            CompanyId = 200, Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(foreignProject);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var page = new Abs.FixedAssets.Pages.Purchasing.CreateModel(db, new AlwaysEnabledModuleGuard(), lookup, tenant);
        WirePageContext(page);

        await page.OnPostAsync(
            vendorId: 1,
            poTypeLookupValueId: 0,
            orderDate: DateTime.UtcNow,
            requiredDate: null,
            notes: null,
            cipProjectId: foreignProject.Id);

        var po = await db.PurchaseOrders.AsNoTracking().SingleAsync();
        Assert.Null(po.CipProjectId); // CRITICAL: foreign id rejected
    }
}
