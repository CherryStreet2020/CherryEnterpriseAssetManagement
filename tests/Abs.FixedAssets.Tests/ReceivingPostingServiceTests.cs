using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for S1-1: ReceivingPostingService implements the
/// GR/IR (Goods Receipt / Invoice Receipt) accounting pattern.
///
/// Per audit S1-1 + ADR-001:
/// - Stock receipts: Dr Inventory / Cr GR-Accrued + ItemInventory + ItemTransaction
/// - Non-stock receipts: Dr DirectExpense / Cr GR-Accrued (no inventory)
/// - CIP-tagged receipts: skipped here (handled by CipAutoCostPostingService)
/// - Idempotent: replay returns the same JE id, no double-post
/// </summary>
public class ReceivingPostingServiceTests
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
            .UseInMemoryDatabase($"recv-post-{dbName}-{Guid.NewGuid()}")
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

    private static ReceivingPostingService NewService(AppDbContext db, ITenantContext tenant)
    {
        var resolver = new GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(
            db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
        return new ReceivingPostingService(db, tenant, resolver, outbox,
            new PassthroughIdempotencyMediator(),
            new Abs.FixedAssets.Tests.TestHelpers.NullChainOfCustodyService(),
            NullLogger<ReceivingPostingService>.Instance);
    }

    private static async Task<(GoodsReceipt receipt, Item item, Location location)>
        SeedStockReceiptAsync(AppDbContext db, int companyId, decimal qty, decimal unitPrice)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == companyId))
            db.Companies.Add(new Company { Id = companyId, CompanyCode = $"C-{companyId}", Name = "Co", IsActive = true });
        if (!await db.Vendors.AnyAsync(v => v.CompanyId == companyId))
            db.Vendors.Add(new Vendor { Code = $"V-{companyId}", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();
        var vendor = await db.Vendors.FirstAsync(v => v.CompanyId == companyId);

        var item = new Item
        {
            PartNumber = "STOCK-1",
            Description = "Stock part",
            Type = ItemType.Part,
            CompanyId = companyId,
            StandardCost = unitPrice
        };
        db.Items.Add(item);
        var location = new Location { Name = "STOCKROOM", IsActive = true };
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            PONumber = "PO-RECV-1", VendorId = vendor.Id, CompanyId = companyId,
            Status = POStatus.Approved, OrderDate = DateTime.UtcNow, Currency = "USD"
        };
        po.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1, Description = "Stock part", UOM = "EA",
            QuantityOrdered = qty, UnitPrice = unitPrice, LineTotal = qty * unitPrice,
            ItemId = item.Id
        });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var receipt = new GoodsReceipt
        {
            ReceiptNumber = "GR-001", PurchaseOrderId = po.Id,
            Status = ReceiptStatus.Received, ReceiptDate = DateTime.UtcNow,
            ReceivedBy = "test", CompanyId = companyId, CreatedAt = DateTime.UtcNow
        };
        receipt.Lines.Add(new GoodsReceiptLine
        {
            PurchaseOrderLineId = po.Lines.First().Id, LineNumber = 1,
            QuantityReceived = qty, QuantityAccepted = qty,
            ReceivingLocationId = location.Id
        });
        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync();

        return (receipt, item, location);
    }

    [Fact]
    public async Task PostReceipt_StockItem_IncrementsInventoryAndCreatesItemTransaction()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, item, location) = await SeedStockReceiptAsync(db, companyId, qty: 10m, unitPrice: 5m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var result = await NewService(db, tenant).PostReceiptAsync(receipt.Id);

        Assert.NotNull(result.JournalEntryId);
        Assert.Equal(50m, result.TotalAccrued); // 10 × $5
        Assert.Equal(1, result.InventoryRowsTouched);

        var inv = await db.Set<ItemInventory>().AsNoTracking().FirstAsync();
        Assert.Equal(10m, inv.QuantityOnHand);
        Assert.Equal(item.Id, inv.ItemId);
        Assert.Equal(location.Id, inv.LocationId);

        var txn = await db.Set<ItemTransaction>().AsNoTracking().SingleAsync();
        Assert.Equal(TransactionType.Receipt, txn.Type);
        Assert.Equal(10m, txn.Quantity);
        Assert.Equal(5m, txn.UnitCost);
        Assert.Equal(location.Id, txn.ToLocationId);
    }

    [Fact]
    public async Task PostReceipt_StockItem_PostsDrInventoryCrGrAccrued()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, _, _) = await SeedStockReceiptAsync(db, companyId, qty: 10m, unitPrice: 5m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        await NewService(db, tenant).PostReceiptAsync(receipt.Id);

        var je = await db.JournalEntries.Include(j => j.Lines).SingleAsync();
        // Industry default: Inventory=1300, GrAccrued=2150.
        var dr = je.Lines.Single(l => l.Debit > 0);
        var cr = je.Lines.Single(l => l.Credit > 0);
        Assert.Equal("1300", dr.Account); // Inventory
        Assert.Equal(50m, dr.Debit);
        Assert.Equal("2150", cr.Account); // GR-Accrued
        Assert.Equal(50m, cr.Credit);
    }

    [Fact]
    public async Task PostReceipt_NonStockServiceItem_PostsDrDirectExpenseNoInventoryWrite()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, item, _) = await SeedStockReceiptAsync(db, companyId, qty: 4m, unitPrice: 75m);

        // Flip the item to Service so it routes to DirectExpense.
        item.Type = ItemType.Service;
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var result = await NewService(db, tenant).PostReceiptAsync(receipt.Id);

        Assert.Equal(0, result.InventoryRowsTouched); // CRITICAL: no inventory for service items
        Assert.Equal(300m, result.TotalAccrued);

        Assert.Empty(await db.Set<ItemInventory>().ToListAsync());
        Assert.Empty(await db.Set<ItemTransaction>().ToListAsync());

        var je = await db.JournalEntries.Include(j => j.Lines).SingleAsync();
        var dr = je.Lines.Single(l => l.Debit > 0);
        Assert.Equal("6000", dr.Account); // DirectExpense industry default
    }

    [Fact]
    public async Task PostReceipt_CipTaggedLine_SkippedFromGrIrAccrual()
    {
        // CIP-tagged lines flow through CipAutoCostPostingService (PR #37);
        // ReceivingPostingService skips them so the cost isn't double-posted
        // (once to GR-Accrued and once to CIP). Asserts no JE is written
        // when every line is CIP-tagged.
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, _, _) = await SeedStockReceiptAsync(db, companyId, qty: 5m, unitPrice: 20m);

        // Tag the PO line with a CIP project.
        var project = new CipProject
        {
            ProjectNumber = "CIP-X", Name = "P", CompanyId = companyId,
            Status = CipProjectStatus.Active, CreatedAt = DateTime.UtcNow
        };
        db.CipProjects.Add(project);
        await db.SaveChangesAsync();
        var poLine = await db.Set<PurchaseOrderLine>().FirstAsync(l => l.PurchaseOrderId == receipt.PurchaseOrderId);
        poLine.CipProjectId = project.Id;
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var result = await NewService(db, tenant).PostReceiptAsync(receipt.Id);

        Assert.Null(result.JournalEntryId); // no JE
        Assert.Equal(0m, result.TotalAccrued);
        Assert.Empty(await db.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task PostReceipt_DuplicateReplay_ReturnsSameJEAndDoesNotDoublePost()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, _, _) = await SeedStockReceiptAsync(db, companyId, qty: 10m, unitPrice: 5m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var svc = NewService(db, tenant);

        var first = await svc.PostReceiptAsync(receipt.Id);
        var second = await svc.PostReceiptAsync(receipt.Id);
        var third = await svc.PostReceiptAsync(receipt.Id);

        Assert.Equal(first.JournalEntryId, second.JournalEntryId);
        Assert.Equal(first.JournalEntryId, third.JournalEntryId);
        Assert.Equal(1, await db.JournalEntries.CountAsync());

        // Inventory should also not double-up.
        var inv = await db.Set<ItemInventory>().AsNoTracking().SingleAsync();
        Assert.Equal(10m, inv.QuantityOnHand);
        Assert.Equal(1, await db.Set<ItemTransaction>().CountAsync());
    }

    [Fact]
    public async Task PostReceipt_GrInOtherTenant_NoOpsSafely()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, _, _) = await SeedStockReceiptAsync(db, companyId, qty: 10m, unitPrice: 5m);

        var tenantB = new StubTenantContext { CompanyId = 200, VisibleCompanyIds = new() { 200 } };
        var result = await NewService(db, tenantB).PostReceiptAsync(receipt.Id);

        Assert.Null(result.JournalEntryId);
        Assert.Equal(0m, result.TotalAccrued);
        Assert.Empty(await db.JournalEntries.ToListAsync());
        Assert.Empty(await db.Set<ItemInventory>().ToListAsync());
    }

    [Fact]
    public async Task PostReceipt_StockItem_EmitsItemReceivedV1OutboxEvent()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, item, location) = await SeedStockReceiptAsync(db, companyId, qty: 7m, unitPrice: 12m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        await NewService(db, tenant).PostReceiptAsync(receipt.Id);

        var evt = await db.OutboxEvents.SingleAsync(e => e.EventType == "item.received");
        Assert.Equal("ItemInventory", evt.EntityType);
        Assert.Equal(companyId, evt.CompanyId);

        using var doc = System.Text.Json.JsonDocument.Parse(evt.PayloadJson);
        var root = doc.RootElement;
        Assert.Equal(item.Id, root.GetProperty("itemId").GetInt32());
        Assert.Equal(location.Id, root.GetProperty("locationId").GetInt32());
        Assert.Equal(7m, root.GetProperty("quantity").GetDecimal());
        Assert.Equal(12m, root.GetProperty("unitCost").GetDecimal());
        Assert.Equal(7m, root.GetProperty("newQuantityOnHand").GetDecimal());
    }

    [Fact]
    public async Task PostReceipt_DuplicateReplay_DoesNotEmitItemReceivedTwice()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, _, _) = await SeedStockReceiptAsync(db, companyId, qty: 5m, unitPrice: 4m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var svc = NewService(db, tenant);
        await svc.PostReceiptAsync(receipt.Id);
        await svc.PostReceiptAsync(receipt.Id); // idempotent re-run

        Assert.Equal(1, await db.OutboxEvents.CountAsync(e => e.EventType == "item.received"));
    }

    // =========================================================================
    // Sprint 13.5 PRA-5d — DEF-008 dual-write: JournalLine.AccountingKeyId
    // stamped alongside the legacy Account string. Mirrors PRA-5c pattern
    // from ApPostingServiceTests.
    // =========================================================================

    private static async Task SeedSystemGlAccountAsync(AppDbContext db, string accountNumber)
    {
        if (!await db.Set<GlAccount>().AnyAsync(a => a.AccountNumber == accountNumber && a.CompanyId == null))
        {
            db.Set<GlAccount>().Add(new GlAccount
            {
                AccountNumber = accountNumber,
                Name = $"Test {accountNumber}",
                CompanyId = null,
                AccountType = GlAccountType.Asset,
                Category = GlAccountCategory.CashAndReceivables,
                NormalBalance = NormalBalance.Debit,
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PostReceipt_WithGlAccountsSeeded_StampsAccountingKeyIdOnAllLines()
    {
        const int companyId = 100;
        await using var db = NewDb();
        // Industry defaults: Inventory=1300, GrAccrued=2150.
        await SeedSystemGlAccountAsync(db, "1300");
        await SeedSystemGlAccountAsync(db, "2150");
        var (receipt, _, _) = await SeedStockReceiptAsync(db, companyId, qty: 10m, unitPrice: 5m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        await NewService(db, tenant).PostReceiptAsync(receipt.Id);

        var je = await db.JournalEntries.Include(j => j.Lines).SingleAsync();
        Assert.Equal(2, je.Lines.Count);
        Assert.All(je.Lines, l => Assert.True(l.AccountingKeyId.HasValue,
            $"Expected AccountingKeyId stamped on line Account={l.Account}; actual NULL"));

        // Both lines share CompanyId, so AccountingKeys differ only by AccountId.
        var keyCount = await db.Set<Abs.FixedAssets.Models.Masters.AccountingKey>()
            .CountAsync(k => k.CompanyId == companyId);
        Assert.Equal(2, keyCount);
    }

    [Fact]
    public async Task PostReceipt_WithoutGlAccountsSeeded_LeavesAccountingKeyIdNullAndStillWorks()
    {
        // Mirrors existing tests in this file — no GlAccount seeded → try/catch
        // in ResolveAccountAndKeyAsync swallows GlAccountResolutionException +
        // legacy Account string path continues unchanged.
        const int companyId = 100;
        await using var db = NewDb();
        var (receipt, _, _) = await SeedStockReceiptAsync(db, companyId, qty: 10m, unitPrice: 5m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        await NewService(db, tenant).PostReceiptAsync(receipt.Id);

        var je = await db.JournalEntries.Include(j => j.Lines).SingleAsync();
        Assert.Equal(2, je.Lines.Count);
        Assert.All(je.Lines, l => Assert.False(l.AccountingKeyId.HasValue,
            $"Expected AccountingKeyId NULL (orphan fallback); actual {l.AccountingKeyId}"));
        // Legacy Account strings still set.
        var dr = je.Lines.Single(l => l.Debit > 0);
        var cr = je.Lines.Single(l => l.Credit > 0);
        Assert.Equal("1300", dr.Account);
        Assert.Equal("2150", cr.Account);
    }
}
