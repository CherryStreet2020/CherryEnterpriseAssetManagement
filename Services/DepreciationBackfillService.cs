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
    }
}
