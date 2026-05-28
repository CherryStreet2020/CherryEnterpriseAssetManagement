// =============================================================================
// CherryAI EAM — IAutoPurchaseService (Sprint 15.3 PR-14)
//
// Implements Dean's spec §16 — the "should we auto-create a PO?" decision
// engine. Walks each ProductionSupplyDemand, evaluates 10 trigger rules and
// 12 blocker rules, and returns a structured AutoPoEvaluation per demand.
//
// This is a PURE READ-SIDE service. It does NOT create POs. PR-15 wires the
// recommendation engine that consumes these evaluations and surfaces them in
// the Purchasing CC "Suggested Action" column. Actual PO creation flows
// through IPurchasingService (existing) — preserving the principle that
// every PO write goes through one typed service.
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §16
//   - docs/research/purchasing-cascade-design-2026-05-28.md Wave 3 PR-14
//   - Services/Purchasing/IPurchasingControlCenterService.cs (read pattern)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Purchasing;

// ═══════════════════════════════════════════════════════════════════════════
// TRIGGER + BLOCKER ENUMS — direct mapping to §16 grid
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// §16 §"Auto-create PO candidate when" — 10 trigger conditions. The engine
/// returns ALL triggers that fire for a given demand (multiple may fire
/// simultaneously, e.g. a child-job buy-to-job demand from a released PRO
/// that has a material shortage).
/// </summary>
public enum AutoPoTrigger
{
    /// <summary>Production Order is released and BOM line source type = buy.</summary>
    ProductionOrderReleasedBuyLine = 0,
    /// <summary>Routing operation is subcontract — service PO needed.</summary>
    SubcontractOperationExists = 1,
    /// <summary>Operation has a material shortage that must be sourced.</summary>
    OperationMaterialShortage = 2,
    /// <summary>Child job requires buy-to-job material (the parent's BOM line is direct-to-job).</summary>
    ChildJobBuyToJobMaterial = 3,
    /// <summary>Project long-lead material has cleared approval.</summary>
    ProjectLongLeadApproved = 4,
    /// <summary>Engineering released a new BOM revision adding lines.</summary>
    BomRevisionReleased = 5,
    /// <summary>Change order added material or service to existing PRO.</summary>
    ChangeOrderAddedMaterial = 6,
    /// <summary>Scrap or rework requires replacement material.</summary>
    ScrapReworkReplacement = 7,
    /// <summary>Buyer manually requested PO creation from a PRO/BOM line.</summary>
    BuyerManualRequest = 8,
    /// <summary>Demand explicitly tagged for purchase (legacy/imported MRP signal).</summary>
    ExplicitlyTaggedForPurchase = 9,
}

/// <summary>
/// §16 "Do not auto-create PO when" — 12 blocker conditions. The engine
/// returns ALL blockers that fire (multiple may fire — e.g. drawing not
/// approved AND supplier not approved). Any non-empty blockers list means
/// the demand cannot auto-buy; the auto-create stays gated.
/// </summary>
public enum AutoPoBlocker
{
    /// <summary>BOM line not released — engineering hold.</summary>
    BomLineNotReleased = 0,
    /// <summary>Drawing or revision not approved.</summary>
    DrawingRevisionNotApproved = 1,
    /// <summary>Supplier not approved (no AVL or fails AVL check).</summary>
    SupplierNotApproved = 2,
    /// <summary>Buy quantity below threshold without buyer override rule.</summary>
    BelowApprovalThresholdNoRule = 3,
    /// <summary>Material is customer-owned (will arrive from customer).</summary>
    CustomerOwnedMaterial = 4,
    /// <summary>Inventory available and reservable — reserve instead.</summary>
    InventoryAvailableReservable = 5,
    /// <summary>Existing PO can satisfy this demand — link instead.</summary>
    ExistingPoCanSatisfy = 6,
    /// <summary>Existing child WO can satisfy demand (make-direct-to-job).</summary>
    ExistingChildWoCanSatisfy = 7,
    /// <summary>Project or contract not approved — sales/finance gate.</summary>
    ProjectOrContractNotApproved = 8,
    /// <summary>ITAR / export-control rule blocks the supplier.</summary>
    ItarExportBlocksSupplier = 9,
    /// <summary>Budget approval still pending.</summary>
    BudgetApprovalPending = 10,
    /// <summary>Required operation on hold (engineering / quality).</summary>
    RequiredOperationOnHold = 11,
}

/// <summary>
/// Bottom-line decision after running every trigger and blocker.
/// </summary>
public enum AutoPoDecision
{
    /// <summary>One or more triggers fire and zero blockers — auto-create is safe.</summary>
    Eligible = 0,
    /// <summary>One or more triggers fire but at least one blocker also fires — manual review required.</summary>
    BlockedReviewRequired = 1,
    /// <summary>No triggers fire — demand doesn't qualify for auto-create on this evaluation.</summary>
    NotEligible = 2,
    /// <summary>Demand already has a linked supply (existing PO/child WO/reservation) — no new PO needed.</summary>
    AlreadySatisfied = 3,
}

// ═══════════════════════════════════════════════════════════════════════════
// RESULT RECORDS
// ═══════════════════════════════════════════════════════════════════════════

public sealed record AutoPoCandidate(
    int DemandId,
    string DemandNumber,
    int ProductionOrderId,
    string? ProductionOrderNumber,
    int? BomLineId,
    int? OperationSequence,
    string? PartNumber,
    decimal RequiredQuantity,
    decimal RemainingQuantity,
    DateTime? RequiredDate,
    int? SuggestedVendorId,
    string? SuggestedVendorName,
    AutoPoDecision Decision,
    IReadOnlyList<AutoPoTrigger> Triggers,
    IReadOnlyList<AutoPoBlocker> Blockers,
    string Summary);

public sealed record AutoPoCandidatePage(
    int TotalCount,
    int EligibleCount,
    int BlockedCount,
    IReadOnlyList<AutoPoCandidate> Candidates);

public sealed record AutoPoEvaluationFilter(
    int? CompanyId = null,
    int? SiteId = null,
    int? VendorId = null,
    int? ProductionOrderId = null,
    int? BuyerUserId = null,
    bool EligibleOnly = false,
    int Skip = 0,
    int Take = 50);

// ═══════════════════════════════════════════════════════════════════════════
// SERVICE INTERFACE
// ═══════════════════════════════════════════════════════════════════════════

public interface IAutoPurchaseService
{
    /// <summary>
    /// Evaluate a single demand line against the §16 trigger/blocker matrix.
    /// Returns the full evaluation including every trigger that fires + every
    /// blocker that fires. Caller can show this in the buyer recommendation
    /// drawer / Purchasing CC "Why?" column.
    /// </summary>
    Task<Result<AutoPoCandidate>> EvaluateDemandAsync(
        int demandId,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluate every open demand on a Production Order. Returns one candidate
    /// per demand. Used by the Production Cockpit "Auto-PO candidates" panel
    /// and by IPurchasingRecommendationService (PR-15) when computing per-PRO
    /// next-action hints.
    /// </summary>
    Task<Result<IReadOnlyList<AutoPoCandidate>>> EvaluateProductionOrderAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>
    /// Walk the tenant's open demand backlog and return every demand for
    /// which auto-create is currently eligible (or BlockedReviewRequired if
    /// triggers fire but blockers gate them). Used by the Purchasing CC
    /// "Auto-PO candidates" exception lane and by the daily MRP run.
    /// </summary>
    Task<Result<AutoPoCandidatePage>> GetCandidatesAsync(
        AutoPoEvaluationFilter filter,
        CancellationToken ct = default);
}
