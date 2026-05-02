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
    /// Sweeps every active book and produces one aggregate JournalEntry per (Book, Month)
    /// for every month in [earliest in-service month, asOfDate], including months whose
    /// computed total is zero. Idempotent: any (BookId, Period) that already has a
    /// journal entry is skipped regardless of Source. Period locking is bypassed; the
    /// whole sweep runs in one transaction with all-or-nothing semantics.
    /// </summary>
    public class HistoricJournalBackfillService
    {
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

            if (!await _runGate.WaitAsync(TimeSpan.FromSeconds(2)))
            {
                report.Errors.Add("Another historic-journal backfill is already running. Please wait for it to finish before retrying.");
                report.Aborted = true;
                sw.Stop();
                report.Duration = sw.Elapsed;
                return report;
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var books = await _db.Books
                    .Where(b => b.IsActive)
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
                        await ProcessBookAsync(book, report, summary, dryRun);
                    }
                    catch (Exception bookEx)
                    {
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
                // Zero per-book "created/posted" figures so the displayed report reflects
                // the rolled-back on-disk state.
                foreach (var s in report.PerBook)
                {
                    s.MonthsCreated = 0;
                    s.MonthsToCreate = 0;
                    s.MonthsZero = 0;
                    s.TotalDebit = 0m;
                }
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
            HistoricJournalBookSummary summary,
            bool dryRun)
        {
            // Include Book so AssetBookSettings.EffectiveMethod / EffectiveConvention /
            // EffectiveUsefulLifeMonths can fall back to the Book's values when the
            // per-asset overrides are null.
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

            // Skip a (Book, Period) if ANY journal entry already exists, regardless of
            // Source — this honors hand-made historical journals so we never duplicate.
            var existingPeriods = await _db.JournalEntries
                .Where(j => j.BookId == book.Id)
                .Select(j => j.Period)
                .ToListAsync();
            var existingSet = new HashSet<int>(existingPeriods);

            var firstPeriod = firstMonth.Year * 100 + firstMonth.Month;
            var lastPeriod = lastMonth.Year * 100 + lastMonth.Month;
            summary.MonthsAlreadyPosted = existingSet.Count(p => p >= firstPeriod && p <= lastPeriod);

            // Include previously-posted in-window debits so the per-book "Posted vs
            // settings.Acc" delta reflects ALL depreciation on the books.
            var existingDebitTotal = await _db.JournalLines
                .Where(jl => jl.JournalEntry != null
                          && jl.JournalEntry.BookId == book.Id
                          && jl.JournalEntry.Period >= firstPeriod
                          && jl.JournalEntry.Period <= lastPeriod
                          && jl.Debit > 0m)
                .SumAsync(jl => (decimal?)jl.Debit) ?? 0m;

            var perMonthTotals = ComputePerMonthTotalsForBook(settings, firstMonth, lastMonth);

            int created = 0;
            int zero = 0;
            decimal bookDebitTotal = 0m;

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
                var rounded = Math.Round(total, 2, MidpointRounding.AwayFromZero);

                // In dry-run, do not call the generator — it would Add tracked entities
                // to the DbContext that could leak into a later RunAsync on the same
                // scope. Compute counts/totals only.
                if (!dryRun)
                {
                    await JournalGenerator.GenerateMonthlyWithAmountAsync(
                        _db,
                        book.Id,
                        month,
                        total,
                        createdBy: "system",
                        companyId: book.CompanyId,
                        enforcePeriodLock: false,
                        saveChanges: false);
                }

                if (total == 0m)
                {
                    zero++;
                    report.JournalsZeroAmount++;
                }

                created++;
                bookDebitTotal += rounded;
                report.JournalsCreated++;
                report.TotalDebit += rounded;
                report.TotalCredit += rounded;
            }

            summary.MonthsCreated = created;
            summary.MonthsZero = zero;
            summary.MonthsToCreate = created;
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
