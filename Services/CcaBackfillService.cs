using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services
{
    public class CcaBackfillPreviewRow
    {
        public int AssetId { get; set; }
        public string AssetNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? AssetType { get; set; }
        public decimal AcquisitionCost { get; set; }
        public DateTime InServiceDate { get; set; }
        public int? CurrentCcaClassNumber { get; set; }
        public int SuggestedCcaClassNumber { get; set; }
        public int? SuggestedCcaClassId { get; set; }
        public string SuggestedCcaClassDescription { get; set; } = string.Empty;
        public bool AlreadyMapped => CurrentCcaClassNumber.HasValue;
    }

    public class CcaBackfillReport
    {
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int AssetsScanned { get; set; }
        public int AssetsAlreadyMapped { get; set; }
        public int AssetsMapped { get; set; }
        public int AssetsSkippedNoCost { get; set; }
        public int AssetsSkippedNoClass { get; set; }
        public int ClassYearsComputed { get; set; }
        public decimal TotalCcaClaimed { get; set; }
        public Dictionary<int, int> MappingsByClassNumber { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public DateTime StartedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public int ThroughFiscalYear { get; set; }
        public string Actor { get; set; } = "system";
    }

    /// <summary>
    /// Bootstraps the Canadian CCA tax book for a single company:
    ///  1. Iterates active assets that own no <see cref="AssetTaxSettings"/>.
    ///  2. Suggests a CRA CCA class via <see cref="CcaClassSuggester"/> (admin can override).
    ///  3. Creates <see cref="AssetTaxSettings"/> + opening <see cref="CcaTransaction"/> Addition.
    ///  4. Computes <see cref="CcaClassBalance"/> per (class, fiscal year) from earliest in-service
    ///     year through the requested fiscal year using <see cref="CcaService"/> — which encodes
    ///     half-year, AII, recapture, and terminal-loss rules.
    ///
    /// Idempotent: re-running creates zero new <see cref="AssetTaxSettings"/> and re-stamps
    /// existing unposted balances with the same numbers.
    /// </summary>
    public class CcaBackfillService
    {
        private readonly AppDbContext _db;
        private readonly CcaService _ccaService;
        private readonly ITenantContext _tenantContext;
        private readonly ITenantContextOverride _tenantOverride;
        private readonly AuditService _audit;
        private readonly ILogger<CcaBackfillService> _logger;

        public CcaBackfillService(
            AppDbContext db,
            CcaService ccaService,
            ITenantContext tenantContext,
            ITenantContextOverride tenantOverride,
            AuditService audit,
            ILogger<CcaBackfillService> logger)
        {
            _db = db;
            _ccaService = ccaService;
            _tenantContext = tenantContext;
            _tenantOverride = tenantOverride;
            _audit = audit;
            _logger = logger;
        }

        /// <summary>
        /// Read-only preview of the per-asset mapping for a company. Always safe to call.
        /// </summary>
        public async Task<List<CcaBackfillPreviewRow>> GetPreviewAsync(int companyId)
        {
            var ccaClasses = await _db.CcaClasses.AsNoTracking()
                .Where(c => c.Active)
                .ToListAsync();
            var classByNumber = ccaClasses.ToDictionary(c => c.ClassNumber, c => c);
            var classIdByNumber = ccaClasses.ToDictionary(c => c.ClassNumber, c => c.Id);

            var assets = await _db.Assets.AsNoTracking()
                .Where(a => a.CompanyId == companyId && a.Active)
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();

            var existingTaxSettings = await _db.AssetTaxSettings.AsNoTracking()
                .Include(t => t.CcaClass)
                .Where(t => assets.Select(a => a.Id).Contains(t.AssetId))
                .ToListAsync();
            var taxByAssetId = existingTaxSettings.ToDictionary(t => t.AssetId, t => t);

            var rows = new List<CcaBackfillPreviewRow>(assets.Count);
            foreach (var asset in assets)
            {
                var suggestedNumber = CcaClassSuggester.Suggest(asset);
                classIdByNumber.TryGetValue(suggestedNumber, out var suggestedId);
                classByNumber.TryGetValue(suggestedNumber, out var suggestedClass);

                int? currentNumber = null;
                if (taxByAssetId.TryGetValue(asset.Id, out var tax) && tax.CcaClass != null)
                    currentNumber = tax.CcaClass.ClassNumber;

                rows.Add(new CcaBackfillPreviewRow
                {
                    AssetId = asset.Id,
                    AssetNumber = asset.AssetNumber,
                    Description = asset.Description,
                    AssetType = asset.AssetType,
                    AcquisitionCost = asset.AcquisitionCost,
                    InServiceDate = asset.InServiceDate,
                    CurrentCcaClassNumber = currentNumber,
                    SuggestedCcaClassNumber = suggestedNumber,
                    SuggestedCcaClassId = suggestedId == 0 ? null : suggestedId,
                    SuggestedCcaClassDescription = suggestedClass?.Description ?? string.Empty
                });
            }

            return rows;
        }

        /// <summary>
        /// Bootstraps tax settings for unmapped active assets in <paramref name="companyId"/>
        /// and computes per-class balances through <paramref name="throughFiscalYear"/>.
        /// </summary>
        /// <param name="companyId">Target Canadian company.</param>
        /// <param name="throughFiscalYear">Latest fiscal year to compute (defaults to current calendar year).</param>
        /// <param name="overrideClassByAssetId">Optional admin overrides — maps AssetId → CcaClass.Id.</param>
        /// <param name="createMissingTaxSettings">When false, only recomputes existing balances.</param>
        /// <param name="computeBalances">When false, only creates AssetTaxSettings/CcaTransactions.</param>
        public async Task<CcaBackfillReport> RunAsync(
            int companyId,
            int? throughFiscalYear = null,
            IReadOnlyDictionary<int, int>? overrideClassByAssetId = null,
            bool createMissingTaxSettings = true,
            bool computeBalances = true,
            string actor = "system")
        {
            var report = new CcaBackfillReport
            {
                CompanyId = companyId,
                StartedAt = DateTime.UtcNow,
                ThroughFiscalYear = throughFiscalYear ?? DateTime.UtcNow.Year,
                Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor
            };
            var sw = Stopwatch.StartNew();

            try
            {
                var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId);
                if (company == null)
                {
                    report.Errors.Add($"Company {companyId} not found.");
                    return report;
                }
                report.CompanyName = company.Name;

                var ccaClasses = await _db.CcaClasses.AsNoTracking().Where(c => c.Active).ToListAsync();
                var classIdByNumber = ccaClasses.ToDictionary(c => c.ClassNumber, c => c.Id);
                var classNumberById = ccaClasses.ToDictionary(c => c.Id, c => c.ClassNumber);
                var validClassIds = new HashSet<int>(ccaClasses.Select(c => c.Id));

                if (createMissingTaxSettings)
                {
                    await CreateMissingTaxSettingsAsync(companyId, classIdByNumber, validClassIds, overrideClassByAssetId, classNumberById, report);
                }

                if (computeBalances)
                {
                    await ComputeBalancesAsync(companyId, report.ThroughFiscalYear, report);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CcaBackfillService.RunAsync failed for company {CompanyId}", companyId);
                report.Errors.Add($"FATAL: {ex.GetType().Name}: {ex.Message}");
            }

            sw.Stop();
            report.Duration = sw.Elapsed;
            return report;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Step 1 — create AssetTaxSettings + opening Addition transaction
        // ────────────────────────────────────────────────────────────────────────
        private async Task CreateMissingTaxSettingsAsync(
            int companyId,
            IReadOnlyDictionary<int, int> classIdByNumber,
            HashSet<int> validClassIds,
            IReadOnlyDictionary<int, int>? overrideClassByAssetId,
            IReadOnlyDictionary<int, int> classNumberById,
            CcaBackfillReport report)
        {
            var assets = await _db.Assets
                .Where(a => a.CompanyId == companyId && a.Active)
                .ToListAsync();
            report.AssetsScanned = assets.Count;

            var assetIds = assets.Select(a => a.Id).ToList();
            var existingMappedIds = new HashSet<int>(
                await _db.AssetTaxSettings
                    .Where(t => assetIds.Contains(t.AssetId))
                    .Select(t => t.AssetId)
                    .ToListAsync());

            var ccaClassEntities = await _db.CcaClasses
                .Where(c => validClassIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c);

            foreach (var asset in assets)
            {
                if (existingMappedIds.Contains(asset.Id))
                {
                    report.AssetsAlreadyMapped++;
                    continue;
                }

                if (asset.AcquisitionCost <= 0)
                {
                    report.AssetsSkippedNoCost++;
                    report.Warnings.Add($"Asset {asset.AssetNumber}: AcquisitionCost is {asset.AcquisitionCost:C} — skipped.");
                    continue;
                }

                int? targetClassId = null;
                if (overrideClassByAssetId != null && overrideClassByAssetId.TryGetValue(asset.Id, out var overridden) && validClassIds.Contains(overridden))
                {
                    targetClassId = overridden;
                }
                else
                {
                    targetClassId = CcaClassSuggester.SuggestCcaClassId(asset, classIdByNumber);
                }

                if (!targetClassId.HasValue || !ccaClassEntities.TryGetValue(targetClassId.Value, out var ccaClass))
                {
                    report.AssetsSkippedNoClass++;
                    report.Warnings.Add($"Asset {asset.AssetNumber}: no CCA class could be resolved — skipped.");
                    continue;
                }

                var afuDate = asset.InServiceDate;

                var taxSettings = new AssetTaxSettings
                {
                    AssetId = asset.Id,
                    CcaClassId = ccaClass.Id,
                    AvailableForUseDate = afuDate,
                    CapitalCost = asset.AcquisitionCost,
                    EligibleForAcceleratedIncentive = ccaClass.IsAcceleratedInvestmentIncentive
                };
                _db.AssetTaxSettings.Add(taxSettings);

                var transaction = new CcaTransaction
                {
                    CcaClassId = ccaClass.Id,
                    AssetId = asset.Id,
                    FiscalYear = afuDate.Year,
                    TransactionType = CcaTransactionType.Addition,
                    TransactionDate = asset.InServiceDate,
                    AvailableForUseDate = afuDate,
                    CapitalCost = asset.AcquisitionCost,
                    NetAddition = asset.AcquisitionCost,
                    SubjectToHalfYearRule = ccaClass.HalfYearRuleApplies,
                    IsAcceleratedIncentiveEligible = ccaClass.IsAcceleratedInvestmentIncentive,
                    Description = $"Backfill addition: {asset.AssetNumber} - {asset.Description}",
                    CreatedBy = report.Actor
                };
                _db.CcaTransactions.Add(transaction);

                report.AssetsMapped++;
                if (classNumberById.TryGetValue(ccaClass.Id, out var num))
                {
                    report.MappingsByClassNumber.TryGetValue(num, out var prior);
                    report.MappingsByClassNumber[num] = prior + 1;
                }
            }

            if (report.AssetsMapped > 0)
            {
                await _db.SaveChangesAsync();
                await _audit.LogAsync<AssetTaxSettings>(
                    action: "CCA_BACKFILL",
                    before: null,
                    after: null,
                    username: report.Actor,
                    description: $"CCA backfill for {report.CompanyName} (Co{companyId}): mapped {report.AssetsMapped} new assets, {report.AssetsAlreadyMapped} already mapped, through FY{report.ThroughFiscalYear}.");
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // Step 2 — compute CcaClassBalance per (class, fiscal year)
        // ────────────────────────────────────────────────────────────────────────
        private async Task ComputeBalancesAsync(int companyId, int throughFiscalYear, CcaBackfillReport report)
        {
            // Find every (CcaClassId, earliest in-service year) with at least one asset for the company.
            var classFirstYears = await _db.AssetTaxSettings
                .Include(t => t.Asset)
                .Where(t => t.Asset != null && t.Asset.CompanyId == companyId)
                .GroupBy(t => t.CcaClassId)
                .Select(g => new
                {
                    CcaClassId = g.Key,
                    FirstYear = g.Min(t => t.Asset!.InServiceDate.Year)
                })
                .ToListAsync();

            if (classFirstYears.Count == 0)
            {
                report.Warnings.Add($"Company {companyId}: no AssetTaxSettings found — nothing to compute.");
                return;
            }

            // Run the calculator under an explicit tenant scope so CcaService's per-tenant
            // filters resolve to this company. Preserve and restore the prior hierarchy.
            var priorAssigned = _tenantContext.AssignedCompanyId;
            var priorVisible = _tenantContext.VisibleCompanyIds.ToList();
            var priorTenantId = _tenantContext.TenantId ?? 1;

            try
            {
                _tenantContext.SetHierarchyContext(companyId, new List<int> { companyId });
                using var scope = _tenantOverride.BeginScope(tenantId: priorTenantId, companyId: companyId);

                foreach (var entry in classFirstYears)
                {
                    for (var year = entry.FirstYear; year <= throughFiscalYear; year++)
                    {
                        try
                        {
                            // Skip already-posted balances — never recompute committed numbers.
                            var existing = await _db.CcaClassBalances
                                .FirstOrDefaultAsync(b => b.CcaClassId == entry.CcaClassId && b.FiscalYear == year);
                            if (existing != null && existing.IsPosted)
                            {
                                report.Warnings.Add($"Class {entry.CcaClassId} FY{year}: already posted — skipped.");
                                continue;
                            }

                            var balance = await _ccaService.CalculateCcaForClassAsync(entry.CcaClassId, year);
                            report.ClassYearsComputed++;
                            report.TotalCcaClaimed += balance.CcaClaimed;
                        }
                        catch (Exception ex)
                        {
                            report.Errors.Add($"Class {entry.CcaClassId} FY{year}: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                _tenantContext.SetHierarchyContext(priorAssigned, priorVisible);
            }
        }
    }
}
