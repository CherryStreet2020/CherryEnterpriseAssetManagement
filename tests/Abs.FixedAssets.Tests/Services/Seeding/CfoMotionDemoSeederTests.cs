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
/// Sprint 12.7 PR #5 — CfoMotionDemoSeeder tests.
///
/// Invariants:
///   1. No demo-tenant Company row → warning, zero inserts.
///   2. Company exists but Name doesn't start with "PWH" → safety guard
///      fires (refusing to write demo data to a non-placeholder tenant).
///   3. Happy path — placeholder tenant + fixtures → all 4 buckets insert.
///   4. Idempotency — second SeedAsync inserts 0 net new rows.
///
/// The seeder defends in depth: lookup by CompanyCode (not Id), then
/// verify Company.Name starts with the expected placeholder prefix
/// before writing.
/// </summary>
public class CfoMotionDemoSeederTests
{
    private const string DemoCode = "PWH-CAN";

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
            .UseInMemoryDatabase($"cfo-demo-seeder-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static CfoMotionDemoSeeder NewSeeder(AppDbContext db) =>
        new(db, NullLogger<CfoMotionDemoSeeder>.Instance);

    /// <summary>
    /// Seed the minimum FK fixtures the service needs against a
    /// placeholder tenant whose Name starts with "PWH": Company,
    /// 2 GL accounts, 1 Book, 3 Vendors.
    /// </summary>
    private static async Task SeedFixturesAsync(AppDbContext db, string companyCode = DemoCode, string companyName = "PWH MANUFACTURING CANADA")
    {
        db.Companies.Add(new Company
        {
            Id = 100,
            CompanyCode = companyCode,
            Name = companyName,
            IsActive = true,
        });
        db.GlAccounts.Add(new GlAccount
        {
            AccountNumber = "1110",
            Name = "Cash on hand",
            Category = GlAccountCategory.CashAndReceivables,
            CompanyId = 100,
            IsActive = true,
        });
        db.GlAccounts.Add(new GlAccount
        {
            AccountNumber = "3000",
            Name = "Common stock",
            Category = GlAccountCategory.Equity,
            CompanyId = 100,
            IsActive = true,
        });
        db.Books.Add(new Book
        {
            Id = 1,
            Code = "GAAP",
            Name = "GAAP",
            CompanyId = 100,
            IsActive = true,
        });
        for (int i = 1; i <= 3; i++)
        {
            db.Vendors.Add(new Vendor
            {
                Id = i,
                Code = $"V-{i:D3}",
                Name = $"Demo Vendor {i}",
                CompanyId = 100,
                IsActive = true,
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedAsync_Without_DemoCompany_Returns_Warning_And_Zero_Inserts()
    {
        await using var db = NewDb();
        // No Company row at all.
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalInserted);
        Assert.Contains(result.Warnings, w => w.Contains(DemoCode) && w.Contains("not found"));
    }

    [Fact]
    public async Task SeedAsync_Refuses_To_Write_When_CompanyName_Does_Not_Match_Placeholder_Prefix()
    {
        await using var db = NewDb();
        // Company exists with the demo CompanyCode but the Name has been
        // changed away from the PWH-* placeholder shape — perhaps a real
        // tenant was given this code by mistake. The seeder must refuse.
        await SeedFixturesAsync(db, companyCode: DemoCode, companyName: "Real Customer Co.");
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalInserted);
        Assert.Contains(result.Warnings, w => w.Contains("does NOT start with") && w.Contains("PWH"));
    }

    [Fact]
    public async Task SeedAsync_Happy_Path_Inserts_All_Four_Buckets()
    {
        await using var db = NewDb();
        await SeedFixturesAsync(db);
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(DemoCode, result.CompanyCode);
        Assert.True(result.CashLinesInserted > 0, "Cash JE lines should be inserted");
        Assert.True(result.VendorInvoicesInserted > 0, "Vendor invoices should be inserted");
        Assert.True(result.PurchaseOrdersInserted > 0, "Purchase orders should be inserted");
        Assert.True(result.CipProjectsInserted > 0, "CIP projects should be inserted");

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
        var afterFirstInvoice = await db.Set<VendorInvoice>().CountAsync();
        var afterFirstPo      = await db.PurchaseOrders.CountAsync();
        var afterFirstCip     = await db.CipProjects.CountAsync();
        var afterFirstJe      = await db.JournalEntries.CountAsync();

        var second = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(0, second.TotalInserted);
        Assert.Equal(afterFirstInvoice, await db.Set<VendorInvoice>().CountAsync());
        Assert.Equal(afterFirstPo,      await db.PurchaseOrders.CountAsync());
        Assert.Equal(afterFirstCip,     await db.CipProjects.CountAsync());
        Assert.Equal(afterFirstJe,      await db.JournalEntries.CountAsync());
        Assert.True(second.TotalSkipped > 0);
    }
}
