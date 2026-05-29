// Sprint 14.4 PR-4 (2026-05-28) — Variance + Close Service interface.
//
// Two capabilities in one service:
//   1. Variance computation — 5 standard cost variances per Dean's spec
//   2. Close workflow — atomic close with variance JE posting
//
// Close workflow steps:
//   1. Verify PRO status is Completed (not already Closed)
//   2. Run rollup to refresh actual costs
//   3. Compute 5+ variances (estimated vs actual)
//   4. Post variance JEs via GlPostingHelpers
//   5. Clear WIP balance
//   6. Stamp variance columns on ProductionOrderCostSummary
//   7. Set FreezeCost = true
//   8. Set ProductionOrderStatus = Closed
//   9. Set ProductionCostStatus = ClosedSettled
//   10. Record ProductionCloseEvent for audit trail

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

/// <summary>Result of variance computation.</summary>
public sealed class VarianceComputationResult
{
    public IReadOnlyList<ProductionVariance> Variances { get; init; } = Array.Empty<ProductionVariance>();
    public decimal TotalVariance { get; init; }
    public int FavorableCount { get; init; }
    public int UnfavorableCount { get; init; }

    // B7 PR-3 — which baseline the actuals were measured against, so callers /
    // cockpit / AS9100 audit can show "vs locked PO estimate" for PoFirst orders
    // instead of implying an item-master standard that doesn't exist.
    public VarianceBaselineMode BaselineMode { get; init; } = VarianceBaselineMode.ItemMasterStandard;
    public DateTime? EstimateLockedUtc { get; init; }
}

/// <summary>Result of the close workflow.</summary>
public sealed class ProductionCloseResult
{
    public ProductionCloseEvent CloseEvent { get; init; } = null!;
    public IReadOnlyList<ProductionVariance> Variances { get; init; } = Array.Empty<ProductionVariance>();
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public interface IProductionVarianceCloseService
{
    // ── Variance computation ────────────────────────────────────

    /// <summary>
    /// Compute 5+ variances for a production order by comparing
    /// estimated cost to actual cost on the ProductionOrderCostSummary.
    /// Does NOT post JEs — use CloseAsync for that.
    /// </summary>
    Task<Result<VarianceComputationResult>> ComputeVariancesAsync(
        int productionOrderId,
        string? computedBy,
        CancellationToken ct = default);

    /// <summary>
    /// B7 PR-3 — lock the variance baseline for a Production Order. For a PoFirst
    /// (master-optional) order this sets <see cref="VarianceBaselineMode.LockedPoEstimate"/>
    /// and stamps <c>LockedEstimateCapturedUtc</c>, declaring the PO estimate frozen
    /// at release as the variance "standard" (decision #5). Creates the cost summary
    /// if one doesn't exist yet. Idempotent. Tenant-scoped.
    /// </summary>
    Task<Result<ProductionOrderCostSummary>> LockEstimateBaselineAsync(
        int productionOrderId,
        VarianceBaselineMode mode,
        string? lockedBy,
        CancellationToken ct = default);

    // ── Close workflow ──────────────────────────────────────────

    /// <summary>
    /// Full close workflow: verify status → refresh costs → compute variances
    /// → post variance JEs → clear WIP → freeze cost → set Closed status.
    /// Atomic — all or nothing.
    /// </summary>
    Task<Result<ProductionCloseResult>> CloseAsync(
        int productionOrderId,
        string? closedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Reopen a closed PRO for cost adjustment. Reverses the close event,
    /// sets status back to Completed, unfreezes cost.
    /// </summary>
    Task<Result<ProductionCloseEvent>> ReopenAsync(
        int productionOrderId,
        string reason,
        string? reopenedBy,
        CancellationToken ct = default);

    // ── Queries ─────────────────────────────────────────────────

    /// <summary>Get all variances for a production order.</summary>
    Task<IReadOnlyList<ProductionVariance>> GetVariancesAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>Get all close events for a production order.</summary>
    Task<IReadOnlyList<ProductionCloseEvent>> GetCloseEventsAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>Check if a PRO is ready to close (no blocking exceptions).</summary>
    Task<Result<bool>> CheckCloseReadinessAsync(
        int productionOrderId,
        CancellationToken ct = default);
}
