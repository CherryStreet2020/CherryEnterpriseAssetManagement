using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Controller;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Controller;

/// <summary>
/// Sprint 12.7 PR #4 — FinanceKpiService unit tests.
///
/// Covers the 4 KPI tiles + the compact money formatter:
///   1. Cash position    — empty DB → "$0" neutral; positive sum formatted compact
///   2. AP due this week — threshold escalation (neutral / info / warning / danger)
///                         + count subtext
///   3. Open POs         — only Approved/Sent/PartiallyReceived count; others ignored
///   4. WIP balance      — only Active CipProjects count; others ignored
///   5. FormatMoneyCompact (internal) — billions/millions/thousands/units + negatives
/// </summary>
public class FinanceKpiServiceTests
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
            .UseInMemoryDatabase($"finance-kpi-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static FinanceKpiService Build(AppDbContext db) =>
        new(db, NullLogger<FinanceKpiService>.Instance);

    // =====================================================================
    // Tile 1 — Cash position
    // =====================================================================

    [Fact]
    public async Task EmptyDb_All4TilesNeutral_NoNRE()
    {
        using var db = NewDb();
        var svc = Build(db);

        var band = await svc.GetBandAsync(companyId: null, CancellationToken.None);

        Assert.NotNull(band);
        // No cash accounts configured → "—" + neutral
        Assert.Equal("Cash position", band.CashPosition.Label);
        Assert.Equal("—", band.CashPosition.Value);
        Assert.Equal("neutral", band.CashPosition.Tone);

        Assert.Equal("AP due this week", band.ApDueThisWeek.Label);
        Assert.Equal("$0", band.ApDueThisWeek.Value);
        Assert.Equal("neutral", band.ApDueThisWeek.Tone);

        Assert.Equal("Open POs", band.OpenPos.Label);
        Assert.Equal("0", band.OpenPos.Value);
        Assert.Equal("neutral", band.OpenPos.Tone);

        Assert.Equal("WIP balance", band.WipBalance.Label);
        Assert.Equal("$0", band.WipBalance.Value);
        Assert.Equal("neutral", band.WipBalance.Tone);
    }

    // =====================================================================
    // Tile 2 — AP due this week threshold tones
    // =====================================================================

    [Theory]
    [InlineData(0,           "neutral", "$0")]     // empty bucket
    [InlineData(10_000,      "info",    "$10K")]   // info band
    [InlineData(49_999,      "info",    "$50K")]   // just below warning (rounded)
    [InlineData(50_100,      "warning", "$50.1K")] // entering warning (>$50K, formatter "0.#" prints one decimal)
    [InlineData(150_000,     "warning", "$150K")]  // middle of warning
    [InlineData(200_001,     "danger",  "$200K")]  // entering danger
    [InlineData(1_500_000,   "danger",  "$1.5M")]  // big danger
    public async Task ApDueThisWeek_ToneEscalation(decimal outstanding, string expectedTone, string expectedValue)
    {
        using var db = NewDb();
        if (outstanding > 0m)
        {
            db.Set<VendorInvoice>().Add(new VendorInvoice
            {
                InvoiceNumber = "INV-TEST-1",
                VendorId = 1,
                Status = InvoiceStatus.Approved,
                InvoiceDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(3), // inside the 7-day window
                Currency = "USD",
                Total = outstanding,
                AmountPaid = 0m,
            });
            await db.SaveChangesAsync();
        }

        var svc = Build(db);
        var band = await svc.GetBandAsync(companyId: null, CancellationToken.None);

        Assert.Equal("AP due this week", band.ApDueThisWeek.Label);
        Assert.Equal(expectedTone, band.ApDueThisWeek.Tone);
        Assert.Equal(expectedValue, band.ApDueThisWeek.Value);
    }

    [Fact]
    public async Task ApDueThisWeek_ExcludesPaid_ExcludesOutsideWindow()
    {
        using var db = NewDb();
        // In-window, approved, $100k outstanding → COUNTS
        db.Set<VendorInvoice>().Add(new VendorInvoice
        {
            InvoiceNumber = "INV-IN",
            VendorId = 1,
            Status = InvoiceStatus.Approved,
            DueDate = DateTime.UtcNow.Date.AddDays(3),
            Total = 100_000m, AmountPaid = 0m,
            Currency = "USD",
        });
        // Fully paid → EXCLUDED (Total == AmountPaid)
        db.Set<VendorInvoice>().Add(new VendorInvoice
        {
            InvoiceNumber = "INV-PAID",
            VendorId = 1,
            Status = InvoiceStatus.Paid,
            DueDate = DateTime.UtcNow.Date.AddDays(2),
            Total = 999_999m, AmountPaid = 999_999m,
            Currency = "USD",
        });
        // Outside window (due in 30 days) → EXCLUDED
        db.Set<VendorInvoice>().Add(new VendorInvoice
        {
            InvoiceNumber = "INV-FAR",
            VendorId = 1,
            Status = InvoiceStatus.Approved,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            Total = 500_000m, AmountPaid = 0m,
            Currency = "USD",
        });
        // Draft status → EXCLUDED (not yet actionable)
        db.Set<VendorInvoice>().Add(new VendorInvoice
        {
            InvoiceNumber = "INV-DRAFT",
            VendorId = 1,
            Status = InvoiceStatus.Draft,
            DueDate = DateTime.UtcNow.Date.AddDays(2),
            Total = 250_000m, AmountPaid = 0m,
            Currency = "USD",
        });
        await db.SaveChangesAsync();

        var band = await Build(db).GetBandAsync(null, CancellationToken.None);

        // Only the $100k Approved-in-window invoice counts → warning tone
        Assert.Equal("$100K", band.ApDueThisWeek.Value);
        Assert.Equal("warning", band.ApDueThisWeek.Tone);
        Assert.Equal("1 invoice", band.ApDueThisWeek.SubText);
    }

    // =====================================================================
    // Tile 3 — Open POs
    // =====================================================================

    [Fact]
    public async Task OpenPos_OnlyCountsActionableStatuses()
    {
        using var db = NewDb();
        // 3 should count
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-A", VendorId = 1, Status = POStatus.Approved,           Total = 50_000m,  Currency = "USD" });
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-B", VendorId = 1, Status = POStatus.Sent,               Total = 75_000m,  Currency = "USD" });
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-C", VendorId = 1, Status = POStatus.PartiallyReceived,  Total = 125_000m, Currency = "USD" });
        // 4 should be excluded
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-D", VendorId = 1, Status = POStatus.Draft,              Total = 999_999m, Currency = "USD" });
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-E", VendorId = 1, Status = POStatus.Closed,             Total = 999_999m, Currency = "USD" });
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-F", VendorId = 1, Status = POStatus.Invoiced,           Total = 999_999m, Currency = "USD" });
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-G", VendorId = 1, Status = POStatus.Cancelled,          Total = 999_999m, Currency = "USD" });
        await db.SaveChangesAsync();

        var band = await Build(db).GetBandAsync(null, CancellationToken.None);

        Assert.Equal("3", band.OpenPos.Value);
        Assert.Contains("250K", band.OpenPos.SubText);
        Assert.Equal("neutral", band.OpenPos.Tone);
    }

    // =====================================================================
    // Tile 4 — WIP balance
    // =====================================================================

    [Fact]
    public async Task WipBalance_OnlyCountsActiveCipProjects()
    {
        using var db = NewDb();
        // Active → counts
        db.Set<CipProject>().Add(new CipProject { ProjectNumber = "CP-1", Name = "P1", Status = CipProjectStatus.Active,      TotalCosts = 250_000m, Currency = "USD", CompanyId = 2 });
        db.Set<CipProject>().Add(new CipProject { ProjectNumber = "CP-2", Name = "P2", Status = CipProjectStatus.Active,      TotalCosts = 750_000m, Currency = "USD", CompanyId = 2 });
        // Other statuses → excluded
        db.Set<CipProject>().Add(new CipProject { ProjectNumber = "CP-3", Name = "P3", Status = CipProjectStatus.Capitalized, TotalCosts = 999_999m, Currency = "USD", CompanyId = 2 });
        db.Set<CipProject>().Add(new CipProject { ProjectNumber = "CP-4", Name = "P4", Status = CipProjectStatus.OnHold,      TotalCosts = 999_999m, Currency = "USD", CompanyId = 2 });
        db.Set<CipProject>().Add(new CipProject { ProjectNumber = "CP-5", Name = "P5", Status = CipProjectStatus.Cancelled,   TotalCosts = 999_999m, Currency = "USD", CompanyId = 2 });
        await db.SaveChangesAsync();

        var band = await Build(db).GetBandAsync(companyId: 2, CancellationToken.None);

        Assert.Equal("$1M", band.WipBalance.Value);
        Assert.Equal("2 active projects", band.WipBalance.SubText);
        Assert.Equal("neutral", band.WipBalance.Tone);
    }

    [Fact]
    public async Task WipBalance_ScopedToCompanyId()
    {
        using var db = NewDb();
        // Tenant 2 — counts
        db.Set<CipProject>().Add(new CipProject { ProjectNumber = "CP-IN",  Name = "in",  Status = CipProjectStatus.Active, TotalCosts = 100_000m, Currency = "USD", CompanyId = 2 });
        // Tenant 3 — excluded when companyId=2
        db.Set<CipProject>().Add(new CipProject { ProjectNumber = "CP-OUT", Name = "out", Status = CipProjectStatus.Active, TotalCosts = 999_999m, Currency = "USD", CompanyId = 3 });
        await db.SaveChangesAsync();

        var band = await Build(db).GetBandAsync(companyId: 2, CancellationToken.None);

        Assert.Equal("$100K", band.WipBalance.Value);
        Assert.Equal("1 active project", band.WipBalance.SubText);
    }

    // =====================================================================
    // Codex P1/P2 regression tests — tenant-scoping discipline
    // =====================================================================

    /// <summary>
    /// Codex P1 regression — AP due tile MUST filter by CompanyId.
    /// Without the fix, tenant 2's CFO would see the $999K tenant-3 invoice
    /// in their AP-due-this-week total + danger tone.
    /// </summary>
    [Fact]
    public async Task ApDueThisWeek_ScopedToCompanyId()
    {
        using var db = NewDb();
        // Tenant 2 — should count ($75K, info tone)
        db.Set<VendorInvoice>().Add(new VendorInvoice
        {
            InvoiceNumber = "INV-T2",
            VendorId = 1,
            Status = InvoiceStatus.Approved,
            DueDate = DateTime.UtcNow.Date.AddDays(3),
            Total = 75_000m, AmountPaid = 0m,
            Currency = "USD",
            CompanyId = 2,
        });
        // Tenant 3 — must NOT leak into tenant 2's tile (would push to danger)
        db.Set<VendorInvoice>().Add(new VendorInvoice
        {
            InvoiceNumber = "INV-T3",
            VendorId = 2,
            Status = InvoiceStatus.Approved,
            DueDate = DateTime.UtcNow.Date.AddDays(3),
            Total = 999_999m, AmountPaid = 0m,
            Currency = "USD",
            CompanyId = 3,
        });
        await db.SaveChangesAsync();

        var band = await Build(db).GetBandAsync(companyId: 2, CancellationToken.None);

        // Without the fix this would be "$1.1M" / "danger" / "2 invoices"
        Assert.Equal("$75K", band.ApDueThisWeek.Value);
        Assert.Equal("warning", band.ApDueThisWeek.Tone);
        Assert.Equal("1 invoice", band.ApDueThisWeek.SubText);
    }

    /// <summary>
    /// Codex P1 regression — Open POs tile MUST filter by CompanyId.
    /// Without the fix, count + committed total would include every tenant's
    /// open POs in the active tenant's hero band.
    /// </summary>
    [Fact]
    public async Task OpenPos_ScopedToCompanyId()
    {
        using var db = NewDb();
        // Tenant 2 — should count (1 PO, $50K committed)
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-T2", VendorId = 1, Status = POStatus.Approved, Total = 50_000m, Currency = "USD", CompanyId = 2 });
        // Tenant 3 — must NOT leak into tenant 2's tile
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-T3-A", VendorId = 2, Status = POStatus.Approved,          Total = 200_000m, Currency = "USD", CompanyId = 3 });
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-T3-B", VendorId = 2, Status = POStatus.Sent,              Total = 300_000m, Currency = "USD", CompanyId = 3 });
        db.Set<PurchaseOrder>().Add(new PurchaseOrder { PONumber = "PO-T3-C", VendorId = 2, Status = POStatus.PartiallyReceived, Total = 400_000m, Currency = "USD", CompanyId = 3 });
        await db.SaveChangesAsync();

        var band = await Build(db).GetBandAsync(companyId: 2, CancellationToken.None);

        // Without the fix this would be "4" / "$950K committed"
        Assert.Equal("1", band.OpenPos.Value);
        Assert.Contains("50K", band.OpenPos.SubText);
    }

    /// <summary>
    /// Codex P2 regression — admin/aggregate view (companyId = null) MUST
    /// include tenant-specific cash accounts, not just system templates.
    /// Without the fix, the previous expression collapsed to
    /// "g.CompanyId == null" when companyId was null, missing every
    /// tenant override and undercounting cash position.
    ///
    /// The companyId=null branch of BuildCashPositionAsync does NOT traverse
    /// JournalEntry/Book, so this test sets up only the GlAccount + raw
    /// JournalLines (Account-string match). JournalLines are added directly
    /// to the DbSet to avoid the InMemory provider's nav-collection-vs-DbSet
    /// gotcha documented in the Sprint 12.7 PR #2 ship memory.
    /// </summary>
    [Fact]
    public async Task CashPosition_NullCompanyId_IncludesTenantAccounts()
    {
        using var db = NewDb();

        // Tenant 2's cash account (CompanyId set) — must be included in
        // admin aggregate view (companyId = null).
        db.Set<GlAccount>().Add(new GlAccount
        {
            AccountNumber = "1110",
            Name = "Tenant 2 Operating Cash",
            Category = GlAccountCategory.CashAndReceivables,
            NormalBalance = NormalBalance.Debit,
            IsActive = true,
            CompanyId = 2,
        });

        // Two raw JournalLines whose Account matches the tenant's cash
        // account. Net position = 250K debit − 50K credit = 200K.
        db.Set<JournalLine>().Add(new JournalLine { Account = "1110", Debit = 250_000m, Credit = 0m });
        db.Set<JournalLine>().Add(new JournalLine { Account = "1110", Debit = 0m,        Credit = 50_000m });
        await db.SaveChangesAsync();

        var band = await Build(db).GetBandAsync(companyId: null, CancellationToken.None);

        // Without the fix, Value would be "—" (no system-template accounts exist
        // and the old expression filtered out tenant-2's account).
        // With the fix, admin view picks up tenant-2's "1110" → net $200K.
        Assert.Equal("$200K", band.CashPosition.Value);
        Assert.Equal("neutral", band.CashPosition.Tone);
    }

    // =====================================================================
    // FormatMoneyCompact
    // =====================================================================

    [Theory]
    [InlineData(0,                  "$0")]
    [InlineData(1,                  "$1")]
    [InlineData(999,                "$999")]
    [InlineData(1_000,              "$1K")]
    [InlineData(1_500,              "$1.5K")]
    [InlineData(999_999,            "$1000K")] // boundary: 999_999/1000 = 999.999 → "1000"
    [InlineData(1_000_000,          "$1M")]
    [InlineData(2_400_000,          "$2.4M")]
    [InlineData(1_500_000_000,      "$1.5B")]
    [InlineData(-1_200_000,         "-$1.2M")]
    [InlineData(-50_000,            "-$50K")]
    public void FormatMoneyCompact_RendersCompactly(decimal input, string expected)
    {
        Assert.Equal(expected, FinanceKpiService.FormatMoneyCompact(input));
    }
}
