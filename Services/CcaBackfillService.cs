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

            // Snapshot prior tenant context so we can restore it in the finally
            // block — we override it during the run so CcaService's per-tenant
            // filters resolve to this Canadian company.
            var priorAssigned = _tenantContext.AssignedCompanyId;
            var priorVisible = _tenantContext.VisibleCompanyIds.ToList();
            var priorTenantId = _tenantContext.TenantId ?? 1;
            IDisposable? scope = null;
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;

            try
            {
                // ── Authorization gate: only Canadian companies are allowed.
                // The page filters the dropdown, but we never trust the posted
                // CompanyId — a tampered request must not mutate non-Canadian
                // tax data. (Out-of-scope per task spec.)
                var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId);
                if (company == null)
                {
                    report.Errors.Add($"Company {companyId} not found.");
                    return report;
                }
                report.CompanyName = company.Name;

                if (!IsCanadian(company))
                {
                    report.Errors.Add($"Company {companyId} ({company.Name}) is not a Canadian company (Country='{company.Country}'). CCA backfill is restricted to Canadian subsidiaries.");
                    return report;
                }

                // ── Multi-Canadian-company guard: until CcaClassBalance is
                // company-scoped at the schema level (see follow-up task),
                // running balance computation while a second Canadian company
                // also has CCA mappings would corrupt that company's totals
                // (balances are unique per (CcaClassId, FiscalYear) globally).
                if (computeBalances)
                {
                    var otherCanadian = await _db.Companies.AsNoTracking()
                        .Where(c => c.Id != companyId && c.IsActive && c.Country != null && c.Country.ToUpper().Contains("CANADA"))
                        .Select(c => new { c.Id, c.Name })
                        .ToListAsync();
                    if (otherCanadian.Count > 0)
                    {
                        var others = string.Join(", ", otherCanadian.Select(c => $"{c.Name} (Co{c.Id})"));
                        report.Errors.Add($"Multiple Canadian companies detected ({others}). Computing CCA balances is disabled until CcaClassBalance is company-scoped at the schema level. Re-run with ComputeBalances=false to only create AssetTaxSettings.");
                        return report;
                    }
                }

                // ── Establish tenant scope for the entire run so
                // CcaService.AddAssetToCcaClassAsync (and CalculateCcaForClassAsync)
                // resolve to this company.
                _tenantContext.SetHierarchyContext(companyId, new List<int> { companyId });
                scope = _tenantOverride.BeginScope(tenantId: priorTenantId, companyId: companyId);

                // ── All-or-nothing: wrap the entire run in a DB transaction so
                // a mid-run failure does not leave partial AssetTaxSettings.
                tx = await _db.Database.BeginTransactionAsync();

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

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CcaBackfillService.RunAsync failed for company {CompanyId}", companyId);
                report.Errors.Add($"FATAL: {ex.GetType().Name}: {ex.Message}");
                if (tx != null)
                {
                    try { await tx.RollbackAsync(); } catch { /* best effort */ }
                }
            }
            finally
            {
                tx?.Dispose();
                scope?.Dispose();
                _tenantContext.SetHierarchyContext(priorAssigned, priorVisible);
            }

            sw.Stop();
            report.Duration = sw.Elapsed;
            return report;
        }

        private static bool IsCanadian(Company c) =>
            !string.IsNullOrWhiteSpace(c.Country) && c.Country.ToUpper().Contains("CANADA");

        // ────────────────────────────────────────────────────────────────────────
        // Step 1 — delegate to CcaService.AddAssetToCcaClassAsync per asset.
        //
        // Per-asset try/catch records failures on the report without aborting
        // the whole run. The outer RunAsync transaction guarantees atomicity:
        // a thrown FATAL rolls everything back together.
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

                if (!targetClassId.HasValue)
                {
                    report.AssetsSkippedNoClass++;
                    report.Warnings.Add($"Asset {asset.AssetNumber}: no CCA class could be resolved — skipped.");
                    continue;
                }

                try
                {
                    // Delegate to the canonical CcaService creation path so we
                    // inherit its tenant filter checks and stay aligned with
                    // future changes to its rules.
                    await _ccaService.AddAssetToCcaClassAsync(asset.Id, targetClassId.Value, asset.InServiceDate);

                    // Stamp the just-created opening transaction with the
                    // backfill marker + actor so it's distinguishable in the
                    // audit trail. (CcaService doesn't take an actor.)
                    var openingTx = await _db.CcaTransactions
                        .Where(t => t.AssetId == asset.Id
                                 && t.TransactionType == CcaTransactionType.Addition
                                 && t.CcaClassId == targetClassId.Value)
                        .OrderByDescending(t => t.Id)
                        .FirstOrDefaultAsync();
                    if (openingTx != null)
                    {
                        openingTx.CreatedBy = report.Actor;
                        openingTx.Description = $"Backfill addition: {asset.AssetNumber} - {asset.Description}";
                        await _db.SaveChangesAsync();
                    }

                    report.AssetsMapped++;
                    if (classNumberById.TryGetValue(targetClassId.Value, out var num))
                    {
                        report.MappingsByClassNumber.TryGetValue(num, out var prior);
                        report.MappingsByClassNumber[num] = prior + 1;
                    }
                }
                catch (Exception ex)
                {
                    // Capture and continue — do NOT abort the whole batch over
                    // a single bad asset. The outer transaction will still be
                    // committed for the assets that succeeded.
                    report.Errors.Add($"Asset {asset.AssetNumber} (id {asset.Id}): {ex.GetType().Name}: {ex.Message}");
                    _logger.LogWarning(ex, "CCA backfill: failed to map asset {AssetId} to class {CcaClassId}", asset.Id, targetClassId);
                }
            }

            if (report.AssetsMapped > 0)
            {
                await _audit.LogAsync<AssetTaxSettings>(
                    action: "CCA_BACKFILL",
                    before: null,
                    after: null,
                    username: report.Actor,
                    description: $"CCA backfill for {report.CompanyName} (Co{companyId}): mapped {report.AssetsMapped} new assets, {report.AssetsAlreadyMapped} already mapped, {report.Errors.Count} errors, through FY{report.ThroughFiscalYear}.");
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

            // Tenant scope is already set by RunAsync — CcaService's per-tenant
            // filters resolve to this company without further setup here.
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
    }
}
