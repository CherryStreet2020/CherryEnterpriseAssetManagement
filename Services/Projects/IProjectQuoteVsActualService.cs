// Theme B9 Wave 5 PR-13 (2026-05-30) — IProjectQuoteVsActualService.
//
// The estimating-improvement surface: it puts the FROZEN quoted cost model
// (the PR-5 ProjectEstimateSnapshot captured at quote submission / award) next
// to the PR-12 live actuals + EAC, element-for-element, so you can see exactly
// where margin was won or lost. This is the feedback loop that makes the NEXT
// quote better — the gold no incumbent surfaces cleanly.
//
// READ-ONLY (no schema). ADR-025: reads through IProjectFinancialsService for
// the actual/EAC side and the AppDbContext for the quoted snapshot. Tenant scope
// flows through the parent CustomerProject.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectQuoteVsActualService
{
    /// <summary>Quoted (frozen estimate snapshot) vs actual/EAC, by cost bucket + headline.</summary>
    Task<Result<ProjectQuoteVsActualView>> GetComparisonAsync(int projectId, CancellationToken ct = default);
}

public sealed record ProjectQuoteVsActualView(
    int ProjectId,
    bool HasQuotedBaseline,
    string Currency,
    int? EstimateSnapshotId,
    System.DateTime? SnapshotCapturedAt,
    // Cost headline.
    decimal QuotedTotalCost,
    decimal ActualCostToDate,
    decimal EstimateAtCompletion,
    decimal QuotedCostVsEacVariance,        // EAC − Quoted (positive = projected overrun)
    decimal? QuotedCostVsEacVariancePct,
    // Price + margin headline.
    decimal? QuotedPrice,
    decimal ContractValue,
    decimal? QuotedMarginPct,
    decimal? ProjectedMarginPct,
    // Lead time (quoted; actual-delivery comparison lands with the schedule actuals).
    int? QuotedLeadTimeDays,
    string Narrative,
    IReadOnlyList<QuoteBucketRow> Buckets);

public sealed record QuoteBucketRow(
    string Bucket,
    decimal Quoted,
    decimal Actual,
    decimal Variance,            // Actual − Quoted (positive = over the quote)
    decimal? VariancePct);
