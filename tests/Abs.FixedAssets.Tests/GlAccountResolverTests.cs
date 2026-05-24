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

    // =========================================================================
    // Sprint 13.5 PRA-5b — ResolveAccountingKeyAsync coverage.
    //
    // The new method extends the existing cascade: resolves the account-number
    // string (existing path), looks up GlAccount.Id, denormalizes
    // IndustryVertical from Company, computes a deterministic sha256 hash
    // over the canonical 8-segment string, and find-or-inserts an
    // AccountingKey row. Returns the row's Id.
    // =========================================================================

    private static async Task SeedGlAccountAsync(AppDbContext db, int? companyId, string accountNumber)
    {
        if (!await db.Set<GlAccount>().AnyAsync(a => a.AccountNumber == accountNumber && a.CompanyId == companyId))
        {
            db.Set<GlAccount>().Add(new GlAccount
            {
                AccountNumber = accountNumber,
                Name = $"Test account {accountNumber}",
                CompanyId = companyId,
                AccountType = GlAccountType.Asset,
                Category = GlAccountCategory.CashAndReceivables,
                NormalBalance = NormalBalance.Debit,
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ResolveAccountingKey_HappyPath_MintsRowAndReturnsId()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        await SeedGlAccountAsync(db, companyId: null, accountNumber: "1500"); // industry-default AssetCost
        var resolver = NewResolver(db);

        var keyId = await resolver.ResolveAccountingKeyAsync(
            companyId,
            GlAccountKind.AssetCost,
            new AccountingKeyResolveContext()); // all segments NULL

        Assert.True(keyId > 0);
        var row = await db.Set<Abs.FixedAssets.Models.Masters.AccountingKey>()
            .FirstOrDefaultAsync(k => k.Id == keyId);
        Assert.NotNull(row);
        Assert.Equal(companyId, row!.CompanyId);
        Assert.Null(row.SiteId);
        Assert.Null(row.CostCenterId);
        Assert.NotEmpty(row.AccountingKeyHash);
        Assert.Equal(64, row.AccountingKeyHash.Length); // sha256-hex
    }

    [Fact]
    public async Task ResolveAccountingKey_RepeatCall_ReturnsSameId()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        await SeedGlAccountAsync(db, companyId: null, accountNumber: "1500");
        var resolver = NewResolver(db);

        var first = await resolver.ResolveAccountingKeyAsync(
            companyId, GlAccountKind.AssetCost, new AccountingKeyResolveContext());
        var second = await resolver.ResolveAccountingKeyAsync(
            companyId, GlAccountKind.AssetCost, new AccountingKeyResolveContext());

        Assert.Equal(first, second);
        // And only one row exists in the table.
        var rowCount = await db.Set<Abs.FixedAssets.Models.Masters.AccountingKey>()
            .CountAsync(k => k.CompanyId == companyId);
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task ResolveAccountingKey_DifferentSegments_MintDistinctRows()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        await SeedGlAccountAsync(db, companyId: null, accountNumber: "1500");
        var resolver = NewResolver(db);

        var keyA = await resolver.ResolveAccountingKeyAsync(
            companyId, GlAccountKind.AssetCost,
            new AccountingKeyResolveContext(DepartmentId: 7));
        var keyB = await resolver.ResolveAccountingKeyAsync(
            companyId, GlAccountKind.AssetCost,
            new AccountingKeyResolveContext(DepartmentId: 8));
        var keyC = await resolver.ResolveAccountingKeyAsync(
            companyId, GlAccountKind.AssetCost,
            new AccountingKeyResolveContext(DepartmentId: 7, ProjectId: 42));

        // 3 different segment combos = 3 different AccountingKey rows.
        Assert.NotEqual(keyA, keyB);
        Assert.NotEqual(keyA, keyC);
        Assert.NotEqual(keyB, keyC);
    }

    [Fact]
    public async Task ResolveAccountingKey_NoMatchingGlAccount_Throws()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        // Deliberately DO NOT seed GlAccount "1500" — the industry default
        // string will resolve, but no row matches in COA.
        var resolver = NewResolver(db);

        await Assert.ThrowsAsync<GlAccountResolutionException>(() =>
            resolver.ResolveAccountingKeyAsync(
                companyId, GlAccountKind.AssetCost, new AccountingKeyResolveContext()));
    }

    [Fact]
    public void BuildCanonicalKeyString_NullSegmentsSerializeAsEmpty()
    {
        // Matches the SQL backfill canonical form: NULL → ''.
        var canonical = GlAccountResolver.BuildCanonicalKeyString(
            companyId: 1,
            siteId: null,
            accountId: 42,
            costCenterId: null,
            departmentId: null,
            projectId: null,
            interCoPartnerCompanyId: null,
            vertical: null);

        // 7 pipes total: 2 before "42" (Company|Site|Account), 5 after
        // (CostCenter|Department|Project|InterCo|Vertical). SQL backfill
        // produces the IDENTICAL string — verified via python sha256
        // round-trip during PRA-5b pre-flight.
        Assert.Equal("1||42|||||", canonical);
    }

    [Fact]
    public void BuildCanonicalKeyString_AllSegmentsPopulated_FormatsCorrectly()
    {
        var canonical = GlAccountResolver.BuildCanonicalKeyString(
            companyId: 1,
            siteId: 17,
            accountId: 5610,
            costCenterId: 110100,
            departmentId: 2009,
            projectId: 14,
            interCoPartnerCompanyId: 2,
            vertical: IndustryVertical.Machining);

        // Vertical.Machining = 1 (short).
        Assert.Equal("1|17|5610|110100|2009|14|2|1", canonical);
    }

    [Fact]
    public void Sha256Hex_KnownInput_ProducesExpectedHash()
    {
        // Lock-in: sha256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        var actual = GlAccountResolver.Sha256Hex("hello");
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", actual);
        Assert.Equal(64, actual.Length);
    }
}
