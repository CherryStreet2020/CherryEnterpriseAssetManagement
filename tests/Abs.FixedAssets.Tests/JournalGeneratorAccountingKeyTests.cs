using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// PRA-5g (2026-05-25) — JournalGenerator dual-write of AccountingKeyId on the
/// two depreciation legs (DR Depreciation Expense / CR Accumulated Depreciation).
///
/// Covers three paths matching the PRA-5c/5d/5e/5f cleanup-pass contract:
///   1. <c>glResolver == null</c>           → AccountingKeyId NULL on both lines
///                                            (back-compat for manual JE pages
///                                            and tests that don't plumb DI).
///   2. resolver returns key ids            → AccountingKeyId stamped on both
///                                            lines (the demo-path behavior
///                                            kicked off from period close).
///   3. resolver throws GlAccountResolutionException
///                                          → DEF-008 fallback: legacy Account
///                                            string still stamped, AccountingKeyId
///                                            NULL, JE still balances and posts.
/// </summary>
public class JournalGeneratorAccountingKeyTests
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
            .UseInMemoryDatabase($"jgen-key-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    /// <summary>
    /// Stub resolver that returns deterministic keys per <see cref="GlAccountKind"/>.
    /// </summary>
    private sealed class StubResolverReturning : IGlAccountResolver
    {
        public Dictionary<GlAccountKind, int> Keys { get; } = new();
        public Task<string> ResolveAsync(int companyId, GlAccountKind kind, GlResolveContext? context = null)
            => Task.FromResult($"acct-{(int)kind}");
        public Task<int> ResolveAccountingKeyAsync(
            int companyId, GlAccountKind kind, AccountingKeyResolveContext keyContext,
            GlResolveContext? glContext = null, CancellationToken ct = default)
            => Keys.TryGetValue(kind, out var id)
                ? Task.FromResult(id)
                : Task.FromException<int>(new GlAccountResolutionException(
                    companyId, kind, new[] { "stub" }));
    }

    /// <summary>
    /// Stub resolver that always throws — exercises the DEF-008 fallback path.
    /// </summary>
    private sealed class StubResolverThrowing : IGlAccountResolver
    {
        public Task<string> ResolveAsync(int companyId, GlAccountKind kind, GlResolveContext? context = null)
            => Task.FromResult($"acct-{(int)kind}");
        public Task<int> ResolveAccountingKeyAsync(
            int companyId, GlAccountKind kind, AccountingKeyResolveContext keyContext,
            GlResolveContext? glContext = null, CancellationToken ct = default)
            => Task.FromException<int>(new GlAccountResolutionException(
                companyId, kind, new[] { "stub-throws" }));
    }

    private static async Task<Book> SeedBookAsync(AppDbContext db, int companyId)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == companyId))
            db.Companies.Add(new Company { Id = companyId, CompanyCode = $"C-{companyId}", Name = "Co", IsActive = true });
        await db.SaveChangesAsync();

        var book = new Book
        {
            Code = $"PRA5G-{Guid.NewGuid().ToString("N")[..6]}",
            Name = "Test Book PRA-5g",
            CompanyId = companyId,
            GlAccountDepExp = "6100",
            GlAccountAccumDep = "1900"
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book;
    }

    [Fact]
    public async Task GenerateMonthlyWithAmountAsync_NoResolver_AccountingKeyIdNullOnBothLines()
    {
        // Back-compat path: legacy callers (manual JE page, HistoricJournalBackfillService,
        // older tests) don't pass a resolver. Both JournalLine rows must still post
        // with the legacy Account string and AccountingKeyId left NULL.
        const int companyId = 700;
        await using var db = NewDb();
        var book = await SeedBookAsync(db, companyId);

        var entry = await JournalGenerator.GenerateMonthlyWithAmountAsync(
            db,
            bookId: book.Id,
            month: new DateTime(2026, 4, 1),
            monthlyTotal: 1_234.56m,
            createdBy: "test",
            companyId: companyId,
            enforcePeriodLock: false,
            saveChanges: true,
            glResolver: null,
            logger: null);

        Assert.Equal(2, entry.Lines!.Count);
        var dr = entry.Lines.Single(l => l.Debit > 0m);
        var cr = entry.Lines.Single(l => l.Credit > 0m);
        Assert.Equal("6100", dr.Account);
        Assert.Equal("1900", cr.Account);
        Assert.Null(dr.AccountingKeyId);
        Assert.Null(cr.AccountingKeyId);
        Assert.Equal(1_234.56m, dr.Debit);
        Assert.Equal(1_234.56m, cr.Credit);
    }

    [Fact]
    public async Task GenerateMonthlyWithAmountAsync_ResolverReturnsKeys_StampsAccountingKeyIdOnBothLines()
    {
        // Demo path: PeriodCloseOrchestrationService injects the real resolver,
        // both depreciation legs land with AccountingKeyId stamped.
        const int companyId = 701;
        await using var db = NewDb();
        var book = await SeedBookAsync(db, companyId);

        var resolver = new StubResolverReturning();
        resolver.Keys[GlAccountKind.DepreciationExpense] = 42;
        resolver.Keys[GlAccountKind.AccumulatedDepreciation] = 43;

        var entry = await JournalGenerator.GenerateMonthlyWithAmountAsync(
            db,
            bookId: book.Id,
            month: new DateTime(2026, 4, 1),
            monthlyTotal: 999.99m,
            createdBy: "test",
            companyId: companyId,
            enforcePeriodLock: false,
            saveChanges: true,
            glResolver: resolver,
            logger: NullLogger.Instance);

        var dr = entry.Lines!.Single(l => l.Debit > 0m);
        var cr = entry.Lines.Single(l => l.Credit > 0m);
        Assert.Equal(42, dr.AccountingKeyId);
        Assert.Equal(43, cr.AccountingKeyId);
        Assert.Equal("6100", dr.Account); // legacy string still stamped
        Assert.Equal("1900", cr.Account);
    }

    [Fact]
    public async Task GenerateMonthlyWithAmountAsync_ResolverThrows_FallsBackToNullKeyButKeepsLegacyAccount()
    {
        // DEF-008 fallback: orphan path (legacy account-number string has no
        // matching GlAccount row → resolver throws). The JE must STILL post —
        // legacy Account string stamped, AccountingKeyId NULL, JE balanced.
        const int companyId = 702;
        await using var db = NewDb();
        var book = await SeedBookAsync(db, companyId);

        var entry = await JournalGenerator.GenerateMonthlyWithAmountAsync(
            db,
            bookId: book.Id,
            month: new DateTime(2026, 4, 1),
            monthlyTotal: 77.00m,
            createdBy: "test",
            companyId: companyId,
            enforcePeriodLock: false,
            saveChanges: true,
            glResolver: new StubResolverThrowing(),
            logger: NullLogger.Instance);

        var dr = entry.Lines!.Single(l => l.Debit > 0m);
        var cr = entry.Lines.Single(l => l.Credit > 0m);
        Assert.Equal("6100", dr.Account);
        Assert.Equal("1900", cr.Account);
        Assert.Null(dr.AccountingKeyId);
        Assert.Null(cr.AccountingKeyId);
        // Balanced
        Assert.Equal(dr.Debit, cr.Credit);
    }
}
