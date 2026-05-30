// B8 PR-PRO-8 (2026-05-27) — PRO Cockpit data aggregation service.
//
// Composes data from all prior PRO services into the shapes needed by
// the /Production/Orders/{id}/Cockpit Control Center surface:
//   - 16-metric summary bar (order header + progress + readiness + cost)
//   - BOM grid (24-column view with supply link status)
//   - Routing grid (22-column view with readiness checks)
//   - Per-tab data (Labor, Quality, Docs, Cost, etc.)
//
// This is a READ-ONLY aggregation service. All mutations route through
// the domain services (IProductionMaterialTransactionService,
// IProductionOperationTransactionService, IProductionWipMoveService,
// IProductionCompletionService, IOperationReadinessService).

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    // ================================================================
    // SUMMARY BAR — 16 metrics
    // ================================================================

    public sealed record CockpitSummaryBar(
        // Identity
        string OrderNumber,
        string? PartNumber,
        string? Revision,
        string? Description,
        ProductionOrderStatus Status,
        // Dates
        DateTime? DueDate,
        int? DaysLate,
        // Quantities
        decimal QuantityOrdered,
        decimal QuantityCompleted,
        decimal QuantityScrapped,
        decimal QuantityRework,
        decimal QuantityRemaining,
        // Progress
        decimal MaterialReadinessPercent,
        decimal OperationProgressPercent,
        // Cost
        decimal WipCost,
        // Quality + alerts
        int OpenQualityHolds,
        int OpenMaterialShortages,
        int ActiveOperators,
        string? CurrentOperation,
        string? NextOperation);

    // ================================================================
    // BOM GRID ROW — 24 columns per spec §7
    // ================================================================

    public sealed record CockpitBomRow(
        int Id,
        int Line,
        int? OperationSequence,
        string PartNumber,
        string? Description,
        string? Revision,
        decimal RequiredQty,
        decimal RequiredInclScrap,
        string? Uom,
        decimal Available,
        decimal Reserved,
        decimal Picked,
        decimal Staged,
        decimal Issued,
        decimal Consumed,
        decimal RemainingToIssue,
        decimal Short,
        string? SourceLocation,
        string? LotSerial,
        string SupplyType,
        bool Backflush,
        bool SubstituteAllowed,
        string Status,
        decimal Cost,
        // Supply link fields (PR-PRO-7)
        string? SupplyLinkDescription,
        string SupplyRisk);

    // ================================================================
    // ROUTING GRID ROW — 22 columns per spec §7
    // ================================================================

    public sealed record CockpitRoutingRow(
        int Id,
        int Sequence,
        string? OperationCode,
        string? Description,
        string? WorkCenterName,
        string? ResourceName,
        string Status,
        DateTime? PlannedStart,
        DateTime? PlannedFinish,
        DateTime? ActualStart,
        DateTime? ActualFinish,
        decimal SetupEstimate,
        decimal SetupActual,
        decimal RunEstimate,
        decimal RunActual,
        decimal LaborEstimate,
        decimal LaborActual,
        decimal GoodQty,
        decimal ScrapQty,
        decimal ReworkQty,
        decimal RemainingQty,
        string MaterialReady,
        bool InspectionRequired,
        string? ActiveEmployee,
        string? LastActivity,
        // Readiness (PR-PRO-7)
        string ReadinessStatus,
        string? ReadinessSummary);

    // ================================================================
    // COCKPIT DATA BUNDLE — everything the page needs
    // ================================================================

    public sealed record CockpitData(
        ProductionOrder Order,
        CockpitSummaryBar Summary,
        IReadOnlyList<CockpitBomRow> BomRows,
        IReadOnlyList<CockpitRoutingRow> RoutingRows,
        ProductionOrderReadiness? Readiness);

    // ================================================================
    // MAKE/BUY PANEL — B7 Wave D PR-1
    // ================================================================

    /// <summary>The resolved make-or-buy decision for the cockpit panel: the explainable
    /// result plus the item identity + audit context + resolved supplier name.</summary>
    public sealed record CockpitMakeBuyPanelData(
        MakeBuyDecisionResult Result,
        string PartNumber,
        string? Description,
        DateTime? DecidedAtUtc,
        MakeBuyDecisionContext Context,
        string? SupplierName);

    /// <summary>Either the panel data, or a human empty-state reason when there's nothing to show.</summary>
    public sealed record CockpitMakeBuyPanel(
        CockpitMakeBuyPanelData? Data,
        string? EmptyReason);

    public interface IProductionCockpitService
    {
        /// <summary>
        /// Load the full cockpit data bundle for a production order.
        /// Single round-trip aggregation from all domain services.
        /// </summary>
        Task<Result<CockpitData>> GetCockpitDataAsync(
            int productionOrderId, CancellationToken ct = default);

        /// <summary>
        /// Load just the summary bar (for lightweight refresh).
        /// </summary>
        Task<Result<CockpitSummaryBar>> GetSummaryBarAsync(
            int productionOrderId, CancellationToken ct = default);

        /// <summary>
        /// Load just the BOM grid rows.
        /// </summary>
        Task<Result<IReadOnlyList<CockpitBomRow>>> GetBomGridAsync(
            int productionOrderId, CancellationToken ct = default);

        /// <summary>
        /// Load just the routing grid rows.
        /// </summary>
        Task<Result<IReadOnlyList<CockpitRoutingRow>>> GetRoutingGridAsync(
            int productionOrderId, CancellationToken ct = default);

        /// <summary>
        /// B7 Wave D PR-1 — resolve the PRO item's latest persisted make-or-buy decision
        /// (re-hydrated via <see cref="IMakeBuyDecisionService.ExplainAsync"/>) for the
        /// cockpit "why did we make vs buy this?" panel. Read-only, tenant-scoped. Returns
        /// a panel with a null <c>Data</c> + an <c>EmptyReason</c> when the order has no item
        /// or no decision has been recorded yet.
        /// </summary>
        Task<Result<CockpitMakeBuyPanel>> GetMakeBuyPanelAsync(
            int productionOrderId, CancellationToken ct = default);
    }
}
