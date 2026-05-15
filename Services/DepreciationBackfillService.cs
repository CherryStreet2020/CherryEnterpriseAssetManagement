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
    public class DepreciationBackfillReport
    {
        public int CompaniesScanned { get; set; }
        public int GaapBooksCreated { get; set; }
        public int TaxBooksCreated { get; set; }
        public int BookGlMappingsCreated { get; set; }
        public int AssetBookSettingsCreated { get; set; }
        public int AssetBookSettingsExisting { get; set; }
        public int AssetsRecomputed { get; set; }
        public int AssetsSkipped { get; set; }
        public decimal TotalAccumulatedDepreciationStamped { get; set; }
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public DateTime AsOfDate { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// One-stop service that gets the depreciation engine producing real numbers:
    ///  1. Ensures every Company with assets has a GAAP book.
    ///  2. Ensures every Book has a BookGlAccount mapping (so journals can post).
    ///  3. Ensures every active Asset has an AssetBookSettings row against its company's GAAP book.
    ///  4. Computes historic depreciation per asset up to asOfDate and stamps Asset and AssetBookSettings.
    ///
    /// Idempotent: safe to re-run. Designed to fix the "all 329 assets show $0 accumulated depreciation" gap.
    /// </summary>
    public class DepreciationBackfillService
    {
        private readonly AppDbContext _db;
        private readonly DepreciationService _depService;
        private readonly ILogger<DepreciationBackfillService> _logger;

        // Industry-standard chart of accounts defaults used when Book has nothing configured.
        private const string DefaultAssetAccount = "1500";        // Property, Plant & Equipment
        private const string DefaultAccumDepAccount = "1510";     // Accumulated Depreciation (contra)
        private const string DefaultDepExpAccount = "6500";       // Depreciation Expense
        private const string DefaultClearingAccount = "1110";     // Asset Clearing / Cash
        private const string DefaultGainAccount = "4500";         // Gain on Disposal
        private const string DefaultLossAccount = "6510";         // Loss on Disposal
        private const string DefaultCipAccount = "1400";          // Construction in Progress

        public DepreciationBackfillService(
            AppDbContext db,
            DepreciationService depService,
            ILogger<DepreciationBackfillService> logger)
        {
            _db = db;
            _depService = depService;
            _logger = logger;
        }

        public async Task<DepreciationBackfillReport> RunAsync(
            DateTime? asOfDate = null,
            bool createMissingBooks = true,
            bool createMissingGlMappings = true,
            bool createMissingAssetBookSettings = true,
            bool computeHistoricDepreciation = true,
            string actor = "system")
        {
            var report = new DepreciationBackfillReport
            {
                AsOfDate = asOfDate ?? DateTime.UtcNow.Date
            };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Wrap the entire run in a single transaction so a mid-step failure cannot leave
            // orphaned Books / GL mappings / AssetBookSettings without depreciation stamped.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (createMissingBooks) await EnsureBooksForCompaniesAsync(report, actor);
                if (createMissingGlMappings) await EnsureBookGlMappingsAsync(report);
                if (createMissingAssetBookSettings) await EnsureAssetBookSettingsAsync(report);
                if (computeHistoricDepreciation) await ComputeHistoricDepreciationAsync(report);

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Depreciation backfill failed; rolling back");
                report.Errors.Add($"FATAL: {ex.GetType().Name}: {ex.Message}");
                try { await tx.RollbackAsync(); } catch { /* swallow rollback errors */ }
            }

            sw.Stop();
            report.Duration = sw.Elapsed;
            return report;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Step 1: Books
        // ────────────────────────────────────────────────────────────────────────
        private async Task EnsureBooksForCompaniesAsync(DepreciationBackfillReport report, string actor)
        {
            // Find every company that owns at least one asset.
            var companyIds = await _db.Assets
                .Where(a => a.CompanyId != null)
                .Select(a => a.CompanyId!.Value)
                .Distinct()
                .ToListAsync();

            report.CompaniesScanned = companyIds.Count;

            var existingBookCompanyIds = await _db.Books
                .Where(b => b.CompanyId != null)
                .Select(b => new { b.CompanyId, b.BookType })
                .ToListAsync();

            foreach (var companyId in companyIds)
            {
                var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
                if (company == null)
                {
                    report.Warnings.Add($"Company {companyId} referenced by assets but not found in Companies table; skipping book creation.");
                    continue;
                }

                bool hasGaap = existingBookCompanyIds.Any(b => b.CompanyId == companyId && b.BookType == BookType.Financial);

                if (!hasGaap)
                {
                    var gaap = new Book
                    {
                        Code = $"GAAP-{company.CompanyCode ?? companyId.ToString()}",
                        Name = $"GAAP Book — {company.Name}",
                        Description = "Primary financial reporting book (auto-created by DepreciationBackfillService).",
                        Method = DepreciationMethod.StraightLine,
                        Convention = DepreciationConvention.HalfYear,
                        BookType = BookType.Financial,
                        TaxJurisdiction = TaxJurisdiction.USA,
                        IsPrimaryBook = true,
                        IsActive = true,
                        CompanyId = companyId,
                        GlAccountDepExp = DefaultDepExpAccount,
                        GlAccountAccumDep = DefaultAccumDepAccount,
                        GlAccountAssetClearing = DefaultAssetAccount,
                        GlAccountGainOnDisposal = DefaultGainAccount,
                        GlAccountLossOnDisposal = DefaultLossAccount,
                        GlAccountCIP = DefaultCipAccount,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = actor
                    };
                    _db.Books.Add(gaap);
                    report.GaapBooksCreated++;
                }
            }

            if (report.GaapBooksCreated > 0)
                await _db.SaveChangesAsync();
        }

        // ────────────────────────────────────────────────────────────────────────
        // Step 2: BookGlAccounts (per-book mapping)
        // ────────────────────────────────────────────────────────────────────────
        private async Task EnsureBookGlMappingsAsync(DepreciationBackfillReport report)
        {
            var allBooks = await _db.Books.ToListAsync();
            var existingMappings = await _db.BookGlAccounts.Select(g => g.BookId).ToListAsync();
            var existingSet = new HashSet<int>(existingMappings);

            foreach (var book in allBooks)
            {
                if (existingSet.Contains(book.Id)) continue;

                var mapping = new BookGlAccount
                {
                    BookId = book.Id,
                    Asset = book.GlAccountAssetClearing ?? DefaultAssetAccount,
                    AccumulatedDepreciation = book.GlAccountAccumDep ?? DefaultAccumDepAccount,
                    DepreciationExpense = book.GlAccountDepExp ?? DefaultDepExpAccount,
                    GainOnDisposal = book.GlAccountGainOnDisposal ?? DefaultGainAccount,
                    LossOnDisposal = book.GlAccountLossOnDisposal ?? DefaultLossAccount,
                    Clearing = DefaultClearingAccount,
                    CIP = book.GlAccountCIP ?? DefaultCipAccount
                };
                _db.BookGlAccounts.Add(mapping);
                report.BookGlMappingsCreated++;
            }

            if (report.BookGlMappingsCreated > 0)
                await _db.SaveChangesAsync();
        }

        // ────────────────────────────────────────────────────────────────────────
        // Step 3: AssetBookSettings (Asset ↔ Book linkage)
        // ────────────────────────────────────────────────────────────────────────

        // DEF-014: per-asset variant of EnsureAssetBookSettingsAsync so the CIP
        // capitalize path (and any future single-asset bootstrap) can provision
        // AssetBookSettings rows without scanning the whole tenant. Returns
        // the number of settings rows created (0 if the asset already has
        // settings, or no GAAP book exists for its company).
        public async Task<int> EnsureAssetBookSettingsForAssetAsync(int assetId)
        {
            var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId);
            if (asset == null || asset.CompanyId == null) return 0;

            // Find the primary GAAP book for this asset's company. Same selection
            // rule as the bulk EnsureAssetBookSettingsAsync path: prefer the one
            // explicitly flagged IsPrimaryBook, then lowest Id as tiebreaker.
            var book = await _db.Books
                .Where(b => b.BookType == BookType.Financial
                            && b.IsActive
                            && b.CompanyId == asset.CompanyId)
                .OrderByDescending(b => b.IsPrimaryBook)
                .ThenBy(b => b.Id)
                .FirstOrDefaultAsync();
            if (book == null) return 0;

            var exists = await _db.AssetBookSettings
                .AnyAsync(s => s.AssetId == assetId && s.BookId == book.Id);
            if (exists) return 0;

            var settings = new AssetBookSettings
            {
                AssetId = assetId,
                BookId = book.Id,
                MethodOverride = null,
                ConventionOverride = null,
                UsefulLifeMonthsOverride = null,
                AccumulatedDepreciation = 0m,
                BookValue = asset.AcquisitionCost,
                IsExcludedFromBook = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.AssetBookSettings.Add(settings);
            await _db.SaveChangesAsync();
            return 1;
        }

        private async Task EnsureAssetBookSettingsAsync(DepreciationBackfillReport report)
        {
            // Build lookup: companyId -> primary GAAP book
            var primaryBooks = await _db.Books
                .Where(b => b.BookType == BookType.Financial && b.IsActive)
                .ToListAsync();
            var bookByCompany = primaryBooks
                .Where(b => b.CompanyId.HasValue)
                .GroupBy(b => b.CompanyId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(b => b.IsPrimaryBook).ThenBy(b => b.Id).First());

            var assets = await _db.Assets
                .Where(a => a.Active && a.CompanyId != null)
                .ToListAsync();

            var existingPairs = await _db.AssetBookSettings
                .Select(s => new { s.AssetId, s.BookId })
                .ToListAsync();
            var existingSet = new HashSet<(int, int)>(existingPairs.Select(p => (p.AssetId, p.BookId)));

            foreach (var asset in assets)
            {
                if (!bookByCompany.TryGetValue(asset.CompanyId!.Value, out var book))
                {
                    report.Warnings.Add($"Asset {asset.AssetNumber} (company {asset.CompanyId}) has no GAAP book — skipping.");
                    continue;
                }

                var key = (asset.Id, book.Id);
                if (existingSet.Contains(key))
                {
                    report.AssetBookSettingsExisting++;
                    continue;
                }

                var settings = new AssetBookSettings
                {
                    AssetId = asset.Id,
                    BookId = book.Id,
                    // Use sensible per-book defaults (asset's own UsefulLifeMonths flows through EffectiveUsefulLifeMonths)
                    MethodOverride = null,         // inherit from Book.Method (StraightLine)
                    ConventionOverride = null,     // inherit from Book.Convention (HalfYear)
                    UsefulLifeMonthsOverride = null,
                    AccumulatedDepreciation = 0m,
                    BookValue = asset.AcquisitionCost,
                    IsExcludedFromBook = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AssetBookSettings.Add(settings);
                report.AssetBookSettingsCreated++;
            }

            if (report.AssetBookSettingsCreated > 0)
                await _db.SaveChangesAsync();
        }

        // ────────────────────────────────────────────────────────────────────────
        // Step 4: Compute and stamp historic depreciation
        // ────────────────────────────────────────────────────────────────────────
        private async Task ComputeHistoricDepreciationAsync(DepreciationBackfillReport report)
        {
            // Pull every asset together with its (primary) book settings + book.
            var settingsList = await _db.AssetBookSettings
                .Include(s => s.Asset)
                .Include(s => s.Book)
                .Where(s => s.Asset != null && s.Book != null && s.Book.BookType == BookType.Financial && !s.IsExcludedFromBook)
                .ToListAsync();

            foreach (var settings in settingsList)
            {
                if (settings.Asset == null || settings.Book == null)
                {
                    report.AssetsSkipped++;
                    continue;
                }

                var asset = settings.Asset;

                // Skip non-depreciable cases
                if (asset.AcquisitionCost <= 0 || asset.UsefulLifeMonths <= 0 || asset.InServiceDate > report.AsOfDate)
                {
                    report.AssetsSkipped++;
                    continue;
                }

                try
                {
                    var schedule = _depService.BuildScheduleWithSettings(asset, report.AsOfDate, settings);
                    if (schedule.Count == 0)
                    {
                        report.AssetsSkipped++;
                        continue;
                    }

                    var lastRow = schedule[^1];
                    var accum = lastRow.AccumulatedDepreciation;
                    var nbv = lastRow.EndingBookValue;

                    asset.AccumulatedDepreciation = accum;
                    asset.BookValue = nbv;
                    asset.LastDepreciationDate = lastRow.PeriodEnd;

                    settings.AccumulatedDepreciation = accum;
                    settings.BookValue = nbv;
                    settings.LastDepreciationDate = lastRow.PeriodEnd;
                    settings.UpdatedAt = DateTime.UtcNow;

                    report.AssetsRecomputed++;
                    report.TotalAccumulatedDepreciationStamped += accum;
                }
                catch (Exception ex)
                {
                    report.AssetsSkipped++;
                    report.Warnings.Add($"Asset {asset.AssetNumber}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (report.AssetsRecomputed > 0)
                await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Recomputes the depreciation snapshot for a single asset across all
        /// of its non-excluded AssetBookSettings. Designed to be called after
        /// in-place mutations that change <see cref="Asset.AcquisitionCost"/>,
        /// <see cref="Asset.UsefulLifeMonths"/>, <see cref="Asset.SalvageValue"/>,
        /// or <see cref="Asset.InServiceDate"/> — typically a capital
        /// improvement or asset adjustment posting.
        ///
        /// Idempotent. Does not touch posted <see cref="JournalEntry"/> rows;
        /// only restamps the running totals on <see cref="Asset"/> and each
        /// <see cref="AssetBookSettings"/>. The new schedule is rebuilt from
        /// the asset's current state, so the caller MUST persist mutations
        /// (via <c>SaveChangesAsync</c>) before invoking this method.
        /// </summary>
        /// <param name="assetId">The asset whose snapshot to recompute.</param>
        /// <param name="asOfDate">As-of date for the schedule's last row.
        /// Defaults to today (UTC).</param>
        /// <returns>The number of <see cref="AssetBookSettings"/> rows whose
        /// snapshot was updated. Returns 0 if the asset has no settings, is
        /// not depreciable, or its in-service date is in the future.</returns>
        public async Task<int> RecomputeAssetAsync(int assetId, DateTime? asOfDate = null)
        {
            var effectiveAsOf = asOfDate ?? DateTime.UtcNow.Date;

            var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId);
            if (asset == null) return 0;

            // Non-depreciable shapes — caller is expected to handle the asset
            // state at the page-handler level. We just no-op so the page can
            // continue without burying this case in an exception.
            if (asset.AcquisitionCost <= 0
                || asset.UsefulLifeMonths <= 0
                || asset.InServiceDate > effectiveAsOf)
            {
                return 0;
            }

            var settingsList = await _db.AssetBookSettings
                .Include(s => s.Book)
                .Where(s => s.AssetId == assetId && !s.IsExcludedFromBook)
                .ToListAsync();

            // Re-attach asset to each settings row so DepreciationService can
            // read .Asset off the settings (the load above pulled by AssetId,
            // not Include(s.Asset), to keep the round-trip small).
            foreach (var s in settingsList) s.Asset = asset;

            int recomputed = 0;
            decimal latestAccum = 0m;
            decimal latestNbv = asset.AcquisitionCost;
            DateTime? latestPeriodEnd = null;

            foreach (var settings in settingsList)
            {
                if (settings.Book == null) continue;

                List<DepreciationRow> schedule;
                try
                {
                    schedule = _depService.BuildScheduleWithSettings(asset, effectiveAsOf, settings);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "RecomputeAssetAsync: schedule build failed for asset {AssetId} book {BookId}",
                        assetId, settings.BookId);
                    continue;
                }

                if (schedule.Count == 0) continue;

                var lastRow = schedule[^1];
                settings.AccumulatedDepreciation = lastRow.AccumulatedDepreciation;
                settings.BookValue = lastRow.EndingBookValue;
                settings.LastDepreciationDate = lastRow.PeriodEnd;
                settings.UpdatedAt = DateTime.UtcNow;
                recomputed++;

                // Track the GAAP/financial book numbers separately so we can
                // also stamp them on the Asset row (which mirrors the primary
                // financial book's snapshot — see ComputeHistoricDepreciationAsync).
                if (settings.Book.BookType == BookType.Financial)
                {
                    latestAccum = lastRow.AccumulatedDepreciation;
                    latestNbv = lastRow.EndingBookValue;
                    latestPeriodEnd = lastRow.PeriodEnd;
                }
            }

            // Stamp asset-level snapshot from the financial book if we found
            // one; otherwise leave the asset's own AccumulatedDepreciation /
            // BookValue alone — they're outputs of the GAAP book and have no
            // meaning without one.
            if (latestPeriodEnd.HasValue)
            {
                asset.AccumulatedDepreciation = latestAccum;
                asset.BookValue = latestNbv;
                asset.LastDepreciationDate = latestPeriodEnd.Value;
            }

            if (recomputed > 0)
                await _db.SaveChangesAsync();

            return recomputed;
        }
    }
}
