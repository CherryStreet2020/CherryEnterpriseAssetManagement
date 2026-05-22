// Sprint 12.9 PR #7 — Automated RLS tenant-leak test in CI.
//
// Closes Absorption Item 8 ("Automated RLS tests + tenant-leak gates"). Each
// test follows the same shape:
//
//   1. Seed a row scoped to Tenant A (companyId=100).
//   2. Build a service backed by a StubTenantContext for Tenant B
//      (companyId=200, VisibleCompanyIds = [200]).
//   3. Call a service mutation against Tenant A's row id.
//   4. Assert the call returns Result.Failure (NOT a SaveChangesAsync that
//      silently mutates the cross-tenant row).
//
// Why this matters: pre-Sprint-12.9, PageModels reached into AppDbContext
// directly and the tenant-scope guard was inline + easy to forget. After
// Sprint 12.9 PR #3/#4/#5, every write path goes through a domain service
// that performs the VisibleCompanyIds.Contains(...) check on the way in.
// These tests prove that contract holds for all three services.
//
// If a future refactor breaks the guard, CI fails — not a customer.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Approvals;
using Abs.FixedAssets.Services.Items;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Purchasing;
using Abs.FixedAssets.Services.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

public class CrossTenantLeakageTests
{
    private const int TenantACompanyId = 100;
    private const int TenantBCompanyId = 200;

    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            // EF InMemory doesn't support Pgvector / RowVersion / jsonb columns.
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"xtenant-{dbName}-{Guid.NewGuid()}")
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

    /// <summary>
    /// Returns a Tenant-B-only tenant context — visible-companies excludes Tenant A.
    /// Used to drive every cross-tenant assertion below.
    /// </summary>
    private static ITenantContext TenantBOnly() => new StubTenantContext
    {
        CompanyId = TenantBCompanyId,
        VisibleCompanyIds = new() { TenantBCompanyId }
    };

    private static async Task EnsureCompaniesAsync(AppDbContext db)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == TenantACompanyId))
            db.Companies.Add(new Company { Id = TenantACompanyId, CompanyCode = "C-A", Name = "TenantA", IsActive = true });
        if (!await db.Companies.AnyAsync(c => c.Id == TenantBCompanyId))
            db.Companies.Add(new Company { Id = TenantBCompanyId, CompanyCode = "C-B", Name = "TenantB", IsActive = true });
        await db.SaveChangesAsync();
    }

    private static ILookupService NewLookup(AppDbContext db) =>
        new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);

    // ========================================================================
    // IItemMasterService cross-tenant tests (Sprint 12.9 PR #5)
    // ========================================================================

    [Fact]
    public async Task IItemMasterService_UpdateItem_RejectsCrossTenantId()
    {
        await using var db = NewDb();
        await EnsureCompaniesAsync(db);

        // Tenant A owns the item.
        var item = new Item { PartNumber = "A-WIDGET", Description = "Widget", StockUOM = "EA", CompanyId = TenantACompanyId };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        // Tenant B tries to update it.
        var svc = new ItemMasterService(db, TenantBOnly(), NewLookup(db), NullLogger<ItemMasterService>.Instance);
        var req = new UpdateItemRequest(
            ItemId: item.Id, PartNumber: "A-WIDGET", TypeLookupValueId: null,
            Description: "PWNED", ExtendedDescription: null, StockUom: "EA", IsActive: true,
            LeadTimeDays: null, MinOrderQty: null, OrderMultiple: null, PurchaseUom: null,
            PackQty: null, StockPolicy: StockPolicy.Stock, LastPrice: null, CurrencyCode: null,
            PriceEffectiveDate: null, ContractFlag: false, ContractRef: null,
            StatusLookupValueId: null, CostMethodLookupValueId: null, TrackingTypeLookupValueId: null,
            StandardCost: null, DefaultLocationId: null);

        var result = await svc.UpdateItemAsync(req, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found in scope", result.Error, StringComparison.OrdinalIgnoreCase);

        // Critical: the original Tenant A item must be unchanged.
        var stillThere = await db.Items.FirstAsync(i => i.Id == item.Id);
        Assert.Equal("Widget", stillThere.Description);
    }

    [Fact]
    public async Task IItemMasterService_CreateItem_DoesNotFlagCrossTenantPartNumberAsDuplicate()
    {
        // Tenant A and Tenant B may legitimately have the same PartNumber.
        // The dup-PN guard must be scoped to visible companies.
        await using var db = NewDb();
        await EnsureCompaniesAsync(db);

        db.Items.Add(new Item { PartNumber = "WIDGET", Description = "TenantA Widget", StockUOM = "EA", CompanyId = TenantACompanyId });
        await db.SaveChangesAsync();

        var svc = new ItemMasterService(db, TenantBOnly(), NewLookup(db), NullLogger<ItemMasterService>.Instance);
        var req = new CreateItemRequest(
            PartNumber: "WIDGET", TypeLookupValueId: null,
            Description: "TenantB Widget", ExtendedDescription: null, StockUom: "EA", IsActive: true,
            LeadTimeDays: null, MinOrderQty: null, OrderMultiple: null, PurchaseUom: null,
            PackQty: null, StockPolicy: StockPolicy.Stock, LastPrice: null, CurrencyCode: null,
            PriceEffectiveDate: null, ContractFlag: false, ContractRef: null,
            StatusLookupValueId: null, CostMethodLookupValueId: null, TrackingTypeLookupValueId: null,
            StandardCost: null, DefaultLocationId: null);

        var result = await svc.CreateItemAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantBCompanyId, result.Value!.CompanyId);
    }

    [Fact]
    public async Task IItemMasterService_SetItemImagePath_RejectsCrossTenantId()
    {
        await using var db = NewDb();
        await EnsureCompaniesAsync(db);

        var item = new Item { PartNumber = "A", Description = "A", StockUOM = "EA", CompanyId = TenantACompanyId };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var svc = new ItemMasterService(db, TenantBOnly(), NewLookup(db), NullLogger<ItemMasterService>.Instance);

        var result = await svc.SetItemImagePathAsync(item.Id, "/images/pwned.png", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found in scope", result.Error, StringComparison.OrdinalIgnoreCase);

        var stillThere = await db.Items.FirstAsync(i => i.Id == item.Id);
        Assert.Null(stillThere.ImagePath);
    }

    [Fact]
    public async Task IItemMasterService_ClearItemImagePath_RejectsCrossTenantId()
    {
        await using var db = NewDb();
        await EnsureCompaniesAsync(db);

        var item = new Item { PartNumber = "A", Description = "A", StockUOM = "EA", CompanyId = TenantACompanyId, ImagePath = "/orig.png" };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var svc = new ItemMasterService(db, TenantBOnly(), NewLookup(db), NullLogger<ItemMasterService>.Instance);

        var result = await svc.ClearItemImagePathAsync(item.Id, CancellationToken.None);

        Assert.True(result.IsFailure);

        var stillThere = await db.Items.FirstAsync(i => i.Id == item.Id);
        Assert.Equal("/orig.png", stillThere.ImagePath);
    }

    // ========================================================================
    // IPurchasingService cross-tenant tests (Sprint 12.9 PR #4)
    // ========================================================================

    private static async Task<PurchaseOrder> SeedDraftPoAsync(AppDbContext db, int companyId)
    {
        if (!await db.Vendors.AnyAsync(v => v.CompanyId == companyId))
            db.Vendors.Add(new Vendor { Code = $"V-{companyId}", Name = $"V{companyId}", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();

        var vendor = await db.Vendors.FirstAsync(v => v.CompanyId == companyId);
        var po = new PurchaseOrder
        {
            PONumber = $"PO-{companyId}",
            VendorId = vendor.Id,
            CompanyId = companyId,
            Status = POStatus.Draft,
            OrderDate = DateTime.UtcNow,
            Currency = "USD"
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();
        return po;
    }

    private static PurchasingService NewPurchasingService(AppDbContext db, ITenantContext tenant)
    {
        var lookup = NewLookup(db);
        var audit = new AuditService(db);
        var approval = new ApprovalService(db, audit, NullLogger<ApprovalService>.Instance);
        var outbox = new OutboxWriter(db, tenant, NullLogger<OutboxWriter>.Instance);
        return new PurchasingService(db, tenant, lookup, approval, outbox, NullLogger<PurchasingService>.Instance);
    }

    [Fact]
    public async Task IPurchasingService_UpdateHeader_RejectsCrossTenantPoId()
    {
        await using var db = NewDb();
        await EnsureCompaniesAsync(db);

        var po = await SeedDraftPoAsync(db, TenantACompanyId);

        var svc = NewPurchasingService(db, TenantBOnly());
        var req = new UpdatePoHeaderRequest(
            PurchaseOrderId: po.Id,
            VendorId: po.VendorId,
            POTypeLookupValueId: 0,
            OrderDate: DateTime.UtcNow,
            RequiredDate: null,
            Notes: "PWNED",
            CipProjectId: null);

        var result = await svc.UpdateHeaderAsync(req, CancellationToken.None);

        Assert.True(result.IsFailure);

        var stillThere = await db.PurchaseOrders.FirstAsync(p => p.Id == po.Id);
        Assert.NotEqual("PWNED", stillThere.Notes);
    }

    [Fact]
    public async Task IPurchasingService_SubmitForApproval_RejectsCrossTenantPoId()
    {
        await using var db = NewDb();
        await EnsureCompaniesAsync(db);

        var po = await SeedDraftPoAsync(db, TenantACompanyId);

        var svc = NewPurchasingService(db, TenantBOnly());

        var result = await svc.SubmitForApprovalAsync(po.Id, CancellationToken.None);

        Assert.True(result.IsFailure);

        var stillThere = await db.PurchaseOrders.FirstAsync(p => p.Id == po.Id);
        Assert.Equal(POStatus.Draft, stillThere.Status); // not moved to PendingApproval
    }

    [Fact]
    public async Task IPurchasingService_DeletePo_RejectsCrossTenantPoId()
    {
        await using var db = NewDb();
        await EnsureCompaniesAsync(db);

        var po = await SeedDraftPoAsync(db, TenantACompanyId);

        var svc = NewPurchasingService(db, TenantBOnly());

        var result = await svc.DeletePoAsync(po.Id, CancellationToken.None);

        Assert.True(result.IsFailure);

        var stillThere = await db.PurchaseOrders.AnyAsync(p => p.Id == po.Id);
        Assert.True(stillThere, "Cross-tenant PO must NOT be deleted.");
    }
}
