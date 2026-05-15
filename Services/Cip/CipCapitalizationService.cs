using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Cip
{
    public class CipCapitalizationPreview
    {
        public int CipProjectId { get; set; }
        public string ProjectNumber { get; set; } = string.Empty;
        public decimal TotalCapitalizableCosts { get; set; }
        public decimal TotalNonCapitalizableCosts { get; set; }
        public decimal ProposedAssetBasis { get; set; }
        public int CostCount { get; set; }
        public List<CipCostTypeTotals> ByType { get; set; } = new();
    }

    public class CipCapitalizationService
    {
        private readonly AppDbContext _db;
        private readonly CipCostService _cipCostService;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IGlAccountResolver _glResolver;
        private readonly IPeriodGuard _periodGuard;
        private readonly DepreciationBackfillService _depBackfill;
        private readonly IOutboxWriter _outbox;

        public CipCapitalizationService(
            AppDbContext db,
            CipCostService cipCostService,
            ILookupService lookupService,
            ITenantContext tenantContext,
            IGlAccountResolver glResolver,
            IPeriodGuard periodGuard,
            DepreciationBackfillService depBackfill,
            IOutboxWriter outbox)
        {
            _db = db;
            _cipCostService = cipCostService;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
            _glResolver = glResolver;
            _periodGuard = periodGuard;
            _depBackfill = depBackfill;
            _outbox = outbox;
        }

        public async Task<CipCapitalizationPreview?> PreviewAsync(int cipProjectId)
        {
            var project = await _db.CipProjects
                .Include(p => p.Costs)
                .FirstOrDefaultAsync(p => p.Id == cipProjectId);
            if (project == null) return null;

            var costs = project.Costs?.ToList() ?? new List<CipCost>();
            var costTypeValues = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipCostType");
            var lvMap = costTypeValues.ToDictionary(v => v.Id, v => v);

            var byType = costs
                .Where(c => c.IsCapitalizable)
                .GroupBy(c => c.CostTypeLookupValueId)
                .Select(g =>
                {
                    string name = "Unknown", code = "";
                    if (g.Key.HasValue && lvMap.TryGetValue(g.Key.Value, out var lv))
                    {
                        name = lv.Name;
                        code = lv.Code;
                    }
                    return new CipCostTypeTotals
                    {
                        LookupValueId = g.Key,
                        TypeName = name,
                        TypeCode = code,
                        Amount = g.Sum(c => c.Amount),
                        Count = g.Count()
                    };
                })
                .OrderBy(t => t.TypeName)
                .ToList();

            var capitalizableTotal = costs.Where(c => c.IsCapitalizable).Sum(c => c.Amount);
            var nonCapitalizableTotal = costs.Where(c => !c.IsCapitalizable).Sum(c => c.Amount);

            return new CipCapitalizationPreview
            {
                CipProjectId = cipProjectId,
                ProjectNumber = project.ProjectNumber,
                TotalCapitalizableCosts = capitalizableTotal,
                TotalNonCapitalizableCosts = nonCapitalizableTotal,
                ProposedAssetBasis = capitalizableTotal,
                CostCount = costs.Count,
                ByType = byType
            };
        }

        public async Task<(Asset? asset, CipCapitalization? capitalization)> CapitalizeAsync(
            int cipProjectId,
            string assetNumber,
            string description,
            int? glAccountId = null,
            string? userId = null)
        {
            // S1-4: tenant scope on the project — without this, a caller
            // with a CipProjectId from another tenant could capitalize a
            // foreign-tenant asset into their own.
            var project = await _db.CipProjects
                .Include(p => p.Costs)
                .FirstOrDefaultAsync(p =>
                    p.Id == cipProjectId &&
                    _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0));
            if (project == null)
                throw new InvalidOperationException($"CIP project {cipProjectId} not found.");
            if (project.IsLocked)
                throw new InvalidOperationException($"CIP project {cipProjectId} is already capitalized/locked.");

            // S1-4: PeriodGuard — capitalization writes a JournalEntry against the
            // project's company; if that period is closed, fail closed.
            var capitalizationDate = DateTime.UtcNow;
            var projectCompanyId = project.CompanyId ?? _tenantContext.CompanyId ?? 0;
            if (projectCompanyId > 0)
            {
                var periodCheck = await _periodGuard.CanPostAsync(projectCompanyId, capitalizationDate);
                if (!periodCheck.IsAllowed)
                {
                    throw new InvalidOperationException(periodCheck.Reason
                        ?? $"Cannot capitalize CIP {project.ProjectNumber}: posting period for {capitalizationDate:yyyy-MM-dd} is closed.");
                }
            }

            var costs = project.Costs?.ToList() ?? new List<CipCost>();
            var capitalizableAmount = costs.Where(c => c.IsCapitalizable).Sum(c => c.Amount);

            // DEF-013: wrap the three SaveChanges + depreciation backfill in a
            // single DB transaction. Previously a failure between the asset
            // INSERT and the CipCapitalization INSERT (e.g., a GL resolve
            // exception, a missing column, a period-lock change mid-flight)
            // left an orphan Asset row that then collided on
            // IX_Assets_CompanyId_AssetNumber_Unique on every retry. Atomic
            // commit closes that window.
            Asset asset;
            JournalEntry journalEntry;
            CipCapitalization capitalization;

            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                // S1-4: stamp CompanyId so the new asset is properly tenant-scoped,
                // and OriginatingCipProjectId so we can walk the audit trail back to
                // the source project (S2-4).
                asset = new Asset
                {
                    AssetNumber = assetNumber,
                    Description = description,
                    CompanyId = project.CompanyId,
                    SiteId = project.SiteId,
                    InServiceDate = capitalizationDate,
                    AcquisitionCost = capitalizableAmount,
                    Status = AssetStatus.Active,
                    OriginatingCipProjectId = cipProjectId
                };
                _db.Assets.Add(asset);
                await _db.SaveChangesAsync();

                // S1-4: resolve GL accounts via IGlAccountResolver instead of the
                // hardcoded "1500"/"1400" string literals. Cascade per ADR-003:
                // per-asset override → per-book → per-company config → industry default.
                var glContext = new GlResolveContext(AssetId: asset.Id, CipProjectId: cipProjectId);
                var assetCostAccount = await _glResolver.ResolveAsync(projectCompanyId, GlAccountKind.AssetCost, glContext);
                var cipPendingAccount = await _glResolver.ResolveAsync(projectCompanyId, GlAccountKind.CipPending, glContext);

                journalEntry = new JournalEntry
                {
                    BookId = null, // CIP capitalization is not book-scoped today; the Book FK is nullable per the model. Replace with per-company default-book resolution if/when CIP becomes book-scoped.
                    Batch = $"CIP-CAP-{project.ProjectNumber}",
                    Period = int.Parse(capitalizationDate.ToString("yyyyMM")),
                    PostingDate = capitalizationDate.Date,
                    Source = "CIP Capitalization",
                    Reference = project.ProjectNumber,
                    Description = $"Capitalization of CIP {project.ProjectNumber}: {project.Name}",
                    CreatedUtc = capitalizationDate,
                    Lines = new List<JournalLine>
                    {
                        new JournalLine
                        {
                            LineNo = 1,
                            Account = assetCostAccount,
                            Description = $"Fixed Asset - {assetNumber}",
                            Debit = capitalizableAmount,
                            Credit = 0m
                        },
                        new JournalLine
                        {
                            LineNo = 2,
                            Account = cipPendingAccount,
                            Description = $"CIP WIP - {project.ProjectNumber}",
                            Debit = 0m,
                            Credit = capitalizableAmount
                        }
                    }
                };
                _db.JournalEntries.Add(journalEntry);
                await _db.SaveChangesAsync();

                capitalization = new CipCapitalization
                {
                    CipProjectId = cipProjectId,
                    AssetId = asset.Id,
                    JournalEntryId = journalEntry.Id,
                    CapitalizedAt = capitalizationDate,
                    CapitalizedByUserId = userId ?? "system",
                    TotalCapitalized = capitalizableAmount,
                    CostMappings = costs.Where(c => c.IsCapitalizable).Select(c => new CipCapitalizationCost
                    {
                        CipCostId = c.Id
                    }).ToList()
                };
                _db.CipCapitalizations.Add(capitalization);

                project.IsCapitalized = true;
                project.CapitalizedAt = capitalizationDate;
                project.Status = CipProjectStatus.Capitalized;
                project.ConvertedAssetId = asset.Id;
                project.ActualCompletionDate = capitalizationDate;
                project.PlacedInServiceDate = capitalizationDate;
                project.UpdatedAt = capitalizationDate;

                await _db.SaveChangesAsync();

                // S1-4: kick off depreciation. Without this, the new asset has no
                // AssetBookSettings snapshot and downstream KPIs / depreciation
                // schedule reads see zero. Same pattern as PR #27. Stays inside
                // the transaction so a depreciation-backfill failure rolls back
                // the entire capitalization rather than leaving a half-state.
                await _depBackfill.RecomputeAssetAsync(asset.Id, capitalizationDate);

                await tx.CommitAsync();
            }

            // S2-5/S2-6: emit cip.capitalized AND the CIP-driven asset.created
            // (Origin="cip.capitalized" so consumers can disambiguate from
            // the UI-driven /Assets/Asset create path). Emitted AFTER the
            // business transaction commits so an outbox-write failure doesn't
            // unwind committed financial state. The remaining outbox-atomicity
            // gap (business commit then outbox commit in separate transactions)
            // is tracked in CODE_REVIEW_FOLLOWUPS #6.
            await _outbox.EnqueueAsync(
                projectCompanyId,
                siteId: project.SiteId,
                new CipCapitalizedV1(
                    CipProjectId: project.Id,
                    ProjectNumber: project.ProjectNumber,
                    ProjectName: project.Name ?? string.Empty,
                    CapitalizationId: capitalization.Id,
                    NewAssetId: asset.Id,
                    AssetNumber: asset.AssetNumber,
                    CompanyId: project.CompanyId,
                    SiteId: project.SiteId,
                    TotalCapitalized: capitalizableAmount,
                    CapitalizableCostCount: capitalization.CostMappings.Count,
                    JournalEntryId: journalEntry.Id,
                    CapitalizedAt: capitalizationDate,
                    CapitalizedByUserId: capitalization.CapitalizedByUserId),
                correlationId: $"cip-cap-{project.Id}"
            );

            await _outbox.EnqueueAsync(
                projectCompanyId,
                siteId: asset.SiteId,
                new AssetCreatedV1(
                    AssetId: asset.Id,
                    AssetNumber: asset.AssetNumber,
                    Description: asset.Description,
                    CompanyId: asset.CompanyId,
                    SiteId: asset.SiteId,
                    AcquisitionCost: asset.AcquisitionCost,
                    InServiceDate: asset.InServiceDate,
                    Status: asset.Status.ToString(),
                    AssetCategoryId: asset.AssetCategoryId,
                    VendorId: asset.VendorId,
                    CreatedBy: capitalization.CapitalizedByUserId,
                    Origin: "cip.capitalized"),
                correlationId: $"asset-create-cip-{asset.Id}"
            );

            return (asset, capitalization);
        }
    }
}
