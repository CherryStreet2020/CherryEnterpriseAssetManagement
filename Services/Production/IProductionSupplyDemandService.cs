// Sprint 15.1 PR-2 (2026-05-28) — IProductionSupplyDemandService.
//
// THE supply-demand orchestration service.
//
// Reads from ProductionSupplyDemand + ProductionSupplyAllocation.
// Writes new demand rows when a PRO is released. Refreshes supply status
// when linked records change. Allocates supply to demand. Releases allocations
// when supply is reassigned.
//
// Called from:
//   - PRO release workflow (GenerateDemandsFromProAsync)
//   - Purchasing CC (Wave 3 — read APIs)
//   - PO creation flow (Allocate after PO line created)
//   - Receipt flow (Refresh after receipt updates supply qty)
//   - Cron / refresh job (RefreshSupplyStatus for all unresolved)

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

// ── Request / result records ────────────────────────────────────────────

public sealed record GenerateDemandsResult(
    int ProductionOrderId,
    int DemandsCreated,
    int DemandsSkipped,
    int AllocationsCreated,
    string? Message);

public sealed record RefreshSupplyResult(
    int DemandId,
    DemandSourceStatus SourceStatus,
    DemandSupplyStatus SupplyStatus,
    DemandShortageStatus ShortageStatus,
    DemandCostStatus CostStatus,
    string? Message);

public sealed record AllocateSupplyRequest(
    int DemandId,
    AllocationSupplyType SupplyType,
    int SupplyRecordId,
    int? SupplyRecordLineId,
    decimal Quantity,
    System.DateTime? PromiseDate,
    string? Notes,
    string? CreatedBy);

public sealed record AllocateSupplyResult(
    int AllocationId,
    int DemandId,
    decimal AllocatedQuantity,
    decimal RemainingDemand,
    string? Message);

public sealed record ReleaseAllocationResult(
    int AllocationId,
    int DemandId,
    decimal QuantityReleased,
    string? Message);

// ── Interface ───────────────────────────────────────────────────────────

public interface IProductionSupplyDemandService
{
    /// <summary>
    /// Walk a Production Order's frozen BOM snapshot and create one
    /// ProductionSupplyDemand row per BOM line. Idempotent: skips lines
    /// that already have a demand row.
    /// </summary>
    Task<Result<GenerateDemandsResult>> GenerateDemandsFromProAsync(
        int productionOrderId,
        string? createdBy,
        CancellationToken ct = default);

    /// <summary>
    /// Refresh a single demand row — re-read linked supply records, recompute
    /// SupplyQuantity totals, recompute ShortageStatus / SupplyStatus /
    /// CostStatus from current state. Returns the new status quartet.
    /// </summary>
    Task<Result<RefreshSupplyResult>> RefreshSupplyStatusAsync(
        int demandId,
        CancellationToken ct = default);

    /// <summary>
    /// Refresh all demands for a Production Order in one pass. Returns the
    /// count of demands updated.
    /// </summary>
    Task<Result<int>> RefreshSupplyStatusForProAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>
    /// Get all demands for a PRO that have no supply or insufficient supply.
    /// Used by buyer/planner dashboards.
    /// </summary>
    Task<IReadOnlyList<ProductionSupplyDemand>> GetUnresolvedDemandsAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>
    /// Get all demands for a PRO regardless of resolution status.
    /// </summary>
    Task<IReadOnlyList<ProductionSupplyDemand>> GetDemandsForProAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>
    /// Create a ProductionSupplyAllocation linking this demand to a supply
    /// record. Updates demand's SupplyStatus and SuppliedQuantity. Idempotent
    /// per (DemandId, SupplyType, SupplyRecordId, SupplyRecordLineId).
    /// </summary>
    Task<Result<AllocateSupplyResult>> AllocateSupplyAsync(
        AllocateSupplyRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Release an allocation — supply is freed for use elsewhere. Demand's
    /// SupplyStatus rolls back accordingly.
    /// </summary>
    Task<Result<ReleaseAllocationResult>> ReleaseAllocationAsync(
        int allocationId,
        string? reasonNotes,
        string? releasedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Get all allocations attached to a specific demand.
    /// </summary>
    Task<IReadOnlyList<ProductionSupplyAllocation>> GetAllocationsForDemandAsync(
        int demandId,
        CancellationToken ct = default);
}
