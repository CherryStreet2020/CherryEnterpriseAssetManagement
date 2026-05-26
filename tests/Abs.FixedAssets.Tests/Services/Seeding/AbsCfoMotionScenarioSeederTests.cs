using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Seeding;

/// <summary>
/// Sprint 12.7 PR #5 — AbsCfoMotionScenarioSeeder tests.
///
/// Core invariants:
///   1. Missing fixtures (no company / no vendors / no GL accounts) →
///      seeder records warnings but does not throw.
///   2. Happy path — fixtures present → inserts demo rows in all 4 buckets.
///   3. Idempotency — calling SeedAsync twice yields no net new rows on
///      the second call. The KPI band shape is stable.
/// </summary>
public class AbsCfoMotionScenarioSeederTests
{
    private const int AbsCompanyId = 2;

    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<CipProject>().Ignore(p => p.RowVersion);
            mb.Entity<PurchaseOrder>().Ignore(p => p.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"abs-cfo-seeder-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static AbsCfoMotionScenarioSeeder NewSeeder(AppDbContext db) =>
        new(db, NullLogger<AbsCfoMotionScenarioSeeder>.Instance);

    /// <summary>
    /// Seeds the minimum FK fixtures the service needs: ABS Company,
    /// 2 GL accounts (one CashAndReceivables + one Other), 1 Book,
    /// 3 Vendors. Returns the AppDbContext after Save.
    /// </summary>
    private static async Task SeedFixturesAsync(AppDbContext db)
    {
        db.Companies.Add(new Company
        {
            Id = AbsCompanyId,
            CompanyCode = "ABS",
            Name = "ABS Machining",
            IsActive = true,
        });
        db.GlAccounts.Add(new GlAccount
        {
            AccountNumber = "1110",
            Name = "Cash on hand",
            Category = GlAccountCategory.CashAndReceivables,
            CompanyId = AbsCompanyId,
            IsActive = true,
        });
        db.GlAccounts.Add(new GlAccount
        {
            AccountNumber = "3000",
            Name = "Common stock",
            Category = GlAccountCategory.Equity,
            CompanyId = AbsCompanyId,
            IsActive = true,
        });
        db.Books.Add(new Book
        {
            Id = 1,
            Code = "GAAP",
            Name = "GAAP",
            CompanyId = AbsCompanyId,
            IsActive = true,
        });
        for (int i = 1; i <= 3; i++)
        {
            db.Vendors.Add(new Vendor
            {
                Id = i,
                Code = $"V-ABS-{i:D3}",
                Name = $"ABS Vendor {i}",
                CompanyId = AbsCompanyId,
                IsActive = true,
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedAsync_Without_Company_Returns_Warning_And_Zero_Inserts()
    {
        await using var db = NewDb();
        // No Company row seeded.
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalInserted);
        Assert.Contains(result.Warnings, w => w.Contains("Company"));
    }

    [Fact]
    public async Task SeedAsync_Happy_Path_Inserts_All_Four_Buckets()
    {
        await using var db = NewDb();
        await SeedFixturesAsync(db);
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(AbsCompanyId, result.CompanyId);
        Assert.True(result.CashLinesInserted > 0, "Cash JE lines should be inserted");
        Assert.True(result.VendorInvoicesInserted > 0, "Vendor invoices should be inserted");
        Assert.True(result.PurchaseOrdersInserted > 0, "Purchase orders should be inserted");
        Assert.True(result.CipProjectsInserted > 0, "CIP projects should be inserted");

        // Confirm rows actually landed in the database (not just queued).
        Assert.Single(await db.JournalEntries.Where(j => j.Batch == "DEMO-CFO-CASH-2026").ToListAsync());
        Assert.True(await db.Set<VendorInvoice>().AnyAsync(i => i.InvoiceNumber.StartsWith("DEMO-CFO-INV-")));
        Assert.True(await db.PurchaseOrders.AnyAsync(p => p.PONumber.StartsWith("DEMO-CFO-PO-")));
        Assert.True(await db.CipProjects.AnyAsync(p => p.ProjectNumber.StartsWith("DEMO-CFO-CIP-")));
    }

    [Fact]
    public async Task SeedAsync_Is_Idempotent_On_Second_Call()
    {
        await using var db = NewDb();
        await SeedFixturesAsync(db);
        var seeder = NewSeeder(db);

        var first = await seeder.SeedAsync(CancellationToken.None);
        var afterFirstInvoiceCount = await db.Set<VendorInvoice>().CountAsync();
        var afterFirstPoCount = await db.PurchaseOrders.CountAsync();
        var afterFirstCipCount = await db.CipProjects.CountAsync();
        var afterFirstJeCount = await db.JournalEntries.CountAsync();

        var second = await seeder.SeedAsync(CancellationToken.None);

        // Second call inserts zero new rows.
        Assert.Equal(0, second.TotalInserted);

        // Bucket totals stay stable.
        Assert.Equal(afterFirstInvoiceCount, await db.Set<VendorInvoice>().CountAsync());
        Assert.Equal(afterFirstPoCount, await db.PurchaseOrders.CountAsync());
        Assert.Equal(afterFirstCipCount, await db.CipProjects.CountAsync());
        Assert.Equal(afterFirstJeCount, await db.JournalEntries.CountAsync());

        // Skipped counts on second call reflect the populated state.
        Assert.True(second.TotalSkipped > 0,
            $"Second call should mark prior rows as skipped — TotalSkipped={second.TotalSkipped}");
    }
}
