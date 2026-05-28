// =============================================================================
// CherryAI EAM — IDemandConsolidationService (Sprint 15.3 PR-14)
//
// Implements Dean's spec §17 — the 6-mode demand consolidation planner.
//
// "This is where ETO/MTO gets tricky."  — Dean
//
// Walks a set of ProductionSupplyDemand records and produces a structured
// ConsolidationPlan that says: which demands go into which PO line, with
// per-demand allocations preserved so the receiving + costing layers can
// settle receipts back to the originating jobs.
//
// This is a PURE PLANNING service. It does NOT create PO lines. The plan
// is consumed by:
//   - IPurchasingRecommendationService (PR-15) — suggests Consolidate vs
//     Strict to the buyer
//   - IPurchasingService.AddLineAsync (existing) — the write path that
//     actually creates the PO + PurchaseOrderLineDemandLink entries
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §17
//   - docs/research/purchasing-cascade-design-2026-05-28.md Wave 3 PR-14
//   - Models/PurchaseOrderLineDemandLink.cs (the traceability substrate)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Purchasing;

// ═══════════════════════════════════════════════════════════════════════════
// 6 CONSOLIDATION MODES — direct map to §17 grid
// ═══════════════════════════════════════════════════════════════════════════

public enum DemandConsolidationMode
{
    /// <summary>One PO line per job/BOM/operation. No grouping.</summary>
    StrictJobSpecific = 0,
    /// <summary>Combine demands with same Supplier + RequiredDate bucket; preserve demand links.</summary>
    SupplierDate = 1,
    /// <summary>Combine demands within the same Project/Customer.</summary>
    Project = 2,
    /// <summary>Buy to stock for multiple demands — single PO line, post-receipt allocation to all.</summary>
    Inventory = 3,
    /// <summary>Combine outside-processing operations by Vendor + Process/ServiceItem.</summary>
    SubcontractBatch = 4,
    /// <summary>Keep every demand separate (alias of StrictJobSpecific for callers that want explicit "no").</summary>
    None = 5,
}

// ═══════════════════════════════════════════════════════════════════════════
// PLAN RECORDS
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// One allocation from a planned PO line back to the originating demand.
/// Becomes a PurchaseOrderLineDemandLink row when the plan executes.
/// </summary>
public sealed record ConsolidationAllocation(
    int DemandId,
    string DemandNumber,
    int ProductionOrderId,
    string? ProductionOrderNumber,
    int? BomLineId,
    int? OperationSequence,
    int? ProjectId,
    decimal AllocatedQuantity);

/// <summary>
/// One planned PO line. May reference a single demand (Strict mode) or many
/// (consolidated modes). The sum of <c>Allocations[*].AllocatedQuantity</c>
/// equals <c>PlannedQuantity</c>.
/// </summary>
public sealed record ConsolidationPlanLine(
    int LineNumber,
    int? ItemId,
    string? PartNumber,
    string? Revision,
    int VendorId,
    string? VendorName,
    decimal PlannedQuantity,
    string? Uom,
    DateTime? RequiredDate,
    DateTime? PromiseDate,
    int? ProjectId,
    int? ServiceItemId,
    string? Notes,
    IReadOnlyList<ConsolidationAllocation> Allocations);

/// <summary>
/// Complete plan for a consolidation request. Caller validates + executes
/// via IPurchasingService.
/// </summary>
public sealed record ConsolidationPlan(
    DemandConsolidationMode Mode,
    int InputDemandCount,
    int PlannedLineCount,
    int SkippedDemandCount,
    IReadOnlyList<ConsolidationPlanLine> Lines,
    IReadOnlyList<int> SkippedDemandIds,
    string? Notes);

public sealed record ConsolidationRequest(
    IReadOnlyList<int> DemandIds,
    DemandConsolidationMode Mode,
    int? DefaultVendorId = null,
    int? RequiredDateBucketDays = null,
    string? Notes = null);

// ═══════════════════════════════════════════════════════════════════════════
// SERVICE INTERFACE
// ═══════════════════════════════════════════════════════════════════════════

public interface IDemandConsolidationService
{
    /// <summary>
    /// Plan a consolidation across the given demand IDs using the requested
    /// mode. Returns the planned PO lines + per-demand allocations. Read-only
    /// — no writes to the database. Caller invokes IPurchasingService to
    /// actually create the PO header + lines + demand links.
    /// </summary>
    Task<Result<ConsolidationPlan>> PlanAsync(
        ConsolidationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Plan a consolidation across every open demand on a Production Order
    /// using the requested mode. Convenience wrapper around PlanAsync.
    /// </summary>
    Task<Result<ConsolidationPlan>> PlanForProductionOrderAsync(
        int productionOrderId,
        DemandConsolidationMode mode,
        CancellationToken ct = default);

    /// <summary>
    /// Suggest the best consolidation mode for a given set of demands based
    /// on their characteristics (same vendor? same project? subcontract?).
    /// Returns mode + reason so the buyer can accept or override.
    /// </summary>
    Task<Result<(DemandConsolidationMode Mode, string Reason)>> SuggestModeAsync(
        IReadOnlyList<int> demandIds,
        CancellationToken ct = default);
}
