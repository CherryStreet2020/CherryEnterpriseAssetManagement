// B6 Foundation Sprint PR-FS-5 (2026-05-26) — ItemSourcingRuleService tests.
//
// All fixtures use REALISTIC precision-machining items + supplier scenarios
// per HARD LOCK feedback_no_fake_data.md:
//
//   9245 BRG-6207-2RS Ball Bearing — multi-vendor (Grainger primary Priority 1,
//        MSC backup Priority 2, Travers emergency Priority 3).
//   9270 BAR-1018-1.5X12 Steel Bar — customer-mandated sole-source from
//        Ryerson Steel (AS9100 §8.4.1) for heat traceability on GE Aviation
//        Customer-Id 42. Cannot be routinely suspended.
//   9302 EM-4FL-8MM End Mill — split-sourcing 60% Sandvik / 40% Kennametal at
//        Priority 1 (risk diversification across cutting-tool brands).
//
// Coverage:
//   1. Add + retrieve in priority order.
//   2. Cascade — per-Site rule sorts before company-wide at same priority.
//   3. Approve / Suspend / Probation state machine.
//   4. Customer-mandated AVL rules CANNOT be routinely suspended.
//   5. Split-sourcing allocation validation: sum > 100% throws on add.
//   6. PendingApproval rules NOT returned by default GetActiveRulesAsync.
//   7. Suspended rules NOT returned unless includeInactive=true.
//   8. EffectiveTo expiration removes rule from active set.
//   9. Validation: VendorId required for BuyFromVendor; CustomerId required for IsCustomerMandated.
//  10. GetPrimarySourceAsync returns highest-priority active rule.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Items;

public class ItemSourcingRuleServiceTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.CostLayer>().Ignore(c => c.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.ItemSourcingRule>().Ignore(r => r.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"item-srcrule-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static ItemSourcingRuleService NewService(AppDbContext db) =>
        new(db, NullLogger<ItemSourcingRuleService>.Instance);

    private static Item BearingBrg6207() => new()
    {
        Id = 9245,
        PartNumber = "BRG-6207-2RS",
        Description = "Ball Bearing 35x72x17mm Sealed",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
    };

    private static Item Bar1018() => new()
    {
        Id = 9270,
        PartNumber = "BAR-1018-1.5X12",
        Description = "Cold-rolled Steel Bar 1018, 1.5\" dia x 12 ft",
        StockUOM = "FT",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
    };

    private static Item EndMill() => new()
    {
        Id = 9302,
        PartNumber = "EM-4FL-8MM",
        Description = "8mm 4-Flute Square End Mill Carbide",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
    };

    [Fact]
    public async Task Add_Returns_Rules_In_Priority_Order()
    {
        // BRG-6207-2RS multi-vendor scenario: Grainger primary (Priority 1),
        // MSC backup (Priority 2), Travers emergency (Priority 3).
        // Vendor IDs: 101=Grainger, 102=MSC, 103=Travers.
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var r3 = await svc.AddRuleAsync(9245, 1, vendorId: 103, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 3, allocationPercent: null, minOrderQty: 50m, maxOrderQty: null, leadTimeDaysOverride: 21, isCustomerMandated: false, customerId: null, notes: "Travers Tool — emergency backup", createdBy: "buyer", ct: CancellationToken.None);
        var r1 = await svc.AddRuleAsync(9245, 1, vendorId: 101, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: 25m, maxOrderQty: null, leadTimeDaysOverride: 7,  isCustomerMandated: false, customerId: null, notes: "Grainger Industrial — primary",     createdBy: "buyer", ct: CancellationToken.None);
        var r2 = await svc.AddRuleAsync(9245, 1, vendorId: 102, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 2, allocationPercent: null, minOrderQty: 25m, maxOrderQty: null, leadTimeDaysOverride: 10, isCustomerMandated: false, customerId: null, notes: "MSC Industrial — backup",            createdBy: "buyer", ct: CancellationToken.None);

        // Approve all three so they show up in default (non-include-inactive) results.
        await svc.ApproveRuleAsync(r1.Id, "qm", CancellationToken.None);
        await svc.ApproveRuleAsync(r2.Id, "qm", CancellationToken.None);
        await svc.ApproveRuleAsync(r3.Id, "qm", CancellationToken.None);

        var rules = await svc.GetActiveRulesAsync(9245, 1, null, includeInactive: false, CancellationToken.None);
        Assert.Equal(3, rules.Count);
        Assert.Equal(101, rules[0].VendorId); // Priority 1 first
        Assert.Equal(102, rules[1].VendorId);
        Assert.Equal(103, rules[2].VendorId);

        var primary = await svc.GetPrimarySourceAsync(9245, 1, null, CancellationToken.None);
        Assert.NotNull(primary);
        Assert.Equal(101, primary!.VendorId);
    }

    [Fact]
    public async Task PerSite_Rule_Sorts_Before_Company_Wide_At_Same_Priority()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        // Company-wide rule (SiteId=null) and a Plant-1-specific rule at SAME Priority.
        // Plant-1 rule should sort first when querying for Site=1.
        var siteRule = await svc.AddRuleAsync(9245, siteId: 1,    vendorId: 101, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: "Plant-1 specific", createdBy: "buyer", ct: CancellationToken.None);
        var companyWide = await svc.AddRuleAsync(9245, siteId: null, vendorId: 102, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: "Company-wide", createdBy: "buyer", ct: CancellationToken.None);
        await svc.ApproveRuleAsync(siteRule.Id, "qm", CancellationToken.None);
        await svc.ApproveRuleAsync(companyWide.Id, "qm", CancellationToken.None);

        var rules = await svc.GetActiveRulesAsync(9245, siteId: 1, null, includeInactive: false, CancellationToken.None);
        Assert.Equal(2, rules.Count);
        Assert.Equal(1, rules[0].SiteId);     // per-Site first
        Assert.Null(rules[1].SiteId);         // company-wide second
    }

    [Fact]
    public async Task Approve_Suspend_Probation_State_Machine_Works()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var rule = await svc.AddRuleAsync(9245, 1, vendorId: 101, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: 25m, maxOrderQty: null, leadTimeDaysOverride: 7, isCustomerMandated: false, customerId: null, notes: "Grainger", createdBy: "buyer", ct: CancellationToken.None);
        Assert.Equal(SourcingApprovalState.PendingApproval, rule.ApprovalState);

        var approved = await svc.ApproveRuleAsync(rule.Id, "qm", CancellationToken.None);
        Assert.Equal(SourcingApprovalState.Approved, approved.ApprovalState);
        Assert.NotNull(approved.ApprovedAtUtc);

        var probation = await svc.PutOnProbationAsync(rule.Id, "Receiving inspection AQL elevated 2026-W22", "qm", CancellationToken.None);
        Assert.Equal(SourcingApprovalState.Probation, probation.ApprovalState);

        var suspended = await svc.SuspendRuleAsync(rule.Id, "Vendor failed source audit 2026-W23", "qm", CancellationToken.None);
        Assert.Equal(SourcingApprovalState.Suspended, suspended.ApprovalState);
        Assert.NotNull(suspended.SuspendedAtUtc);

        // Re-approve clears the suspension.
        var reApproved = await svc.ApproveRuleAsync(rule.Id, "qm", CancellationToken.None);
        Assert.Equal(SourcingApprovalState.Approved, reApproved.ApprovalState);
        Assert.Null(reApproved.SuspendedAtUtc);
    }

    [Fact]
    public async Task CustomerMandated_AVL_Cannot_Be_Routinely_Suspended()
    {
        // GE Aviation (CustomerId=42) mandates Ryerson Steel as sole-source
        // for BAR-1018 due to AS9100 §8.4.1 heat-trace requirement.
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var rule = await svc.AddRuleAsync(9270, 1, vendorId: 201 /* Ryerson */, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: 48m, maxOrderQty: null, leadTimeDaysOverride: 14, isCustomerMandated: true, customerId: 42, notes: "GE Aviation customer-mandated AVL — AS9100 §8.4.1 heat-trace sole-source", createdBy: "qm", ct: CancellationToken.None);
        await svc.ApproveRuleAsync(rule.Id, "qm", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await svc.SuspendRuleAsync(rule.Id, "Routine quality flag — AQL elevated", "qm", CancellationToken.None));

        // Verify rule remains Approved.
        var stillActive = await svc.GetActiveRulesAsync(9270, 1, null, includeInactive: false, CancellationToken.None);
        Assert.Single(stillActive);
        Assert.Equal(SourcingApprovalState.Approved, stillActive[0].ApprovalState);
    }

    [Fact]
    public async Task SplitSourcing_Allocation_Sum_Over_100_Throws()
    {
        // EM-4FL-8MM split-sourcing risk diversification: 60% Sandvik / 40% Kennametal.
        // Sum to 100% should work. Adding a third at 30% should throw (would exceed 100%).
        await using var db = NewDb();
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddRuleAsync(9302, 1, vendorId: 301 /* Sandvik */,    transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: 60m, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: "Sandvik primary",    createdBy: "buyer", ct: CancellationToken.None);
        await svc.AddRuleAsync(9302, 1, vendorId: 302 /* Kennametal */, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: 40m, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: "Kennametal backup", createdBy: "buyer", ct: CancellationToken.None);

        // Third addition at 30% would push total to 130%.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await svc.AddRuleAsync(9302, 1, vendorId: 303, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: 30m, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: null, createdBy: "buyer", ct: CancellationToken.None));
    }

    [Fact]
    public async Task PendingApproval_Rules_Excluded_By_Default()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddRuleAsync(9245, 1, vendorId: 101, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: "Pending", createdBy: "buyer", ct: CancellationToken.None);

        var active = await svc.GetActiveRulesAsync(9245, 1, null, includeInactive: false, CancellationToken.None);
        Assert.Empty(active);

        var withInactive = await svc.GetActiveRulesAsync(9245, 1, null, includeInactive: true, CancellationToken.None);
        Assert.Single(withInactive);
        Assert.Equal(SourcingApprovalState.PendingApproval, withInactive[0].ApprovalState);
    }

    [Fact]
    public async Task Suspended_Rules_Excluded_From_Active_Set()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var rule = await svc.AddRuleAsync(9245, 1, vendorId: 101, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: "Grainger", createdBy: "buyer", ct: CancellationToken.None);
        await svc.ApproveRuleAsync(rule.Id, "qm", CancellationToken.None);
        await svc.SuspendRuleAsync(rule.Id, "Vendor failed quality audit 2026-W22", "qm", CancellationToken.None);

        var active = await svc.GetActiveRulesAsync(9245, 1, null, includeInactive: false, CancellationToken.None);
        Assert.Empty(active);
    }

    [Fact]
    public async Task EffectiveTo_Expiration_Removes_Rule_From_Active_Set()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var rule = await svc.AddRuleAsync(9245, 1, vendorId: 101, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: "Grainger Q1 contract", createdBy: "buyer", ct: CancellationToken.None);
        await svc.ApproveRuleAsync(rule.Id, "qm", CancellationToken.None);

        // Manually stamp an expiration in the past (the service surface doesn't
        // expose EffectiveTo mutation in this PR — that's a follow-up).
        var entity = await db.ItemSourcingRules.FirstAsync(r => r.Id == rule.Id);
        entity.EffectiveToUtc = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var active = await svc.GetActiveRulesAsync(9245, 1, null, includeInactive: false, CancellationToken.None);
        Assert.Empty(active);
    }

    [Fact]
    public async Task Validation_Requires_VendorId_For_BuyFromVendor()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await svc.AddRuleAsync(9245, 1, vendorId: null, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: false, customerId: null, notes: null, createdBy: "buyer", ct: CancellationToken.None));
    }

    [Fact]
    public async Task Validation_Requires_CustomerId_When_CustomerMandated()
    {
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await svc.AddRuleAsync(9270, 1, vendorId: 201, transferFromSiteId: null, SourceMethod.BuyFromVendor, priority: 1, allocationPercent: null, minOrderQty: null, maxOrderQty: null, leadTimeDaysOverride: null, isCustomerMandated: true, customerId: null /* missing */, notes: null, createdBy: "qm", ct: CancellationToken.None));
    }

    [Fact]
    public async Task MakeInternal_Source_Does_Not_Require_Vendor()
    {
        await using var db = NewDb();
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var rule = await svc.AddRuleAsync(9302, 1, vendorId: null, transferFromSiteId: null, SourceMethod.MakeInternal, priority: 2, allocationPercent: null, minOrderQty: 1m, maxOrderQty: null, leadTimeDaysOverride: 5, isCustomerMandated: false, customerId: null, notes: "Internal regrind/refurbish capability", createdBy: "engineering", ct: CancellationToken.None);
        Assert.Equal(SourceMethod.MakeInternal, rule.SourceMethod);
        Assert.Null(rule.VendorId);
    }
}
