using System;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public static class JournalGenerator
    {
        /// <summary>
        /// Public, on-demand monthly depreciation entry. Computes the total
        /// internally via <see cref="DepreciationService.CalculateMonthlyDepreciation"/>
        /// per asset, summed over the book's company scope. Method-aware
        /// (MACRS / DDB / 150DB / SYD / SL all honored — see PR #102 B-11
        /// for the reflection-bug history). Used by /Pages/Journals (manual
        /// one-shot generation).
        /// </summary>
        public static async Task<JournalEntry> GenerateMonthlyAsync(
            AppDbContext db,
            int bookId,
            DateTime month,
            string createdBy = "system",
            int? companyId = null,
            bool enforcePeriodLock = true)
        {
            var period = new DateTime(month.Year, month.Month, 1);

            // PR #102 (B-11): compute the monthly total via the canonical
            // DepreciationService.CalculateMonthlyDepreciation. The previous
            // path reflected for a method named CalculateMonthly or Calculate;
            // the real method is CalculateMonthlyDepreciation, so the
            // reflection silently returned null and FallbackStraightLineMonthly
            // ran for every asset — stripping MACRS / DDB / SYD acceleration
            // from the monthly GL aggregate. Per-asset snapshots stayed
            // correct; the JE that hit the books did not. This direct call
            // honors asset.DepreciationMethod end-to-end.
            decimal totalMonthly = await ComputeMethodAwareMonthlyTotalAsync(db, bookId, period, companyId);

            return await GenerateMonthlyWithAmountAsync(
                db, bookId, month, totalMonthly,
                createdBy: createdBy,
                companyId: companyId,
                enforcePeriodLock: enforcePeriodLock,
                saveChanges: true);
        }

        /// <summary>
        /// Bulk overload that <see cref="GenerateMonthlyAsync"/> delegates to. Caller
        /// supplies the precomputed monthly total. Set <paramref name="saveChanges"/> to
        /// false when running inside a larger transaction.
        /// </summary>
        public static async Task<JournalEntry> GenerateMonthlyWithAmountAsync(
            AppDbContext db,
            int bookId,
            DateTime month,
            decimal monthlyTotal,
            string createdBy = "system",
            int? companyId = null,
            bool enforcePeriodLock = true,
            bool saveChanges = true)
        {
            var period = new DateTime(month.Year, month.Month, 1);
            var posting = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));

            var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId)
                       ?? throw new InvalidOperationException("Book not found.");

            // Period-locking: prevent posting depreciation into closed/locked periods (skip during historical backfill).
            var lockCompanyId = companyId ?? book.CompanyId;
            if (enforcePeriodLock && lockCompanyId.HasValue)
            {
                var fp = await db.FiscalPeriods.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.CompanyId == lockCompanyId.Value && posting >= p.StartDate && posting <= p.EndDate);
                if (fp != null && fp.Status != PeriodStatus.Open)
                    throw new InvalidOperationException($"Fiscal period '{fp.Name}' is {fp.Status} for {posting:yyyy-MM-dd}. Re-open the period or skip this run.");
            }

            var map = await db.BookGlAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.BookId == bookId);

            // Fall back to legacy Book.GlAccountDepExp / Book.GlAccountAccumDep when the
            // BookGlAccount row is absent or has blank fields. Either source is valid.
            var depExpense = !string.IsNullOrWhiteSpace(map?.DepreciationExpense)
                ? map!.DepreciationExpense!
                : book.GlAccountDepExp;
            var accumDep = !string.IsNullOrWhiteSpace(map?.AccumulatedDepreciation)
                ? map!.AccumulatedDepreciation!
                : book.GlAccountAccumDep;

            if (string.IsNullOrWhiteSpace(depExpense) || string.IsNullOrWhiteSpace(accumDep))
            {
                throw new InvalidOperationException(
                    "DepreciationExpense and AccumulatedDepreciation GL accounts are required. " +
                    "Set them on Book.GlAccountDepExp / Book.GlAccountAccumDep or via Books → GL Accounts.");
            }

            monthlyTotal = Math.Round(monthlyTotal, 2, MidpointRounding.AwayFromZero);

            var batch = $"DEP-{book.Code}-{period:yyyyMM}";

            var entry = new JournalEntry
            {
                BookId      = bookId,
                Period      = period.Year * 100 + period.Month,
                Batch       = batch,
                PostingDate = posting,
                Reference   = $"DEP {book.Code} {period:yyyy-MM}",
                Source      = "DEP",
                Description = $"Monthly depreciation — {book.Name} {period:yyyy-MM}",
                CreatedUtc  = DateTime.UtcNow
            };

            entry.Lines = new()
            {
                new JournalLine
                {
                    LineNo      = 1,
                    Account     = depExpense!,
                    Description = "Depreciation expense",
                    Debit       = monthlyTotal,
                    Credit      = 0m
                },
                new JournalLine
                {
                    LineNo      = 2,
                    Account     = accumDep!,
                    Description = "Accumulated depreciation",
                    Debit       = 0m,
                    Credit      = monthlyTotal
                }
            };

            db.JournalEntries.Add(entry);
            if (saveChanges)
            {
                await db.SaveChangesAsync();
            }
            return entry;
        }

        // PR #102 (B-11): Method-aware monthly total. Replaces the previous
        // reflection-based TryUseExistingDepreciationService + straight-line
        // fallback pair. The reflection used to look for a method named
        // CalculateMonthly OR Calculate; the real DepreciationService method
        // is CalculateMonthlyDepreciation, so the reflection always returned
        // null and every monthly aggregate JE was computed straight-line
        // regardless of asset.DepreciationMethod — silently stripping MACRS
        // bonus first-year acceleration, DDB front-loading, SYD, and 150DB
        // out of the GL totals. Per-asset snapshots stayed correct; the
        // posted JE did not. This direct call honors the configured method
        // end-to-end and matches the per-asset depreciation that the asset
        // detail page already shows.
        private static async Task<decimal> ComputeMethodAwareMonthlyTotalAsync(AppDbContext db, int bookId, DateTime period, int? companyId)
        {
            _ = await db.Books.FirstAsync(b => b.Id == bookId);

            var assetsQuery = db.Assets.AsNoTracking().AsQueryable();
            if (companyId.HasValue)
                assetsQuery = assetsQuery.Where(a => a.CompanyId == companyId);

            var assets = await assetsQuery.ToListAsync();
            var monthEnd = new DateTime(period.Year, period.Month, DateTime.DaysInMonth(period.Year, period.Month));

            var svc = new DepreciationService();
            decimal total = 0m;
            foreach (var asset in assets)
            {
                if (asset.UsefulLifeMonths <= 0) continue;
                if (asset.InServiceDate > monthEnd) continue;

                var basis = asset.AcquisitionCost - asset.SalvageValue;
                if (basis <= 0) continue;

                // 1-based month index inside the asset's service life. Beyond
                // the last month, depreciation is zero.
                var monthsInService = ((period.Year - asset.InServiceDate.Year) * 12)
                                      + (period.Month - asset.InServiceDate.Month)
                                      + 1;
                if (monthsInService < 1 || monthsInService > asset.UsefulLifeMonths) continue;

                // Current NBV drives DDB / 150DB / SYD. Reads the cached
                // AccumulatedDepreciation snapshot; if the snapshot is stale
                // the per-asset background recompute (DepreciationBackfillService)
                // restamps it on the next read of the asset detail page.
                var currentNBV = asset.AcquisitionCost - asset.AccumulatedDepreciation;
                var lifeYears = Math.Max(1, asset.UsefulLifeMonths / 12);

                var monthly = svc.CalculateMonthlyDepreciation(
                    cost: asset.AcquisitionCost,
                    salvage: asset.SalvageValue,
                    lifeMonths: asset.UsefulLifeMonths,
                    method: asset.DepreciationMethod,
                    currentMonth: monthsInService,
                    currentNBV: currentNBV,
                    lifeYears: lifeYears);

                total += monthly;
            }

            return total;
        }
    }
}
