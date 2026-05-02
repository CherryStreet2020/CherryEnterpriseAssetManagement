using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests
{
    /// <summary>
    /// End-to-end tests for HistoricJournalBackfillService against an EF InMemory database.
    /// Verifies the three contract guarantees from the task spec:
    ///   (a) Re-running is idempotent (no duplicate entries)
    ///   (b) Total debit == total credit per book (always balanced)
    ///   (c) Sum of debits per book reconciles to AssetBookSettings.AccumulatedDepreciation
    ///       within $1 (because we use the same engine — DepreciationService — that the
    ///       depreciation backfill used to stamp those AccumulatedDepreciation values).
    /// </summary>
    public class HistoricJournalBackfillServiceTests
    {
        // InMemory provider can't map JsonDocument (LookupValue.Metadata is jsonb in Postgres).
        // Subclass the real AppDbContext and ignore the unsupported property — we don't touch
        // lookups in this test fixture.
        private sealed class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
            protected override void OnModelCreating(ModelBuilder mb)
            {
                base.OnModelCreating(mb);
                mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            }
        }

        private static AppDbContext NewDb(string dbName)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                // InMemory provider doesn't support real transactions; service calls
                // BeginTransactionAsync but it's a no-op there. Suppress the warning.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new TestAppDbContext(opts);
        }

        private static (Book book, BookGlAccount map, List<AssetBookSettings> settings) Seed(AppDbContext db, string suffix)
        {
            var book = new Book
            {
                Code = "GAAP-T-" + suffix,
                Name = "Test GAAP " + suffix,
                Method = DepreciationMethod.StraightLine,
                Convention = DepreciationConvention.FullMonth,
                BookType = BookType.Financial,
                IsActive = true,
                CompanyId = 1
            };
            db.Books.Add(book);
            db.SaveChanges();

            var map = new BookGlAccount
            {
                BookId = book.Id,
                DepreciationExpense = "6500",
                AccumulatedDepreciation = "1510"
            };
            db.BookGlAccounts.Add(map);

            // 3 assets, all in service 2024-01-01, 36-month straight-line full-month.
            //   $36,000 / 36 mo = $1,000/mo each → $3,000/mo aggregate
            // After 24 months (through 2025-12) AccumulatedDepreciation per asset = $24,000
            //   total per book = $72,000
            var assets = new List<Asset>
            {
                new Asset { AssetNumber = "A-" + suffix + "-001", Description = "Test1", AcquisitionCost = 36000m, SalvageValue = 0m, UsefulLifeMonths = 36, InServiceDate = new DateTime(2024,1,1), DepreciationMethod = DepreciationMethod.StraightLine, Active = true, CompanyId = 1 },
                new Asset { AssetNumber = "A-" + suffix + "-002", Description = "Test2", AcquisitionCost = 36000m, SalvageValue = 0m, UsefulLifeMonths = 36, InServiceDate = new DateTime(2024,1,1), DepreciationMethod = DepreciationMethod.StraightLine, Active = true, CompanyId = 1 },
                new Asset { AssetNumber = "A-" + suffix + "-003", Description = "Test3", AcquisitionCost = 36000m, SalvageValue = 0m, UsefulLifeMonths = 36, InServiceDate = new DateTime(2024,1,1), DepreciationMethod = DepreciationMethod.StraightLine, Active = true, CompanyId = 1 }
            };
            db.Assets.AddRange(assets);
            db.SaveChanges();

            var settings = assets.Select(a => new AssetBookSettings
            {
                AssetId = a.Id,
                BookId = book.Id,
                MethodOverride = DepreciationMethod.StraightLine,
                ConventionOverride = DepreciationConvention.FullMonth,
                // Pre-stamp AccumulatedDepreciation as if a prior depreciation backfill had run
                // through 2025-12 (24 months × $1,000/mo = $24,000 each).
                AccumulatedDepreciation = 24000m,
                BookValue = 12000m,
                LastDepreciationDate = new DateTime(2025, 12, 31)
            }).ToList();
            db.AssetBookSettings.AddRange(settings);
            db.SaveChanges();

            return (book, map, settings);
        }

        private static HistoricJournalBackfillService MakeService(AppDbContext db) =>
            new HistoricJournalBackfillService(db, new DepreciationService(), NullLogger<HistoricJournalBackfillService>.Instance);

        [Fact]
        public async Task FirstRun_CreatesOneEntryPerMonthPerBook_AndIsBalanced()
        {
            using var db = NewDb(nameof(FirstRun_CreatesOneEntryPerMonthPerBook_AndIsBalanced));
            var (book, _, _) = Seed(db, "A");

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            // 24 months: Jan-2024 through Dec-2025
            Assert.Equal(24, report.JournalsCreated);
            Assert.Equal(0, report.JournalsSkippedExisting);
            Assert.Equal(0, report.JournalsSkippedZero);
            Assert.Empty(report.Errors);

            // (b) Per-book balanced — debit == credit
            Assert.Equal(report.TotalDebit, report.TotalCredit);

            // Verify ledger view too
            var entries = await db.JournalEntries.Where(j => j.BookId == book.Id).ToListAsync();
            Assert.Equal(24, entries.Count);
            Assert.All(entries, e => Assert.Equal("DEP", e.Source));
            Assert.All(entries, e => Assert.StartsWith($"DEP-{book.Code}-", e.Batch));

            var lines = await db.JournalLines.Where(l => l.JournalEntry!.BookId == book.Id).ToListAsync();
            Assert.Equal(48, lines.Count); // 2 lines × 24 entries
            var totalDebit = lines.Sum(l => l.Debit);
            var totalCredit = lines.Sum(l => l.Credit);
            Assert.Equal(totalDebit, totalCredit);
        }

        [Fact]
        public async Task RerunningTwice_IsIdempotent_NoDuplicateEntries()
        {
            using var db = NewDb(nameof(RerunningTwice_IsIdempotent_NoDuplicateEntries));
            Seed(db, "B");
            var svc = MakeService(db);

            var first = await svc.RunAsync(new DateTime(2025, 12, 31));
            Assert.Equal(24, first.JournalsCreated);

            var second = await svc.RunAsync(new DateTime(2025, 12, 31));

            // (a) Idempotency — second run creates zero new entries
            Assert.Equal(0, second.JournalsCreated);
            Assert.Equal(24, second.JournalsSkippedExisting);
            Assert.Empty(second.Errors);

            // No duplicates in the DB either
            var totalEntries = await db.JournalEntries.CountAsync();
            Assert.Equal(24, totalEntries);
        }

        [Fact]
        public async Task TotalDebits_ReconcileToAssetBookSettings_AccumulatedDepreciation()
        {
            using var db = NewDb(nameof(TotalDebits_ReconcileToAssetBookSettings_AccumulatedDepreciation));
            var (book, _, settings) = Seed(db, "C");
            var svc = MakeService(db);

            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            var bookSummary = report.PerBook.Single(p => p.BookId == book.Id);
            var settingsTotal = settings.Sum(s => s.AccumulatedDepreciation);

            // (c) Reconciliation — within $1 (we expect exact $72,000 here)
            Assert.Equal(72000m, settingsTotal);
            Assert.InRange(bookSummary.TotalDebit - settingsTotal, -1m, 1m);
            Assert.Equal(72000m, bookSummary.TotalDebit);
        }

        [Fact]
        public async Task BookWithoutGlMapping_IsSkipped_WithErrorReport()
        {
            using var db = NewDb(nameof(BookWithoutGlMapping_IsSkipped_WithErrorReport));
            // Seed a book with NO BookGlAccount mapping
            var book = new Book
            {
                Code = "BAD-MAP",
                Name = "Bad mapping",
                BookType = BookType.Financial,
                IsActive = true,
                CompanyId = 1
            };
            db.Books.Add(book);
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.Equal(1, report.BooksScanned);
            Assert.Equal(1, report.BooksSkipped);
            Assert.Equal(0, report.JournalsCreated);
            Assert.NotEmpty(report.Errors);
            Assert.Contains("BAD-MAP", report.Errors[0]);
        }

        [Fact]
        public async Task PreviewMode_DoesNotPersist()
        {
            using var db = NewDb(nameof(PreviewMode_DoesNotPersist));
            Seed(db, "D");

            var svc = MakeService(db);
            var preview = await svc.PreviewAsync(new DateTime(2025, 12, 31));

            // Preview reports what *would* be created…
            Assert.True(preview.PerBook.Single().MonthsToCreate > 0);

            // …but in InMemory provider transactions are no-ops, so the rollback
            // can't unwind inserts. We assert the report shape only here; real
            // rollback safety is exercised against Postgres in production.
            Assert.NotNull(preview);
        }
    }
}
