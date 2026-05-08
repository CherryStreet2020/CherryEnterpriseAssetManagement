using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Lock-in tests for ADR-003 / S2-7: central GL account resolver.
/// Cascade order:
///   1. Per-entity override (Asset.GLAssetAccount, BookGlAccount)
///   2. Per-book defaults (Book.GlAccountDepExp/AccumDep)
///   3. Per-company config (CompanyGlAccountConfigs)
///   4. Industry-default constants
///   5. Fail with GlAccountResolutionException
/// </summary>
public class GlAccountResolverTests
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
            .UseInMemoryDatabase($"gl-resolver-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static GlAccountResolver NewResolver(AppDbContext db)
        => new GlAccountResolver(db, new MemoryCache(new MemoryCacheOptions()));

    private static async Task<int> SeedCompanyAsync(AppDbContext db, int id = 100)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == id))
        {
            db.Companies.Add(new Company { Id = id, CompanyCode = $"C-{id}", Name = $"Co {id}", IsActive = true });
            await db.SaveChangesAsync();
        }
        return id;
    }

    [Fact]
    public async Task Resolve_NoConfigAnywhere_FallsBackToIndustryDefault()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var resolver = NewResolver(db);

        var account = await resolver.ResolveAsync(companyId, GlAccountKind.AssetCost);
        Assert.Equal("1500", account); // industry default
    }

    [Fact]
    public async Task Resolve_PerCompanyConfig_TakesPrecedenceOverIndustryDefault()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        db.Set<CompanyGlAccountConfig>().Add(new CompanyGlAccountConfig
        {
            CompanyId = companyId,
            AccountKind = GlAccountKind.AssetCost,
            GlAccount = "9999"
        });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var account = await resolver.ResolveAsync(companyId, GlAccountKind.AssetCost);
        Assert.Equal("9999", account); // company config wins
    }

    [Fact]
    public async Task Resolve_PerBookOverride_TakesPrecedenceOverCompanyConfig()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var book = new Book
        {
            Code = "GAAP", Name = "GAAP", CompanyId = companyId,
            BookType = BookType.Financial, IsActive = true,
            GlAccountDepExp = "BOOK-DEP-EXP",
            GlAccountAccumDep = "BOOK-ACCUM-DEP"
        };
        db.Books.Add(book);
        db.Set<CompanyGlAccountConfig>().Add(new CompanyGlAccountConfig
        {
            CompanyId = companyId,
            AccountKind = GlAccountKind.DepreciationExpense,
            GlAccount = "COMPANY-DEP-EXP"
        });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var account = await resolver.ResolveAsync(
            companyId,
            GlAccountKind.DepreciationExpense,
            new GlResolveContext(BookId: book.Id));
        Assert.Equal("BOOK-DEP-EXP", account); // book trumps company
    }

    [Fact]
    public async Task Resolve_PerEntityAssetOverride_TakesPrecedenceOverBook()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var asset = new Asset
        {
            AssetNumber = "A-1", Description = "x", CompanyId = companyId,
            AcquisitionCost = 1000m, UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow,
            GLAssetAccount = "ASSET-LEVEL-1500"
        };
        db.Assets.Add(asset);
        var book = new Book
        {
            Code = "GAAP", Name = "GAAP", CompanyId = companyId,
            BookType = BookType.Financial, IsActive = true
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var account = await resolver.ResolveAsync(
            companyId,
            GlAccountKind.AssetCost,
            new GlResolveContext(AssetId: asset.Id, BookId: book.Id));
        Assert.Equal("ASSET-LEVEL-1500", account); // asset trumps everything
    }

    [Fact]
    public async Task Resolve_UnknownKindWithNoIndustryDefault_Throws()
    {
        // We don't actually have a kind without an industry default in the
        // current enum. This test verifies the fail-fast path by passing a
        // company that exists but inserting a "fake" out-of-range kind value
        // via direct cast — which IndustryDefaults.For returns null for.
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var resolver = NewResolver(db);

        var unknownKind = (GlAccountKind)9999;
        var ex = await Assert.ThrowsAsync<GlAccountResolutionException>(
            () => resolver.ResolveAsync(companyId, unknownKind));
        Assert.Equal(companyId, ex.CompanyId);
        Assert.Equal(unknownKind, ex.Kind);
        Assert.NotEmpty(ex.CascadeHistory);
    }

    [Fact]
    public async Task Resolve_AllIndustryDefaults_AreNonEmptyForKnownKinds()
    {
        // Lock down: every declared GlAccountKind has an industry-default
        // mapping. If a future PR adds a kind without a default, this fails.
        var allKinds = Enum.GetValues<GlAccountKind>();
        foreach (var kind in allKinds)
        {
            var dflt = GlAccountResolver.IndustryDefaults.For(kind);
            Assert.False(string.IsNullOrWhiteSpace(dflt),
                $"GlAccountKind.{kind} has no industry default — add one to GlAccountResolver.IndustryDefaults.For.");
        }
    }

    [Fact]
    public async Task Resolve_CachesPerCompanyConfig()
    {
        // Second call shouldn't re-query. We can't easily count queries against
        // InMemory, so prove caching by mutating the row after the first
        // resolve and asserting the second resolve returns the cached value.
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var row = new CompanyGlAccountConfig
        {
            CompanyId = companyId,
            AccountKind = GlAccountKind.Inventory,
            GlAccount = "INVENTORY-V1"
        };
        db.Set<CompanyGlAccountConfig>().Add(row);
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var first = await resolver.ResolveAsync(companyId, GlAccountKind.Inventory);
        Assert.Equal("INVENTORY-V1", first);

        // Mutate the row directly. The resolver should still return v1 (cached).
        row.GlAccount = "INVENTORY-V2";
        await db.SaveChangesAsync();

        var second = await resolver.ResolveAsync(companyId, GlAccountKind.Inventory);
        Assert.Equal("INVENTORY-V1", second); // cached
    }
}
