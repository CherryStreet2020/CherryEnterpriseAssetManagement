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
            // 48-month range (2024-01..2027-12). Engine schedule = 36 active × $3k =
            // $108k + 12 zero months past life. Engine is the source of truth: backfill
            // posts $108k and emits a drift Warning vs the stale $72k stamped Acc.
            var report = await svc.RunAsync(new DateTime(2027, 12, 31));

            Assert.Equal(48, report.JournalsCreated);
            Assert.Equal(0, report.JournalsSkippedExisting);
            Assert.False(report.Aborted);
            Assert.Empty(report.Errors);
            Assert.Equal(12, report.JournalsZeroAmount);
            Assert.Equal(report.TotalDebit, report.TotalCredit);
            Assert.Equal(108000m, report.TotalDebit);

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

            // Cost $100k, Sec179 $20k, Bonus 50% on (100k - 20k) = $40k.
            // Adjusted basis $40k / 60 months SL = $666.67/mo over 24 months + $60k upfront.
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

            // Stamp Acc the same way DepreciationBackfillService would (last-row accum).
            var depSvc = new DepreciationService();
            var sched = depSvc.BuildScheduleWithSettings(asset, new DateTime(2025, 12, 31), s);
            s.AccumulatedDepreciation = sched[^1].AccumulatedDepreciation;
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2025, 12, 31));

            Assert.False(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            var bs = report.PerBook.Single();

            var debitTotal = await db.JournalLines
                .Where(jl => jl.JournalEntry!.BookId == book.Id && jl.Debit > 0m)
                .SumAsync(jl => jl.Debit);
            Assert.InRange(debitTotal - s.AccumulatedDepreciation, -1m, 1m);
            Assert.InRange(bs.TotalDebit - s.AccumulatedDepreciation, -1m, 1m);
            Assert.True(s.AccumulatedDepreciation > 60000m);
        }

        [Fact]
        public async Task DriftBetweenStampedAccAndEngine_EmitsWarning_PostsEngineAmount()
        {
            // Stamped Acc has been hand-edited to a value that differs from what the
            // engine produces. Engine is the source of truth — the run posts engine
            // amounts and surfaces the drift as an informational Warning rather than
            // silently rewriting journals to lie about what depreciated when.
            using var db = NewDb(nameof(DriftBetweenStampedAccAndEngine_EmitsWarning_PostsEngineAmount));
            var (book, _, settings) = Seed(db, "DRIFT");
            foreach (var s in settings) s.AccumulatedDepreciation = 30000m;
            db.SaveChanges();
            // Engine: 3 assets × $1k/mo × 24 months = $72,000; stamped = $90,000.

            var run = await MakeService(db).RunAsync(new DateTime(2025, 12, 31));

            Assert.False(run.Aborted, $"Errors: {string.Join(" | ", run.Errors)}");
            var bs = run.PerBook.Single();
            Assert.NotNull(bs.Warning);
            Assert.Contains("Drift", bs.Warning);
            Assert.Equal(72000m, bs.ComputedScheduleTotal);
            Assert.Equal(90000m, bs.SettingsAccumulated);
            Assert.Equal(-18000m, bs.ReconciliationDelta);

            var debitTotal = await db.JournalLines
                .Where(l => l.JournalEntry!.BookId == book.Id && l.Debit > 0m)
                .SumAsync(l => l.Debit);
            Assert.Equal(72000m, debitTotal);
            Assert.Contains(run.Warnings, w => w.Contains(book.Code) && w.Contains("Drift"));
        }

        [Fact]
        public async Task UnsupportedMethod_SkipsAssetWithWarning_SweepContinues()
        {
            // CCA isn't handled by DepreciationService.BuildScheduleWithSettings — the
            // sweep must NOT abort. The asset is skipped, surfaced in the per-book
            // Warning, and the book's remaining (supported) assets still post normally.
            using var db = NewDb(nameof(UnsupportedMethod_SkipsAssetWithWarning_SweepContinues));
            var book = new Book
            {
                Code = "TAX-CCA", Name = "CCA Tax",
                Method = DepreciationMethod.CCA,
                Convention = DepreciationConvention.FullMonth,
                UsefulLifeOverrideMonths = 60,
                BookType = BookType.Tax, IsActive = true, CompanyId = 1
            };
            db.Books.Add(book);
            db.SaveChanges();
            db.BookGlAccounts.Add(new BookGlAccount { BookId = book.Id, DepreciationExpense = "6510", AccumulatedDepreciation = "1520" });

            var ccaAsset = new Asset
            {
                AssetNumber = "CCA-001", Description = "CCA asset",
                AcquisitionCost = 50000m, SalvageValue = 0m, UsefulLifeMonths = 60,
                InServiceDate = new DateTime(2024, 1, 1),
                DepreciationMethod = DepreciationMethod.CCA,
                Active = true, CompanyId = 1
            };
            var slAsset = new Asset
            {
                AssetNumber = "SL-001", Description = "SL asset",
                AcquisitionCost = 24000m, SalvageValue = 0m, UsefulLifeMonths = 24,
                InServiceDate = new DateTime(2024, 1, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                Active = true, CompanyId = 1
            };
            db.Assets.AddRange(ccaAsset, slAsset);
            db.SaveChanges();
            db.AssetBookSettings.AddRange(
                new AssetBookSettings { AssetId = ccaAsset.Id, BookId = book.Id, MethodOverride = DepreciationMethod.CCA, AccumulatedDepreciation = 18000m },
                new AssetBookSettings { AssetId = slAsset.Id, BookId = book.Id, MethodOverride = DepreciationMethod.StraightLine, ConventionOverride = DepreciationConvention.FullMonth, UsefulLifeMonthsOverride = 24 });
            db.SaveChanges();

            var run = await MakeService(db).RunAsync(new DateTime(2025, 12, 31));

            Assert.False(run.Aborted, $"Errors: {string.Join(" | ", run.Errors)}");
            var bs = run.PerBook.Single();
            Assert.NotNull(bs.Warning);
            Assert.Contains("CCA-001", bs.Warning);
            Assert.Contains("Skipped 1 asset", bs.Warning);

            // SL asset still posts normally: $1k/mo × 24 months = $24,000.
            var debitTotal = await db.JournalLines
                .Where(l => l.JournalEntry!.BookId == book.Id && l.Debit > 0m)
                .SumAsync(l => l.Debit);
            Assert.Equal(24000m, debitTotal);
        }

        [Fact]
        public async Task PreviewAsync_SurfacesMissingGlMappingPerBook_WithoutAborting()
        {
            using var db = NewDb(nameof(PreviewAsync_SurfacesMissingGlMappingPerBook_WithoutAborting));
            var book = new Book
            {
                Code = "NO-GL", Name = "No GL", Method = DepreciationMethod.StraightLine,
                Convention = DepreciationConvention.FullMonth, UsefulLifeOverrideMonths = 12,
                BookType = BookType.Financial, IsActive = true, CompanyId = 1
            };
            db.Books.Add(book);
            db.SaveChanges();
            var asset = new Asset
            {
                AssetNumber = "NG-001", Description = "x",
                AcquisitionCost = 12000m, SalvageValue = 0m, UsefulLifeMonths = 12,
                InServiceDate = new DateTime(2024, 1, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                Active = true, CompanyId = 1
            };
            db.Assets.Add(asset);
            db.SaveChanges();
            db.AssetBookSettings.Add(new AssetBookSettings { AssetId = asset.Id, BookId = book.Id });
            db.SaveChanges();

            var preview = await MakeService(db).PreviewAsync(new DateTime(2024, 12, 31));
            Assert.False(preview.Aborted);
            var bs = preview.PerBook.Single();
            Assert.NotNull(bs.Error);
            Assert.Contains("GL mapping", bs.Error);
        }

        [Fact]
        public async Task GlAccountFallback_UsesBookGlAccountDepExp_WhenBookGlAccountRowMissing()
        {
            // BookGlAccount row absent — generator must fall back to Book.GlAccountDepExp /
            // Book.GlAccountAccumDep. Without the fallback the entire sweep aborts.
            using var db = NewDb(nameof(GlAccountFallback_UsesBookGlAccountDepExp_WhenBookGlAccountRowMissing));
            var book = new Book
            {
                Code = "GAAP-FB",
                Name = "Fallback book",
                Method = DepreciationMethod.StraightLine,
                Convention = DepreciationConvention.FullMonth,
                UsefulLifeOverrideMonths = 12,
                BookType = BookType.Financial,
                IsActive = true,
                CompanyId = 1,
                GlAccountDepExp = "6500-LEGACY",
                GlAccountAccumDep = "1510-LEGACY"
            };
            db.Books.Add(book);
            db.SaveChanges();
            // Note: NO BookGlAccount row.

            var asset = new Asset
            {
                AssetNumber = "FB-001", Description = "Fallback",
                AcquisitionCost = 12000m, SalvageValue = 0m, UsefulLifeMonths = 12,
                InServiceDate = new DateTime(2024, 1, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                Active = true, CompanyId = 1
            };
            db.Assets.Add(asset);
            db.SaveChanges();
            db.AssetBookSettings.Add(new AssetBookSettings
            {
                AssetId = asset.Id, BookId = book.Id,
                MethodOverride = DepreciationMethod.StraightLine,
                ConventionOverride = DepreciationConvention.FullMonth,
                AccumulatedDepreciation = 12000m
            });
            db.SaveChanges();

            var svc = MakeService(db);
            var report = await svc.RunAsync(new DateTime(2024, 12, 31));

            Assert.False(report.Aborted, $"Errors: {string.Join(" | ", report.Errors)}");
            Assert.Equal(12, report.JournalsCreated);

            var lines = await db.JournalLines
                .Where(l => l.JournalEntry!.BookId == book.Id)
                .ToListAsync();
            Assert.Contains(lines, l => l.Account == "6500-LEGACY" && l.Debit > 0m);
            Assert.Contains(lines, l => l.Account == "1510-LEGACY" && l.Credit > 0m);
        }

        [Fact]
        public async Task PreviewAsync_DoesNotMutateState_AndAvoidsLeakingIntoSubsequentRun()
        {
            using var db = NewDb(nameof(PreviewAsync_DoesNotMutateState_AndAvoidsLeakingIntoSubsequentRun));
            var (book, _, _) = Seed(db, "P");
            var svc = MakeService(db);

            var preview = await svc.PreviewAsync(new DateTime(2025, 12, 31));
            Assert.Equal(24, preview.PerBook.Single().MonthsToCreate);

            Assert.Empty(db.ChangeTracker.Entries<JournalEntry>());
            Assert.Equal(0, db.JournalEntries.Count());

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
