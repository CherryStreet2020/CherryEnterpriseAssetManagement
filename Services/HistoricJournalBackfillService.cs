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
        public int JournalsSkippedZero { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<HistoricJournalBookSummary> PerBook { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
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
        public int MonthsSkippedZero { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal SettingsAccumulated { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Sweeps every active Financial Book and produces one aggregate JournalEntry per (Book, Month)
    /// from the book's earliest in-service month through a chosen as-of date. Idempotent:
    /// existing entries (matched by BookId + Period yyyymm + Source="DEP") are skipped.
    ///
    /// Per-month totals are derived by aggregating <see cref="DepreciationService.BuildScheduleWithSettings"/>
    /// across every <see cref="AssetBookSettings"/> for the book — using the same engine that
    /// <see cref="DepreciationBackfillService"/> uses to stamp Asset.AccumulatedDepreciation. This
    /// guarantees that SUM(JournalLines.Debit) per book reconciles exactly to
    /// SUM(AssetBookSettings.AccumulatedDepreciation) for that book within rounding.
    ///
    /// Period locking is intentionally bypassed — this is a one-time historical load that must
    /// post into closed periods. The whole sweep is wrapped in a single DB transaction.
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
                sw.Stop();
                report.Duration = sw.Elapsed;
                return report;
            }

            // Single transaction so a partial run never leaves the GL half-posted.
            // For dryRun we still open one and roll back at the end.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var books = await _db.Books
                    .Where(b => b.IsActive && b.BookType == BookType.Financial)
                    .OrderBy(b => b.Id)
                    .ToListAsync();

                var glMappings = await _db.BookGlAccounts.ToDictionaryAsync(g => g.BookId);

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

                    if (!glMappings.TryGetValue(book.Id, out var map) ||
                        string.IsNullOrWhiteSpace(map.DepreciationExpense) ||
                        string.IsNullOrWhiteSpace(map.AccumulatedDepreciation))
                    {
                        var msg = $"Book {book.Code} (id {book.Id}): missing BookGlAccount mapping or required GL accounts (DepreciationExpense / AccumulatedDepreciation). Fill these in via Books → GL Accounts before backfilling.";
                        summary.Error = msg;
                        report.Errors.Add(msg);
                        report.BooksSkipped++;
                        continue;
                    }

                    var settings = await _db.AssetBookSettings
                        .Include(s => s.Asset)
                        .Where(s => s.BookId == book.Id && !s.IsExcludedFromBook && s.Asset != null)
                        .ToListAsync();

                    if (settings.Count == 0)
                    {
                        summary.MonthsInRange = 0;
                        continue;
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
                        continue;
                    }

                    var firstMonth = MonthStart(earliestInService);
                    var lastMonth = MonthStart(report.AsOfDate);
                    summary.EarliestMonth = firstMonth;
                    summary.LatestMonth = lastMonth;
                    summary.MonthsInRange = MonthsBetween(firstMonth, lastMonth);

                    var existingPeriods = await _db.JournalEntries
                        .Where(j => j.BookId == book.Id && j.Source == "DEP")
                        .Select(j => j.Period)
                        .ToListAsync();
                    var existingSet = new HashSet<int>(existingPeriods);
                    summary.MonthsAlreadyPosted = existingSet.Count;

                    var perMonthTotals = ComputePerMonthTotalsForBook(settings, firstMonth, lastMonth);

                    int created = 0;
                    int skippedZero = 0;
                    decimal bookDebitTotal = 0m;

                    foreach (var (month, total) in perMonthTotals)
                    {
                        report.MonthsConsidered++;
                        var period = month.Year * 100 + month.Month;
                        if (existingSet.Contains(period))
                        {
                            report.JournalsSkippedExisting++;
                            continue;
                        }

                        if (total <= 0m)
                        {
                            report.JournalsSkippedZero++;
                            skippedZero++;
                            continue;
                        }

                        var rounded = Math.Round(total, 2, MidpointRounding.AwayFromZero);
                        var posting = MonthEnd(month);

                        var entry = new JournalEntry
                        {
                            BookId = book.Id,
                            Period = period,
                            Batch = $"DEP-{book.Code}-{month:yyyyMM}",
                            Reference = $"DEP {book.Code} {month:yyyy-MM}",
                            Source = "DEP",
                            Description = $"Historical monthly depreciation — {book.Name} {month:yyyy-MM}",
                            PostingDate = posting,
                            CreatedUtc = DateTime.UtcNow,
                            Lines = new List<JournalLine>
                            {
                                new JournalLine
                                {
                                    LineNo = 1,
                                    Account = map.DepreciationExpense!,
                                    Description = "Depreciation expense (historical backfill)",
                                    Debit = rounded,
                                    Credit = 0m
                                },
                                new JournalLine
                                {
                                    LineNo = 2,
                                    Account = map.AccumulatedDepreciation!,
                                    Description = "Accumulated depreciation (historical backfill)",
                                    Debit = 0m,
                                    Credit = rounded
                                }
                            }
                        };

                        _db.JournalEntries.Add(entry);
                        created++;
                        bookDebitTotal += rounded;
                        report.JournalsCreated++;
                        report.TotalDebit += rounded;
                        report.TotalCredit += rounded;
                    }

                    summary.MonthsCreated = created;
                    summary.MonthsSkippedZero = skippedZero;
                    summary.MonthsToCreate = created;
                    summary.TotalDebit = bookDebitTotal;
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
                _logger.LogError(ex, "Historic journal backfill failed; rolling back");
                report.Errors.Add($"FATAL: {ex.GetType().Name}: {ex.Message}");
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

            // Return ordered by month so the loop walks chronologically.
            return totals
                .OrderBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
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
