using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services
{
    public class HistoricJournalBackfillReport
    {
        public DateTime AsOfDate { get; set; }
        public int BooksScanned { get; set; }
        public int BooksSkipped { get; set; }
        public int MonthsConsidered { get; set; }
        public int JournalsCreated { get; set; }
        public int JournalsSkippedExisting { get; set; }
        public int JournalsZeroAmount { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<HistoricJournalBookSummary> PerBook { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public bool Aborted { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class HistoricJournalBookSummary
    {
        public int BookId { get; set; }
        public string BookCode { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
        public DateTime? EarliestMonth { get; set; }
        public DateTime? LatestMonth { get; set; }
        public int MonthsInRange { get; set; }
        public int MonthsAlreadyPosted { get; set; }
        public int MonthsToCreate { get; set; }
        public int MonthsCreated { get; set; }
        public int MonthsZero { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal SettingsAccumulated { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Sweeps every active Financial Book and produces one aggregate JournalEntry per
    /// (Book, Month) — one entry for EVERY month in [earliest in-service month, asOfDate],
    /// even if the computed total is zero. Idempotent: existing entries (matched by
    /// BookId + Period yyyymm + Source="DEP") are skipped.
    ///
    /// Per-month totals are derived by aggregating <see cref="DepreciationService.BuildScheduleWithSettings"/>
    /// across every <see cref="AssetBookSettings"/> for the book — using the same engine that
    /// <see cref="DepreciationBackfillService"/> uses to stamp Asset.AccumulatedDepreciation. This
    /// guarantees that SUM(JournalLines.Debit) per book reconciles exactly to
    /// SUM(AssetBookSettings.AccumulatedDepreciation) for that book within rounding.
    ///
    /// The pre-aggregated total is then handed to
    /// <see cref="JournalGenerator.GenerateMonthlyWithAmountAsync"/> so the entry/line construction
    /// (header naming, GL mapping lookup, period-lock handling) goes through the shared
    /// generator — keeping behavior consistent with the on-demand /Pages/Journals flow.
    ///
    /// Period locking is intentionally bypassed (enforcePeriodLock: false) — this is a
    /// one-time historical load that must post into closed periods. The whole sweep is
    /// wrapped in a single DB transaction. Any per-book error throws and the entire
    /// transaction rolls back — this is an all-or-nothing operation.
    /// </summary>
    public class HistoricJournalBackfillService
    {
        // App-wide gate: only one historic-journal sweep may run at a time.
        // Two admins clicking "Run" simultaneously would otherwise both pass the
        // existing-period check before either committed, queueing duplicate journals.
        private static readonly System.Threading.SemaphoreSlim _runGate = new(1, 1);

        private readonly AppDbContext _db;
        private readonly DepreciationService _depService;
        private readonly ILogger<HistoricJournalBackfillService> _logger;

        public HistoricJournalBackfillService(
            AppDbContext db,
            DepreciationService depService,
            ILogger<HistoricJournalBackfillService> logger)
        {
            _db = db;
            _depService = depService;
            _logger = logger;
        }

        public async Task<HistoricJournalBackfillReport> PreviewAsync(DateTime? asOfDate = null)
        {
            return await RunInternalAsync(asOfDate, dryRun: true);
        }

        public async Task<HistoricJournalBackfillReport> RunAsync(DateTime? asOfDate = null)
        {
            return await RunInternalAsync(asOfDate, dryRun: false);
        }

        private async Task<HistoricJournalBackfillReport> RunInternalAsync(DateTime? asOfDate, bool dryRun)
        {
            var report = new HistoricJournalBackfillReport
            {
                AsOfDate = MonthEnd(asOfDate ?? PreviousMonthEnd(DateTime.UtcNow.Date))
            };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Refuse concurrent runs — two admins racing would each pass the existing-period
            // check independently and queue duplicate journals before either commits.
            if (!await _runGate.WaitAsync(TimeSpan.FromSeconds(2)))
            {
                report.Errors.Add("Another historic-journal backfill is already running. Please wait for it to finish before retrying.");
                report.Aborted = true;
                sw.Stop();
                report.Duration = sw.Elapsed;
                return report;
            }

            // Single transaction — all-or-nothing. Any unhandled error rolls back the entire sweep.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var books = await _db.Books
                    .Where(b => b.IsActive && b.BookType == BookType.Financial)
                    .OrderBy(b => b.Id)
                    .ToListAsync();

                foreach (var book in books)
                {
                    var summary = new HistoricJournalBookSummary
                    {
                        BookId = book.Id,
                        BookCode = book.Code,
                        BookName = book.Name,
                        CompanyId = book.CompanyId
                    };
                    report.PerBook.Add(summary);
                    report.BooksScanned++;

                    try
                    {
                        await ProcessBookAsync(book, report, summary);
                    }
                    catch (Exception bookEx)
                    {
                        // Annotate the per-book summary with a clear, named error and re-throw
                        // with the book name embedded so the outer catch (and UI) gets a loud
                        // "Book GAAP-PWH (3): ..." message instead of a generic exception.
                        var prefix = $"Book {book.Code} ({book.Id}) — {book.Name}";
                        summary.Error = $"{prefix}: {bookEx.Message}";
                        report.Errors.Add(summary.Error);
                        throw new InvalidOperationException($"{prefix}: {bookEx.Message}", bookEx);
                    }
                }

                if (dryRun)
                {
                    await tx.RollbackAsync();
                }
                else
                {
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Historic journal backfill failed; rolling back entire sweep");
                report.Errors.Add($"FATAL — entire sweep rolled back: {ex.GetType().Name}: {ex.Message}");
                report.Aborted = true;
                report.JournalsCreated = 0;
                report.TotalDebit = 0m;
                report.TotalCredit = 0m;
                try { await tx.RollbackAsync(); } catch { /* swallow */ }
            }
            finally
            {
                _runGate.Release();
            }

            sw.Stop();
            report.Duration = sw.Elapsed;
            return report;
        }

        private async Task ProcessBookAsync(
            Book book,
            HistoricJournalBackfillReport report,
            HistoricJournalBookSummary summary)
        {
            // Book MUST be Included — DepreciationService.BuildScheduleWithSettings reads
            // AssetBookSettings.EffectiveMethod / EffectiveConvention / EffectiveUsefulLifeMonths,
            // which fall back to Book.Method / Book.Convention / Book.UsefulLifeOverrideMonths
            // when the per-asset overrides are null. Without Include(s.Book) the inherited values
            // silently drop to defaults (StraightLine / MidMonth) and reconciliation breaks.
            var settings = await _db.AssetBookSettings
                .Include(s => s.Asset)
                .Include(s => s.Book)
                .Where(s => s.BookId == book.Id && !s.IsExcludedFromBook && s.Asset != null)
                .ToListAsync();

            if (settings.Count == 0)
            {
                summary.MonthsInRange = 0;
                return;
            }

            summary.SettingsAccumulated = settings.Sum(s => s.AccumulatedDepreciation);

            var earliestInService = settings
                .Where(s => s.Asset != null && s.Asset.AcquisitionCost > 0 && s.Asset.UsefulLifeMonths > 0)
                .Select(s => s.EffectiveInServiceDate)
                .DefaultIfEmpty(DateTime.MinValue)
                .Min();

            if (earliestInService == DateTime.MinValue || earliestInService > report.AsOfDate)
            {
                summary.MonthsInRange = 0;
                return;
            }

            var firstMonth = MonthStart(earliestInService);
            var lastMonth = MonthStart(report.AsOfDate);
            summary.EarliestMonth = firstMonth;
            summary.LatestMonth = lastMonth;
            summary.MonthsInRange = MonthsBetween(firstMonth, lastMonth);

            // Idempotency: every existing DEP entry for this book by Period yyyymm.
            var existingPeriods = await _db.JournalEntries
                .Where(j => j.BookId == book.Id && j.Source == "DEP")
                .Select(j => j.Period)
                .ToListAsync();
            var existingSet = new HashSet<int>(existingPeriods);

            // For per-book reporting, only count existing periods that fall inside the
            // sweep window [firstMonth, lastMonth] — otherwise an old DEP entry outside
            // the window would inflate the "already posted" count and confuse operators.
            var firstPeriod = firstMonth.Year * 100 + firstMonth.Month;
            var lastPeriod = lastMonth.Year * 100 + lastMonth.Month;
            summary.MonthsAlreadyPosted = existingSet.Count(p => p >= firstPeriod && p <= lastPeriod);

            // Reconciliation: include debits from PREVIOUSLY-posted DEP entries within the
            // sweep window so the per-book "Posted vs AssetBookSettings.Acc" comparison is
            // accurate even when a book was partially pre-seeded by an earlier sweep.
            var existingDebitTotal = await _db.JournalLines
                .Where(jl => jl.JournalEntry != null
                          && jl.JournalEntry.BookId == book.Id
                          && jl.JournalEntry.Source == "DEP"
                          && jl.JournalEntry.Period >= firstPeriod
                          && jl.JournalEntry.Period <= lastPeriod
                          && jl.Debit > 0m)
                .SumAsync(jl => (decimal?)jl.Debit) ?? 0m;

            // Aggregate per-asset schedules into per-month totals using the same
            // engine the Depreciation Backfill used. Months not in the dictionary
            // (e.g. before earliest in-service) get treated as zero below.
            var perMonthTotals = ComputePerMonthTotalsForBook(settings, firstMonth, lastMonth);

            int created = 0;
            int zero = 0;
            decimal bookDebitTotal = 0m;

            // Walk EVERY month in range; create an entry even if the amount is zero,
            // unless the (Book, Period) was already posted.
            for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
            {
                report.MonthsConsidered++;
                var period = month.Year * 100 + month.Month;
                if (existingSet.Contains(period))
                {
                    report.JournalsSkippedExisting++;
                    continue;
                }

                perMonthTotals.TryGetValue(month, out var total);
                if (total < 0m) total = 0m;

                // Hand the precomputed total to the shared JournalGenerator. saveChanges:false
                // because we have a single SaveChanges at the end of the outer transaction.
                // enforcePeriodLock:false — this is a historical load that posts into closed periods.
                // Any failure here (missing GL mapping, etc.) propagates up and the entire sweep
                // rolls back via the catch in RunInternalAsync — all-or-nothing.
                await JournalGenerator.GenerateMonthlyWithAmountAsync(
                    _db,
                    book.Id,
                    month,
                    total,
                    createdBy: "system",
                    companyId: book.CompanyId,
                    enforcePeriodLock: false,
                    saveChanges: false);

                if (total == 0m)
                {
                    zero++;
                    report.JournalsZeroAmount++;
                }

                created++;
                bookDebitTotal += Math.Round(total, 2, MidpointRounding.AwayFromZero);
                report.JournalsCreated++;
                report.TotalDebit += Math.Round(total, 2, MidpointRounding.AwayFromZero);
                report.TotalCredit += Math.Round(total, 2, MidpointRounding.AwayFromZero);
            }

            summary.MonthsCreated = created;
            summary.MonthsZero = zero;
            summary.MonthsToCreate = created;
            // Reconciliation total = newly-created debits + previously-posted in-window debits.
            summary.TotalDebit = bookDebitTotal + existingDebitTotal;
        }

        // Aggregates per-asset DepreciationService schedules into per-month totals for the book.
        private Dictionary<DateTime, decimal> ComputePerMonthTotalsForBook(
            List<AssetBookSettings> settings,
            DateTime firstMonth,
            DateTime lastMonth)
        {
            var totals = new Dictionary<DateTime, decimal>();

            foreach (var s in settings)
            {
                if (s.Asset == null) continue;
                if (s.Asset.AcquisitionCost <= 0 || s.Asset.UsefulLifeMonths <= 0) continue;

                var schedule = _depService.BuildScheduleWithSettings(s.Asset, lastMonth, s);
                if (schedule.Count == 0) continue;

                foreach (var row in schedule)
                {
                    var key = MonthStart(row.PeriodStart);
                    if (key < firstMonth || key > lastMonth) continue;
                    if (!totals.ContainsKey(key)) totals[key] = 0m;
                    totals[key] += row.DepreciationAmount;
                }
            }

            return totals;
        }

        // ──────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────
        private static DateTime MonthStart(DateTime dt) => new DateTime(dt.Year, dt.Month, 1);
        private static DateTime MonthEnd(DateTime dt) => new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month));
        private static DateTime PreviousMonthEnd(DateTime today)
        {
            var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
            return firstOfThisMonth.AddDays(-1);
        }
        private static int MonthsBetween(DateTime a, DateTime b)
        {
            var start = MonthStart(a);
            var end = MonthStart(b);
            if (end < start) return 0;
            return (end.Year - start.Year) * 12 + (end.Month - start.Month) + 1;
        }
    }
}
