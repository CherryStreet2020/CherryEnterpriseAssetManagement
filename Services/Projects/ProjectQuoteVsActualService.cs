// Theme B9 Wave 5 PR-13 (2026-05-30) — ProjectQuoteVsActualService impl.
//
// Reads the quoted baseline (the awarded revision's ProjectEstimateSnapshot,
// else the latest snapshot for the project) and lines it up against the live
// actuals + EAC from IProjectFinancialsService, bucket-for-bucket. READ-ONLY.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;       // CostElementType
using Abs.FixedAssets.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectQuoteVsActualService : IProjectQuoteVsActualService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IProjectFinancialsService _financials;
    private readonly ILogger<ProjectQuoteVsActualService> _log;

    public ProjectQuoteVsActualService(
        AppDbContext db, ITenantContext tenant,
        IProjectFinancialsService financials, ILogger<ProjectQuoteVsActualService> log)
    {
        _db = db; _tenant = tenant; _financials = financials; _log = log;
    }

    // Map a fine-grained CostElementType to the 5 estimate-snapshot buckets.
    private static string BucketOf(CostElementType e) => e switch
    {
        CostElementType.Material => "Material",
        CostElementType.Labor => "Labor",
        CostElementType.Subcontract => "Subcontract",
        CostElementType.VariableOverhead or CostElementType.FixedOverhead => "Overhead",
        _ => "Other",   // Setup / Tooling / Other
    };

    private static readonly string[] BucketOrder = { "Material", "Labor", "Subcontract", "Overhead", "Other" };

    public async Task<Result<ProjectQuoteVsActualView>> GetComparisonAsync(int projectId, CancellationToken ct = default)
    {
        if (projectId <= 0) return Result.Failure<ProjectQuoteVsActualView>("CustomerProjectId must be > 0.");
        var seen = await _db.CustomerProjects
            .AnyAsync(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0), ct);
        if (!seen) return Result.Failure<ProjectQuoteVsActualView>($"Customer project {projectId} not found in your tenant scope.");

        // The live actual/EAC side (reuse the PR-12 margin engine).
        var finRes = await _financials.GetFinancialsAsync(projectId, ct);
        if (finRes.IsFailure) return Result.Failure<ProjectQuoteVsActualView>(finRes.Error!);
        var fin = finRes.Value!;

        // Actual-to-date rolled into the 5 quote buckets.
        var actualByBucket = fin.ByElement
            .GroupBy(e => BucketOf(e.CostElementType))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Actual));

        // The quoted baseline snapshot: prefer the awarded/baseline revision's
        // source snapshot, else the latest snapshot for the project.
        int? awardedSnapId = await (
            from r in _db.ProjectQuoteRevisions
            join q in _db.ProjectQuotes on r.ProjectQuoteId equals q.Id
            where q.CustomerProjectId == projectId
                && r.SourceEstimateSnapshotId != null
                && (r.ConvertedToBaseline || r.VersionStatus == ProjectQuoteRevisionStatus.Awarded)
            orderby r.Id descending
            select r.SourceEstimateSnapshotId).FirstOrDefaultAsync(ct);

        ProjectEstimateSnapshot? snap = null;
        if (awardedSnapId is int sid && sid > 0)
            snap = await _db.ProjectEstimateSnapshots
                .FirstOrDefaultAsync(s => s.Id == sid && s.CustomerProjectId == projectId, ct);
        snap ??= await _db.ProjectEstimateSnapshots
            .Where(s => s.CustomerProjectId == projectId)
            .OrderByDescending(s => s.CapturedAt).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

        var currency = !string.IsNullOrWhiteSpace(fin.Currency) ? fin.Currency : "USD";

        if (snap is null)
        {
            // Graceful empty surface — actuals exist but nothing to compare against.
            return Result.Success(new ProjectQuoteVsActualView(
                projectId, HasQuotedBaseline: false, currency, null, null,
                0m, fin.ActualCostToDate, fin.EstimateAtCompletion, 0m, null,
                null, fin.ContractValue, null, fin.ProjectedMarginPercent, null,
                "No quoted baseline (estimate snapshot) captured for this project yet — quote-vs-actual will populate once a quote is submitted/awarded.",
                Array.Empty<QuoteBucketRow>()));
        }

        // Quoted cost per bucket (frozen snapshot roll-up).
        var quotedByBucket = new Dictionary<string, decimal>
        {
            ["Material"] = snap.MaterialCost,
            ["Labor"] = snap.LaborCost,
            ["Subcontract"] = snap.SubcontractCost,
            ["Overhead"] = snap.OverheadCost,
            ["Other"] = snap.OtherCost,
        };

        var buckets = BucketOrder
            .Where(b => quotedByBucket[b] != 0m || (actualByBucket.TryGetValue(b, out var a0) && a0 != 0m))
            .Select(b =>
            {
                var quoted = quotedByBucket[b];
                var actual = actualByBucket.TryGetValue(b, out var a) ? a : 0m;
                var variance = actual - quoted;
                decimal? pct = quoted != 0m ? Math.Round(variance / quoted * 100m, 2) : (decimal?)null;
                return new QuoteBucketRow(b, quoted, actual, variance, pct);
            }).ToList();

        // The quoted lead time from the awarded revision, if present.
        int? quotedLead = await (
            from r in _db.ProjectQuoteRevisions
            join q in _db.ProjectQuotes on r.ProjectQuoteId equals q.Id
            where q.CustomerProjectId == projectId && r.SourceEstimateSnapshotId == snap.Id
            orderby r.Id descending
            select r.QuotedLeadTimeDays).FirstOrDefaultAsync(ct);

        var quotedTotal = snap.TotalCost;
        var eacVariance = fin.EstimateAtCompletion - quotedTotal;
        decimal? eacVariancePct = quotedTotal != 0m ? Math.Round(eacVariance / quotedTotal * 100m, 2) : (decimal?)null;

        var narrative =
            $"Quoted cost {quotedTotal:C0} vs EAC {fin.EstimateAtCompletion:C0} — " +
            $"{(eacVariance > 0 ? $"projected overrun {eacVariance:C0}" : eacVariance < 0 ? $"under quote by {Math.Abs(eacVariance):C0}" : "on quote")}. " +
            $"Margin quoted {(snap.EstimatedMarginPct.HasValue ? snap.EstimatedMarginPct.Value.ToString("0.#") + "%" : "n/a")} " +
            $"vs projected {(fin.ProjectedMarginPercent.HasValue ? fin.ProjectedMarginPercent.Value.ToString("0.#") + "%" : "n/a")}.";

        return Result.Success(new ProjectQuoteVsActualView(
            projectId, HasQuotedBaseline: true, currency, snap.Id, snap.CapturedAt,
            quotedTotal, fin.ActualCostToDate, fin.EstimateAtCompletion, eacVariance, eacVariancePct,
            snap.QuotedPrice, fin.ContractValue, snap.EstimatedMarginPct, fin.ProjectedMarginPercent,
            quotedLead == 0 ? null : quotedLead,
            narrative, buckets));
    }
}
