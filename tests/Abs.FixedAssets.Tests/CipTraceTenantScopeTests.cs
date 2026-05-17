using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Cip;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for S2-3: CipTraceQueryService now project-scopes
/// every trace lookup. A user from a tenant who cannot see the CIP
/// project gets an empty list — they don't leak related WO/PO/Invoice/
/// JournalEntry/Receipt rows that happen to share the project id.
///
/// Per audit S2-3: "GetRelatedWorkOrdersAsync, GetRelatedPurchaseOrdersAsync,
/// GetRelatedVendorInvoicesAsync, GetRelatedJournalsAsync all return rows by
/// ID set without checking VisibleCompanyIds."
/// </summary>
public class CipTraceTenantScopeTests
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
            .UseInMemoryDatabase($"cip-trace-{dbName}-{Guid.NewGuid()}")
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

    [Fact]
    public async Task GetRelatedWorkOrdersAsync_ProjectInOtherTenant_ReturnsEmpty()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "A", IsActive = true });
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        var foreignProject = new CipProject
        {
            ProjectNumber = "CIP-FOREIGN", Name = "Other tenant project",
            CompanyId = 200, Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(foreignProject);
        var asset = new Asset
        {
            AssetNumber = "A-1", Description = "x", CompanyId = 200,
            AcquisitionCost = 1000m, UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine, CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        // Add a WO tied to the foreign project — under the buggy pre-fix
        // service, this would have leaked back to a Tenant A user.
        db.WorkOrders.Add(new WorkOrder
        {
            WorkOrderNumber = "WO-LEAK", AssetId = asset.Id,
            CipProjectId = foreignProject.Id,
            Status = MaintenanceStatus.InProgress, ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Tenant A only sees company 100; project lives in company 200.
        var tenantA = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var svc = new CipTraceQueryService(db, tenantA);

        var result = await svc.GetRelatedWorkOrdersAsync(foreignProject.Id);
        Assert.Empty(result); // CRITICAL: no leak
    }

    [Fact]
    public async Task GetRelatedWorkOrdersAsync_ProjectInVisibleTenant_ReturnsRelated()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 100, CompanyCode = "C-100", Name = "A", IsActive = true });
        var project = new CipProject
        {
            ProjectNumber = "CIP-OK", Name = "In-scope",
            CompanyId = 100, Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        var asset = new Asset
        {
            AssetNumber = "A-1", Description = "x", CompanyId = 100,
            AcquisitionCost = 1000m, UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine, CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        db.WorkOrders.Add(new WorkOrder
        {
            WorkOrderNumber = "WO-OK", AssetId = asset.Id,
            CipProjectId = project.Id,
            Status = MaintenanceStatus.InProgress, ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var svc = new CipTraceQueryService(db, tenant);

        var result = await svc.GetRelatedWorkOrdersAsync(project.Id);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetRelatedPurchaseOrdersAsync_ProjectInOtherTenant_ReturnsEmpty()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        db.Vendors.Add(new Vendor { Id = 1, Code = "V-1", Name = "V", CompanyId = 200, IsActive = true });
        var project = new CipProject
        {
            ProjectNumber = "CIP-X", Name = "Foreign", CompanyId = 200,
            Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-LEAK", VendorId = 1, CompanyId = 200,
            Status = POStatus.Approved, OrderDate = DateTime.UtcNow,
            Currency = "USD", CipProjectId = project.Id
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var tenantA = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var svc = new CipTraceQueryService(db, tenantA);

        Assert.Empty(await svc.GetRelatedPurchaseOrdersAsync(project.Id));
    }

    [Fact]
    public async Task GetRelatedJournalsAsync_ProjectInOtherTenant_ReturnsEmpty()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        var project = new CipProject
        {
            ProjectNumber = "CIP-Y", Name = "Foreign", CompanyId = 200,
            Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var tenantA = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var svc = new CipTraceQueryService(db, tenantA);
        Assert.Empty(await svc.GetRelatedJournalsAsync(project.Id));
    }

    [Fact]
    public async Task GetRelatedReceiptsAsync_ProjectInOtherTenant_ReturnsEmpty()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        var project = new CipProject
        {
            ProjectNumber = "CIP-Z", Name = "Foreign", CompanyId = 200,
            Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var tenantA = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var svc = new CipTraceQueryService(db, tenantA);
        Assert.Empty(await svc.GetRelatedReceiptsAsync(project.Id));
    }

    [Fact]
    public async Task GetRelatedVendorInvoicesAsync_ProjectInOtherTenant_ReturnsEmpty()
    {
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = 200, CompanyCode = "C-200", Name = "B", IsActive = true });
        var project = new CipProject
        {
            ProjectNumber = "CIP-W", Name = "Foreign", CompanyId = 200,
            Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();

        var tenantA = new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } };
        var svc = new CipTraceQueryService(db, tenantA);
        Assert.Empty(await svc.GetRelatedVendorInvoicesAsync(project.Id));
    }
}
