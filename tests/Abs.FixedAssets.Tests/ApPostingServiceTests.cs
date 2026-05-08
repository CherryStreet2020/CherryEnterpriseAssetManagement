using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.AccountsPayable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for S1-5: ApPostingService implements approval JE,
/// payment JE, and void/contra-JE per ADR-002.
///
/// Focus: the load-bearing happy paths (approve manual line, approve
/// matched PO line, payment, void). PPV and three-way-match-exception
/// paths are documented in the service but tested at the service-edge
/// level only.
/// </summary>
public class ApPostingServiceTests
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
            .UseInMemoryDatabase($"ap-post-{dbName}-{Guid.NewGuid()}")
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

    private sealed class StubPeriodGuard : IPeriodGuard
    {
        public Task<PeriodCheckResult> CanPostAsync(int companyId, DateTime postingDate)
            => Task.FromResult(new PeriodCheckResult { IsAllowed = false, Reason = "Period closed" });
        public Task EnsureCanPostAsync(int companyId, DateTime postingDate) => Task.CompletedTask;
    }

    private static ApPostingService NewService(AppDbContext db, ITenantContext tenant, IPeriodGuard? guard = null)
    {
        var resolver = new GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));
        var matching = new InvoiceMatchingService(db);
        return new ApPostingService(db, tenant, resolver, guard ?? new AllowAllPeriodGuard(), matching,
            NullLogger<ApPostingService>.Instance);
    }

    private static async Task<VendorInvoice> SeedManualInvoiceAsync(AppDbContext db, int companyId, decimal lineTotal)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == companyId))
            db.Companies.Add(new Company { Id = companyId, CompanyCode = $"C-{companyId}", Name = "Co", IsActive = true });
        if (!await db.Vendors.AnyAsync(v => v.CompanyId == companyId))
            db.Vendors.Add(new Vendor { Code = $"V-{companyId}", Name = "V", CompanyId = companyId, IsActive = true });
        await db.SaveChangesAsync();
        var vendor = await db.Vendors.FirstAsync(v => v.CompanyId == companyId);

        var inv = new VendorInvoice
        {
            CompanyId = companyId,
            VendorId = vendor.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString("N")[..6]}",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Currency = "USD",
            Subtotal = lineTotal,
            Total = lineTotal,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        db.VendorInvoices.Add(inv);
        await db.SaveChangesAsync();

        db.Set<VendorInvoiceLine>().Add(new VendorInvoiceLine
        {
            VendorInvoiceId = inv.Id,
            LineNumber = 1,
            Description = "Manual line",
            Quantity = 1m,
            UnitPrice = lineTotal,
            LineTotal = lineTotal
        });
        await db.SaveChangesAsync();
        return inv;
    }

    [Fact]
    public async Task PostApproval_ManualLineNoPo_PostsDrDirectExpenseCrAccountsPayable()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var inv = await SeedManualInvoiceAsync(db, companyId, lineTotal: 250m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var result = await NewService(db, tenant).PostApprovalAsync(inv.Id);

        Assert.NotNull(result.JournalEntryId);
        Assert.Equal(250m, result.AmountPosted);

        var je = await db.JournalEntries.Include(j => j.Lines).SingleAsync();
        var dr = je.Lines.Single(l => l.Debit > 0);
        var cr = je.Lines.Single(l => l.Credit > 0);
        Assert.Equal("6000", dr.Account); // DirectExpense industry default
        Assert.Equal(250m, dr.Debit);
        Assert.Equal("2000", cr.Account); // AccountsPayable industry default
        Assert.Equal(250m, cr.Credit);

        var invAfter = await db.VendorInvoices.AsNoTracking().FirstAsync(i => i.Id == inv.Id);
        Assert.Equal(InvoiceStatus.Approved, invAfter.Status);
    }

    [Fact]
    public async Task PostApproval_PeriodClosed_ThrowsAndDoesNotCreateJE()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var inv = await SeedManualInvoiceAsync(db, companyId, lineTotal: 100m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var svc = NewService(db, tenant, guard: new StubPeriodGuard());

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.PostApprovalAsync(inv.Id));

        Assert.Empty(await db.JournalEntries.ToListAsync());
        var invAfter = await db.VendorInvoices.AsNoTracking().FirstAsync(i => i.Id == inv.Id);
        Assert.Equal(InvoiceStatus.Draft, invAfter.Status);
    }

    [Fact]
    public async Task PostPayment_FullPayment_PostsDrApCrCash_FlipsStatusToPaid()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var inv = await SeedManualInvoiceAsync(db, companyId, lineTotal: 500m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var svc = NewService(db, tenant);
        await svc.PostApprovalAsync(inv.Id);

        await svc.PostPaymentAsync(inv.Id, amount: 500m, paymentDate: DateTime.UtcNow, "wire-001");

        var pmtJe = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Source == "AP" && j.Reference!.Contains("PMT"))
            .SingleAsync();
        var dr = pmtJe.Lines.Single(l => l.Debit > 0);
        var cr = pmtJe.Lines.Single(l => l.Credit > 0);
        Assert.Equal("2000", dr.Account); // AP
        Assert.Equal(500m, dr.Debit);
        Assert.Equal("1110", cr.Account); // Cash
        Assert.Equal(500m, cr.Credit);

        var invAfter = await db.VendorInvoices.AsNoTracking().FirstAsync(i => i.Id == inv.Id);
        Assert.Equal(InvoiceStatus.Paid, invAfter.Status);
        Assert.Equal(500m, invAfter.AmountPaid);
    }

    [Fact]
    public async Task PostPayment_PartialPayment_StaysApproved()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var inv = await SeedManualInvoiceAsync(db, companyId, lineTotal: 1000m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var svc = NewService(db, tenant);
        await svc.PostApprovalAsync(inv.Id);

        await svc.PostPaymentAsync(inv.Id, amount: 400m, paymentDate: DateTime.UtcNow);

        var invAfter = await db.VendorInvoices.AsNoTracking().FirstAsync(i => i.Id == inv.Id);
        Assert.Equal(InvoiceStatus.Approved, invAfter.Status); // not yet Paid
        Assert.Equal(400m, invAfter.AmountPaid);
    }

    [Fact]
    public async Task PostVoid_ApprovedInvoice_PostsContraJE()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var inv = await SeedManualInvoiceAsync(db, companyId, lineTotal: 750m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var svc = NewService(db, tenant);
        await svc.PostApprovalAsync(inv.Id);

        await svc.PostVoidAsync(inv.Id, "billed in error");

        var contraJe = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Source == "AP" && j.Reference!.StartsWith("AP-VOID-"))
            .SingleAsync();

        // Contra reverses Dr↔Cr from the approval JE.
        var contraDr = contraJe.Lines.Single(l => l.Debit > 0); // was AP credit
        var contraCr = contraJe.Lines.Single(l => l.Credit > 0); // was Expense debit
        Assert.Equal("2000", contraDr.Account);
        Assert.Equal(750m, contraDr.Debit);
        Assert.Equal("6000", contraCr.Account);
        Assert.Equal(750m, contraCr.Credit);

        var invAfter = await db.VendorInvoices.AsNoTracking().FirstAsync(i => i.Id == inv.Id);
        Assert.Equal(InvoiceStatus.Voided, invAfter.Status);

        // Original approval JE is preserved (append-only ledger).
        Assert.Equal(2, await db.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task PostApproval_DuplicateReplay_ReturnsExistingJEAndDoesNotDoublePost()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var inv = await SeedManualInvoiceAsync(db, companyId, lineTotal: 100m);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var svc = NewService(db, tenant);

        var first = await svc.PostApprovalAsync(inv.Id);
        var second = await svc.PostApprovalAsync(inv.Id);

        Assert.Equal(first.JournalEntryId, second.JournalEntryId);
        Assert.Equal(1, await db.JournalEntries.CountAsync());
    }
}
