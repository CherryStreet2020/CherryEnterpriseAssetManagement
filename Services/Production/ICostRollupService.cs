// Sprint 14.4 PR-3 (2026-05-28) — Cost Rollup Engine interface.
//
// THE rollup engine from Dean's 910-line cost-object graph research spec.
// Implements the 8-step algorithm from §20:
//   1. Build cost graph (PRO parent-child + supply links)
//   2. Validate graph (cycle detection + single ownership)
//   3. Pull originating costs (Layer A)
//   4. Pull transfer costs (Layer B)
//   5. Classify each line (Additive/Transfer/Drilldown/Reversal/Variance/Provisional)
//   6. Calculate totals by mode (Financial or Exploded)
//   7. Detect exceptions (§13 — 16+ types)
//   8. Store rollup run with lines + exceptions
//
// Two valid rollup methods (§9):
//   Financial — parent total uses child transfer cost (ties to WIP/GL)
//   Exploded  — ignores transfers, sums originating costs across graph (analysis)
//
// Anti-compounding rule: RollupAdditiveFlag prevents double-counting.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

/// <summary>Cost graph node for internal rollup computation.</summary>
public sealed class CostGraphNode
{
    public CostObjectType CostObjectType { get; init; }
    public int CostObjectId { get; init; }
    public int? ProductionOrderId { get; init; }
    public int? ParentNodeId { get; init; }
    public int Depth { get; set; }
    public int? SiteId { get; init; }

    /// <summary>Display label for the node (e.g., "PRO-1001" or "Child WO-1002").</summary>
    public string Label { get; init; } = string.Empty;

    public List<CostGraphNode> Children { get; } = new();
    public List<CostTransaction> OriginatingCosts { get; } = new();
    public List<CostTransfer> InboundTransfers { get; } = new();
    public List<CostTransfer> OutboundTransfers { get; } = new();
}

/// <summary>Result of a rollup execution.</summary>
public sealed class CostRollupResult
{
    public CostRollupRun Run { get; init; } = null!;
    public IReadOnlyList<CostRollupLine> Lines { get; init; } = Array.Empty<CostRollupLine>();
    public IReadOnlyList<CostRollupException> Exceptions { get; init; } = Array.Empty<CostRollupException>();
    public CostGraphNode? Graph { get; init; }
}

public interface ICostRollupService
{
    // ── Full orchestration ──────────────────────────────────────
    /// <summary>
    /// Execute a full rollup for a production order in the specified mode.
    /// Steps: build graph → validate → pull costs → classify → calculate → detect exceptions → store run.
    /// Stamps 30+ header fields on ProductionOrderCostSummary.
    /// </summary>
    Task<Result<CostRollupResult>> ExecuteRollupAsync(
        int productionOrderId,
        CostRollupMode mode,
        string? executedBy,
        CancellationToken ct = default);

    // ── Graph operations ────────────────────────────────────────
    /// <summary>Build the cost object graph rooted at a production order.</summary>
    Task<Result<CostGraphNode>> BuildGraphAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>Validate the graph for cycles and single-ownership violations.</summary>
    Result<bool> ValidateGraph(CostGraphNode root);

    // ── Queries ─────────────────────────────────────────────────
    /// <summary>Get the most recent rollup run for a production order.</summary>
    Task<CostRollupRun?> GetLatestRunAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>Get all rollup runs for a production order.</summary>
    Task<IReadOnlyList<CostRollupRun>> GetRunsAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>Get lines for a specific rollup run.</summary>
    Task<IReadOnlyList<CostRollupLine>> GetLinesAsync(
        int rollupRunId,
        CancellationToken ct = default);

    /// <summary>Get exceptions for a specific rollup run.</summary>
    Task<IReadOnlyList<CostRollupException>> GetExceptionsAsync(
        int rollupRunId,
        CancellationToken ct = default);
}
