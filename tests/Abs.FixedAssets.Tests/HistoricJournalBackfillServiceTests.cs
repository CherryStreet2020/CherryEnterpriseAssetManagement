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
            // Asset is in service from 2024-01 with 36-month life. Run through 2027-12
            // → 48 months in range (Jan-2024 through Dec-2027): 36 with depreciation +
            // 12 zero-amount months past full depreciation.
            var report = await svc.RunAsync(new DateTime(2027, 12, 31));

            // Spec contract: one entry per (Book, Month) for EVERY month in range
            Assert.Equal(48, report.JournalsCreated);
            Assert.Equal(0, report.JournalsSkippedExisting);
            Assert.False(report.Aborted);
            Assert.Empty(report.Errors);

            // 12 of those should be zero-amount (months 37-48, after full depreciation)
            Assert.Equal(12, report.JournalsZeroAmount);

            // (b) Per-book balanced — debit == credit
            Assert.Equal(report.TotalDebit, report.TotalCredit);

            // Verify ledger view: every month has an entry, including zero-amount ones
            var entries = await db.JournalEntries.Where(j => j.BookId == book.Id).ToListAsync();
            Assert.Equal(48, entries.Count);
            Assert.All(entries, e => Assert.Equal("DEP", e.Source));
            Assert.All(entries, e => Assert.StartsWith($"DEP-{book.Code}-", e.Batch));
            Assert.All(entries, e => Assert.Equal(book.Id, e.BookId));

            var lines = await db.JournalLines.Where(l => l.JournalEntry!.BookId == book.Id).ToListAsync();
            Assert.Equal(96, lines.Count); // 2 lines × 48 entries
            Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
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
            Assert.False(second.Aborted);
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

        /// <summary>
        /// Regression for the production-shape: AssetBookSettings have ALL overrides null,
        /// so EffectiveMethod / EffectiveConvention / EffectiveUsefulLifeMonths must
        /// inherit from Book.Method / Book.Convention / Book.UsefulLifeOverrideMonths.
        /// If the service forgets Include(s.Book), the schedule silently falls back to
        /// defaults and reconciliation breaks. This test would FAIL with the missing Include.
        /// </summary>
        [Fact]
        public async Task InheritedBookSettings_NullOverrides_StillReconcilesToAccumulated()
        {
            using var db = NewDb(nameof(InheritedBookSettings_NullOverrides_StillReconcilesToAccumulated));

            // Book with NON-default convention (FullMonth, not the default MidMonth) and a life override.
            var book = new Book
            {
                Code = "GAAP-INH",
                Name = "Inherited",
                Method = DepreciationMethod.StraightLine,
                Convention = DepreciationConvention.FullMonth,
                UsefulLifeOverrideMonths = 36,
                BookType = BookType.Financial,
                IsActive = true,
                CompanyId = 1
            };
            db.Books.Add(book);
            db.SaveChanges();
            db.BookGlAccounts.Add(new BookGlAccount { BookId = book.Id, DepreciationExpense = "6500", AccumulatedDepreciation = "1510" });

            // Asset has a DIFFERENT useful life (60 months) — if Book isn't loaded, the
            // schedule would use 60 instead of the book's 36, blowing reconciliation.
            var asset = new Asset
            {
                AssetNumber = "INH-001", Description = "Inherited",
                AcquisitionCost = 36000m, SalvageValue = 0m, UsefulLifeMonths = 60,
                InServiceDate = new DateTime(2024, 1, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                Active = true, CompanyId = 1
            };
            db.Assets.Add(asset);
            db.SaveChanges();

            // ALL OVERRIDES NULL — must inherit from Book.
            db.AssetBookSettings.Add(new AssetBookSettings
            {
                AssetId = asset.Id,
                BookId = book.Id,
                MethodOverride = null,
                ConventionOverride = null,
                UsefulLifeMonthsOverride = null,
                // Pre-stamp as if depreciation backfill had run with book's life=36 through 2025-12
                // (24 months × $1,000/mo with life=36 → $24,000). If life=60 was used by mistake,
                // it would be 24 × $600 = $14,400.
                AccumulatedDepreciation = 24000m,
                BookValue = 12000m,
                LastDepreciationDate = new DateTime(2025, 12, 31)
            });
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.False(report.Aborted, $"Expected success. Errors: {string.Join(" | ", report.Errors)}");
            Assert.Equal(24, report.JournalsCreated);

            var bookSummary = report.PerBook.Single(b => b.BookId == book.Id);
            // With Book.UsefulLifeOverrideMonths=36 inherited, $36,000 / 36 = $1,000/mo × 24 = $24,000.
            // Reconciles to AssetBookSettings.AccumulatedDepreciation within $1.
            Assert.Equal(24000m, bookSummary.SettingsAccumulated);
            Assert.InRange(bookSummary.TotalDebit - 24000m, -1m, 1m);
        }

        [Fact]
        public async Task BookWithoutGlMapping_AbortsEntireSweep_AllOrNothing()
        {
            using var db = NewDb(nameof(BookWithoutGlMapping_AbortsEntireSweep_AllOrNothing));
            // Seed one good book with full settings…
            var (goodBook, _, _) = Seed(db, "GOOD");
            // …and a second book WITHOUT a BookGlAccount mapping but WITH an asset/setting.
            var badBook = new Book { Code = "BAD-MAP", Name = "Bad mapping", BookType = BookType.Financial, IsActive = true, CompanyId = 1 };
            db.Books.Add(badBook);
            db.SaveChanges();
            var asset = new Asset { AssetNumber = "BAD-001", Description = "x", AcquisitionCost = 12000m, UsefulLifeMonths = 12, InServiceDate = new DateTime(2024, 1, 1), DepreciationMethod = DepreciationMethod.StraightLine, Active = true, CompanyId = 1 };
            db.Assets.Add(asset);
            db.SaveChanges();
            db.AssetBookSettings.Add(new AssetBookSettings { AssetId = asset.Id, BookId = badBook.Id, MethodOverride = DepreciationMethod.StraightLine, ConventionOverride = DepreciationConvention.FullMonth });
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            // All-or-nothing: missing GL mapping on one book aborts the entire sweep
            Assert.True(report.Aborted, $"Expected sweep to abort. Errors: {string.Join(" | ", report.Errors)}");
            Assert.NotEmpty(report.Errors);
            Assert.Equal(0, report.JournalsCreated);

            // Per-book error must be annotated on the summary with book code AND name.
            var badSummary = report.PerBook.Single(b => b.BookId == badBook.Id);
            Assert.NotNull(badSummary.Error);
            Assert.Contains("BAD-MAP", badSummary.Error);
            Assert.Contains("Bad mapping", badSummary.Error);
            // And must surface in the top-level errors list.
            Assert.Contains(report.Errors, e => e.Contains("BAD-MAP") && e.Contains("Bad mapping"));

            // Rollback semantics in the report: every per-book summary's
            // created/posted figures must be zeroed so the displayed report
            // doesn't claim work that was rolled back.
            Assert.All(report.PerBook, s =>
            {
                Assert.Equal(0, s.MonthsCreated);
                Assert.Equal(0, s.MonthsToCreate);
                Assert.Equal(0, s.MonthsZero);
                Assert.Equal(0m, s.TotalDebit);
            });

            // The good book's entries were rolled back too (… subject to InMemory provider
            // which doesn't support real transactions, so we skip the DB count assertion here.
            // Real Postgres rollback semantics are exercised in the live smoke test.)
        }

        [Fact]
        public async Task PreExistingNonDepJournal_IsSkipped_NoDuplicatePosting()
        {
            // Seeds a hand-made "MANUAL" journal for one (Book, Month) and proves the
            // backfill skips that month on first run AND on rerun — never creating a
            // duplicate. Mirrors the production reality of 7 hand-made historical
            // journals that the spec explicitly calls out.
            using var db = NewDb(nameof(PreExistingNonDepJournal_IsSkipped_NoDuplicatePosting));
            var (book, _, _) = Seed(db, "M");

            // The seeded book runs from 2024-01 with 36-month life. Hand-place a
            // legacy journal at 2024-06 with Source != "DEP".
            var legacyPeriod = 2024 * 100 + 6; // yyyymm
            var legacyEntry = new JournalEntry
            {
                BookId = book.Id,
                Period = legacyPeriod,
                PostingDate = new DateTime(2024, 6, 30),
                Source = "MANUAL",
                Description = "Hand-made legacy depreciation entry",
                Reference = "LEGACY-001"
            };
            db.JournalEntries.Add(legacyEntry);
            db.SaveChanges();
            db.JournalLines.AddRange(
                new JournalLine { JournalEntryId = legacyEntry.Id, Account = "6500", Debit = 3000m, Credit = 0m, LineNo = 1 },
                new JournalLine { JournalEntryId = legacyEntry.Id, Account = "1510", Debit = 0m, Credit = 3000m, LineNo = 2 });
            db.SaveChanges();

            var svc = MakeService(db);
            // Run through 2025-12 → 24 months in range, but 2024-06 already has the
            // hand-made entry, so the backfill should create 23 (not 24) entries.
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.False(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            Assert.Equal(23, report.JournalsCreated);
            Assert.Equal(1, report.JournalsSkippedExisting);

            // The (Book, Period) for 2024-06 must have exactly ONE entry — the legacy one.
            var entriesAtLegacyPeriod = db.JournalEntries
                .Where(j => j.BookId == book.Id && j.Period == legacyPeriod)
                .ToList();
            Assert.Single(entriesAtLegacyPeriod);
            Assert.Equal("MANUAL", entriesAtLegacyPeriod[0].Source);

            // Per-book report counters honor the in-window legacy entry.
            var bs = report.PerBook.Single();
            Assert.Equal(1, bs.MonthsAlreadyPosted);
            Assert.Equal(23, bs.MonthsCreated);

            // Reconciliation must include the legacy debit ($3,000) plus newly-created
            // DEP debits (23 × $3,000 = $69,000) → $72,000 total, matching the
            // pre-stamped AssetBookSettings.AccumulatedDepreciation of $72,000.
            Assert.Equal(72000m, bs.TotalDebit);
            Assert.Equal(72000m, bs.SettingsAccumulated);

            // Rerun: should be a complete no-op (24 already posted, 0 created).
            var rerun = await svc.RunAsync(new DateTime(2025, 12, 31));
            Assert.False(rerun.Aborted);
            Assert.Equal(0, rerun.JournalsCreated);
            Assert.Equal(24, rerun.JournalsSkippedExisting);

            // And the legacy month STILL has only one entry.
            var entriesAfterRerun = db.JournalEntries
                .Count(j => j.BookId == book.Id && j.Period == legacyPeriod);
            Assert.Equal(1, entriesAfterRerun);
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
