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
    public class HistoricJournalBackfillServiceTests
    {
        // InMemory provider can't map JsonDocument (LookupValue.Metadata is jsonb).
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

            // 3 assets × $36,000 / 36-month SL = $1,000/mo each, $3,000/mo aggregate.
            // After 24 months (through 2025-12), per-asset Acc = $24,000, total = $72,000.
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
            // 48-month range (2024-01..2027-12) = 36 active + 12 zero-amount months.
            var report = await svc.RunAsync(new DateTime(2027, 12, 31));

            Assert.Equal(48, report.JournalsCreated);
            Assert.Equal(0, report.JournalsSkippedExisting);
            Assert.False(report.Aborted);
            Assert.Empty(report.Errors);
            Assert.Equal(12, report.JournalsZeroAmount);
            Assert.Equal(report.TotalDebit, report.TotalCredit);

            var entries = await db.JournalEntries.Where(j => j.BookId == book.Id).ToListAsync();
            Assert.Equal(48, entries.Count);
            Assert.All(entries, e => Assert.Equal("DEP", e.Source));
            Assert.All(entries, e => Assert.StartsWith($"DEP-{book.Code}-", e.Batch));
            Assert.All(entries, e => Assert.Equal(book.Id, e.BookId));

            var lines = await db.JournalLines.Where(l => l.JournalEntry!.BookId == book.Id).ToListAsync();
            Assert.Equal(96, lines.Count);
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
            Assert.Equal(0, second.JournalsCreated);
            Assert.Equal(24, second.JournalsSkippedExisting);
            Assert.False(second.Aborted);
            Assert.Empty(second.Errors);

            Assert.Equal(24, await db.JournalEntries.CountAsync());
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

            Assert.Equal(72000m, settingsTotal);
            Assert.InRange(bookSummary.TotalDebit - settingsTotal, -1m, 1m);
            Assert.Equal(72000m, bookSummary.TotalDebit);
        }

        // Regression: AssetBookSettings with all overrides null must inherit from Book.
        [Fact]
        public async Task InheritedBookSettings_NullOverrides_StillReconcilesToAccumulated()
        {
            using var db = NewDb(nameof(InheritedBookSettings_NullOverrides_StillReconcilesToAccumulated));

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

            // Asset has UsefulLifeMonths=60 but the book overrides to 36 — the inherited
            // book value must win (without Include(s.Book) the asset's 60 would be used).
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

            db.AssetBookSettings.Add(new AssetBookSettings
            {
                AssetId = asset.Id,
                BookId = book.Id,
                MethodOverride = null,
                ConventionOverride = null,
                UsefulLifeMonthsOverride = null,
                AccumulatedDepreciation = 24000m,
                BookValue = 12000m,
                LastDepreciationDate = new DateTime(2025, 12, 31)
            });
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.False(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            Assert.Equal(24, report.JournalsCreated);

            var bookSummary = report.PerBook.Single(b => b.BookId == book.Id);
            Assert.Equal(24000m, bookSummary.SettingsAccumulated);
            Assert.InRange(bookSummary.TotalDebit - 24000m, -1m, 1m);
        }

        [Fact]
        public async Task BookWithoutGlMapping_AbortsEntireSweep_AllOrNothing()
        {
            using var db = NewDb(nameof(BookWithoutGlMapping_AbortsEntireSweep_AllOrNothing));
            var (goodBook, _, _) = Seed(db, "GOOD");
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

            Assert.True(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            Assert.NotEmpty(report.Errors);
            Assert.Equal(0, report.JournalsCreated);

            var badSummary = report.PerBook.Single(b => b.BookId == badBook.Id);
            Assert.NotNull(badSummary.Error);
            Assert.Contains("BAD-MAP", badSummary.Error);
            Assert.Contains("Bad mapping", badSummary.Error);
            Assert.Contains(report.Errors, e => e.Contains("BAD-MAP") && e.Contains("Bad mapping"));

            Assert.All(report.PerBook, s =>
            {
                Assert.Equal(0, s.MonthsCreated);
                Assert.Equal(0, s.MonthsToCreate);
                Assert.Equal(0, s.MonthsZero);
                Assert.Equal(0m, s.TotalDebit);
            });
        }

        [Fact]
        public async Task NonFinancialBook_IsAlsoProcessed()
        {
            // Active books of any BookType (Tax, Other) must also be swept so every
            // dollar stamped on AssetBookSettings.AccumulatedDepreciation reconciles.
            using var db = NewDb(nameof(NonFinancialBook_IsAlsoProcessed));
            var taxBook = new Book
            {
                Code = "TAX-1",
                Name = "Tax book",
                Method = DepreciationMethod.StraightLine,
                Convention = DepreciationConvention.FullMonth,
                BookType = BookType.Tax,
                IsActive = true,
                CompanyId = 1
            };
            db.Books.Add(taxBook);
            db.SaveChanges();
            db.BookGlAccounts.Add(new BookGlAccount { BookId = taxBook.Id, DepreciationExpense = "6500", AccumulatedDepreciation = "1510" });
            var asset = new Asset
            {
                AssetNumber = "TAX-001", Description = "Tax asset",
                AcquisitionCost = 12000m, SalvageValue = 0m, UsefulLifeMonths = 12,
                InServiceDate = new DateTime(2024, 1, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                Active = true, CompanyId = 1
            };
            db.Assets.Add(asset);
            db.SaveChanges();
            db.AssetBookSettings.Add(new AssetBookSettings
            {
                AssetId = asset.Id,
                BookId = taxBook.Id,
                MethodOverride = DepreciationMethod.StraightLine,
                ConventionOverride = DepreciationConvention.FullMonth,
                AccumulatedDepreciation = 12000m,
                BookValue = 0m,
                LastDepreciationDate = new DateTime(2024, 12, 31)
            });
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.False(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            Assert.Single(report.PerBook);
            Assert.Equal(taxBook.Id, report.PerBook[0].BookId);
            Assert.Equal(24, report.JournalsCreated);
            Assert.Equal(12, report.JournalsZeroAmount);
            var summary = report.PerBook.Single();
            Assert.Equal(12000m, summary.SettingsAccumulated);
            Assert.InRange(summary.TotalDebit - 12000m, -1m, 1m);
        }

        [Fact]
        public async Task PreExistingNonDepJournal_IsSkipped_NoDuplicatePosting()
        {
            using var db = NewDb(nameof(PreExistingNonDepJournal_IsSkipped_NoDuplicatePosting));
            var (book, _, _) = Seed(db, "M");

            var legacyPeriod = 2024 * 100 + 6;
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
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.False(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            Assert.Equal(23, report.JournalsCreated);
            Assert.Equal(1, report.JournalsSkippedExisting);

            var entriesAtLegacyPeriod = db.JournalEntries
                .Where(j => j.BookId == book.Id && j.Period == legacyPeriod)
                .ToList();
            Assert.Single(entriesAtLegacyPeriod);
            Assert.Equal("MANUAL", entriesAtLegacyPeriod[0].Source);

            var bs = report.PerBook.Single();
            Assert.Equal(1, bs.MonthsAlreadyPosted);
            Assert.Equal(23, bs.MonthsCreated);
            // Legacy $3,000 + 23 × $3,000 newly-created = $72,000 → matches Acc.
            Assert.Equal(72000m, bs.TotalDebit);
            Assert.Equal(72000m, bs.SettingsAccumulated);

            var rerun = await svc.RunAsync(new DateTime(2025, 12, 31));
            Assert.False(rerun.Aborted);
            Assert.Equal(0, rerun.JournalsCreated);
            Assert.Equal(24, rerun.JournalsSkippedExisting);
            Assert.Equal(1, db.JournalEntries.Count(j => j.BookId == book.Id && j.Period == legacyPeriod));
        }

        [Fact]
        public async Task Section179AndBonus_UpfrontAmounts_ReconcileToAccumulated()
        {
            // Sec179 + Bonus depreciation are baked into the schedule's accum but are NOT
            // emitted as row.DepreciationAmount. The backfill must fold those upfront
            // amounts into the asset's first in-service month so SUM(journals) equals
            // AssetBookSettings.AccumulatedDepreciation per book.
            using var db = NewDb(nameof(Section179AndBonus_UpfrontAmounts_ReconcileToAccumulated));
            var book = new Book
            {
                Code = "TAX-179",
                Name = "Tax with bonus",
                Method = DepreciationMethod.StraightLine,
                Convention = DepreciationConvention.FullMonth,
                UsefulLifeOverrideMonths = 60,
                BookType = BookType.Tax,
                IsActive = true,
                CompanyId = 1
            };
            db.Books.Add(book);
            db.SaveChanges();
            db.BookGlAccounts.Add(new BookGlAccount { BookId = book.Id, DepreciationExpense = "6500", AccumulatedDepreciation = "1510" });

            // Cost $100,000. Sec179 = $20,000. Bonus = 50% of (100k - 20k) = $40,000.
            // Adjusted cost = $40,000 over 60 months SL = $666.67/mo.
            // Through 2025-12 (24 months from 2024-01) → 24 × $666.67 + $20,000 + $40,000.
            var asset = new Asset
            {
                AssetNumber = "S179-001", Description = "Bonus eligible",
                AcquisitionCost = 100000m, SalvageValue = 0m, UsefulLifeMonths = 60,
                InServiceDate = new DateTime(2024, 1, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                Active = true, CompanyId = 1
            };
            db.Assets.Add(asset);
            db.SaveChanges();

            var s = new AssetBookSettings
            {
                AssetId = asset.Id,
                BookId = book.Id,
                MethodOverride = DepreciationMethod.StraightLine,
                ConventionOverride = DepreciationConvention.FullMonth,
                Section179Deduction = 20000m,
                BonusDepreciationPercent = 50m,
            };
            db.AssetBookSettings.Add(s);
            db.SaveChanges();

            // Stamp AccumulatedDepreciation the same way DepreciationBackfillService would
            // — by taking the LAST row's accum from the engine through 2025-12.
            var depSvc = new DepreciationService();
            var sched = depSvc.BuildScheduleWithSettings(asset, new DateTime(2025, 12, 31), s);
            s.AccumulatedDepreciation = sched[^1].AccumulatedDepreciation;
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.False(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            var bs = report.PerBook.Single();

            // Persisted journal-line debits per book must match AccumulatedDepreciation.
            var debitTotal = await db.JournalLines
                .Where(jl => jl.JournalEntry!.BookId == book.Id && jl.Debit > 0m)
                .SumAsync(jl => jl.Debit);
            Assert.InRange(debitTotal - s.AccumulatedDepreciation, -1m, 1m);
            Assert.InRange(bs.TotalDebit - s.AccumulatedDepreciation, -1m, 1m);
            // Sanity: $60k upfront should be > 0 — proves the test is not trivially passing
            // when upfront amounts are silently dropped.
            Assert.True(s.AccumulatedDepreciation > 60000m);
        }

        [Fact]
        public async Task PreviewAsync_DoesNotMutateState_AndAvoidsLeakingIntoSubsequentRun()
        {
            // Preview must not Add tracked entities to the DbContext; otherwise a
            // later RunAsync on the same scope could persist preview-created rows.
            using var db = NewDb(nameof(PreviewAsync_DoesNotMutateState_AndAvoidsLeakingIntoSubsequentRun));
            var (book, _, _) = Seed(db, "P");
            var svc = MakeService(db);

            var preview = await svc.PreviewAsync(new DateTime(2025, 12, 31));
            Assert.Equal(24, preview.PerBook.Single().MonthsToCreate);

            // No JournalEntry should have been added to the change tracker.
            Assert.Equal(0, db.ChangeTracker.Entries<JournalEntry>().Count());
            Assert.Equal(0, db.JournalEntries.Count());

            // Subsequent RunAsync on the same context creates exactly the expected count.
            var run = await svc.RunAsync(new DateTime(2025, 12, 31));
            Assert.False(run.Aborted);
            Assert.Equal(24, run.JournalsCreated);
            Assert.Equal(24, db.JournalEntries.Count(j => j.BookId == book.Id));
        }

        [Fact]
        public async Task PreviewMode_DoesNotPersist()
        {
            using var db = NewDb(nameof(PreviewMode_DoesNotPersist));
            Seed(db, "D");

            var svc = MakeService(db);
            var preview = await svc.PreviewAsync(new DateTime(2025, 12, 31));

            Assert.True(preview.PerBook.Single().MonthsToCreate > 0);
            Assert.NotNull(preview);
        }
    }
}
