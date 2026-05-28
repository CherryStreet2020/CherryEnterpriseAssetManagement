// =============================================================================
// CherryAI EAM — IPurchasingControlCenterService (Sprint 15.3 PR-10)
//
// THE service backing the Purchasing Control Center (§7 + §21).
//
// The Purchasing CC does NOT just show open POs. It shows UNRESOLVED SUPPLY
// DEMAND — every BOM line, routing operation, subcontract op, and project
// expense that needs an action from a buyer/planner. Wave 1 + Wave 2 shipped
// the substrate (ProductionSupplyDemand, ProductionSupplyAllocation,
// PurchaseOrderLineDemandLink, SubcontractOperation, VendorWipBalance,
// SubcontractShipment/Receipt, SubcontractFlowService). This service is the
// READ + WORKFLOW layer on top of that substrate that drives the §21 13-tab
// Command Center page (PR-11/12/13).
//
// Surface:
//   * KPI band — 5 tiles (open demand, open POs, vendor WIP value, late POs,
//     missing supply). Rendered above the tab strip.
//   * Supply Demand queue — 13 queue types from §7 dispatched via
//     PurchasingQueueType enum. Each queue applies a tab-specific filter to
//     ProductionSupplyDemand + adjacent supply/PO records.
//   * Exception lane — cost exceptions + supplier-performance alerts surfaced
//     as a separate dispatch (the diagonal stripe on the §21 layout).
//   * Lifecycle state machine — BuyerActionState transitions guarded by
//     business rules. A buyer cannot jump from Open → Closed; transitions
//     follow Open → Assigned → InProgress → AwaitingVendor / AwaitingApproval
//     → Resolved → Closed (Blocked + Cancelled as off-ramps).
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §7, §21
//   - docs/research/purchasing-cascade-design-2026-05-28.md Wave 3 PR-10
//   - Services/Receiving/IReceivingControlCenterService.cs (pattern reference)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Purchasing;

// ═══════════════════════════════════════════════════════════════════════════
// QUEUE TYPE — 13 from §7
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// The 13 queue types from Dean's §7 spec. Each dispatches to a different
/// filter combination over ProductionSupplyDemand + adjacent supply records.
/// </summary>
public enum PurchasingQueueType
{
    /// <summary>All unresolved demand from PROs/projects/inventory/MRP (§21 tab 1).</summary>
    SupplyDemand = 0,
    /// <summary>Job-specific BOM demand with no PO/reservation (§21 tab 2).</summary>
    BuyToJob = 1,
    /// <summary>Stock replenishment needed for job demand (§7 Buy-to-inventory).</summary>
    BuyToInventory = 2,
    /// <summary>Outside operations needing service POs (§21 tab 3).</summary>
    Subcontract = 3,
    /// <summary>Material currently outside at suppliers (§21 tab 7).</summary>
    VendorWip = 4,
    /// <summary>PO lines missing required dates / late to operation need date (§21 tab 6 subset).</summary>
    LatePoLines = 5,
    /// <summary>PO lines without confirmed promise dates from supplier.</summary>
    MissingSupplierPromise = 6,
    /// <summary>Received but not yet usable — inspection holds (§21 tab 9).</summary>
    InspectionBlocked = 7,
    /// <summary>Internally made components late — child WO supply risk.</summary>
    ChildWoSupplyRisk = 8,
    /// <summary>BOM line lacks supplier / source rule (§18 supplier missing).</summary>
    NoSourceDemand = 9,
    /// <summary>PO receipt/invoice/cost mismatch (§21 tab 12).</summary>
    CostExceptions = 10,
    /// <summary>PO/requisition needs approval (§21 tab 11).</summary>
    ApprovalRequired = 11,
    /// <summary>Demand has shortage or late supply — expedite needed (§21 tab 10).</summary>
    ExpediteRequired = 12,
    /// <summary>Supply blocking customer project milestone.</summary>
    ProjectCriticalSupply = 13,
}

/// <summary>
/// Buyer-state lifecycle transition action — guarded set per §24 rules.
/// Service rejects illegal transitions (e.g., Open → Closed must go through
/// at least Assigned).
/// </summary>
public enum BuyerActionTransition
{
    /// <summary>Open → Assigned. Buyer claims the demand.</summary>
    Assign = 0,
    /// <summary>Assigned/Open → InProgress. Buyer starts sourcing.</summary>
    StartWork = 1,
    /// <summary>InProgress → AwaitingVendor. PO sent, awaiting vendor action.</summary>
    SendToVendor = 2,
    /// <summary>InProgress/AwaitingVendor → AwaitingApproval. Needs management sign-off.</summary>
    RequestApproval = 3,
    /// <summary>AwaitingApproval → InProgress. Approver kicked it back.</summary>
    ApprovalDenied = 4,
    /// <summary>AwaitingApproval → AwaitingVendor. Approver signed off, proceed.</summary>
    ApprovalGranted = 5,
    /// <summary>Any → Resolved. Supply committed.</summary>
    MarkResolved = 6,
    /// <summary>Resolved → Closed. Demand fully satisfied + costed.</summary>
    Close = 7,
    /// <summary>Any → Blocked. Drawing/supplier/etc. blocks progress.</summary>
    Block = 8,
    /// <summary>Blocked → InProgress. Blocker cleared.</summary>
    Unblock = 9,
    /// <summary>Any → Cancelled. PRO cancelled, BOM removed, etc.</summary>
    Cancel = 10,
    /// <summary>Closed/Cancelled → Open. Admin reopen.</summary>
    Reopen = 11,
}

// ═══════════════════════════════════════════════════════════════════════════
// REQUEST + RESULT RECORDS
// ═══════════════════════════════════════════════════════════════════════════

public sealed record PurchasingKpiBand(
    int OpenDemandCount,
    /// <summary>
    /// Sum of UnitPrice * (Required - Received) for demands whose linked PO line
    /// is on an ACTIVE PO (excludes Cancelled + Closed). Renamed from
    /// "OpenDemandTotalValueUsd" per Wave 3 PR-10 review — the label now matches
    /// the math. Estimated open-demand value for un-PO'd demand is a separate
    /// concern delivered by PR-15 buyer recommendation engine.
    /// </summary>
    decimal CommittedSupplyValueUsd,
    int OpenPoCount,
    decimal OpenPoTotalValueUsd,
    decimal VendorWipTotalValueUsd,
    int LatePoCount,
    int MissingSupplyDemandCount,
    DateTime SnapshotUtc);

public sealed record PurchasingQueueFilter(
    int? CompanyId = null,
    int? SiteId = null,
    int? BuyerUserId = null,
    int? VendorId = null,
    int? ProductionOrderId = null,
    DateTime? RequiredBefore = null,
    bool IncludeClosedAndCancelled = false,
    int Skip = 0,
    int Take = 50);

public sealed record PurchasingQueueRow(
    int DemandId,
    string DemandNumber,
    PurchasingQueueType QueueType,
    int ProductionOrderId,
    string? ProductionOrderNumber,
    int? BomLineId,
    int? OperationSequence,
    int? ProjectId,
    int? CustomerId,
    string? PartNumber,
    string? Revision,
    string? Description,
    string? Uom,
    decimal RequiredQuantity,
    decimal OpenQuantity,
    DateTime? RequiredDate,
    SupplyPolicy SupplyPolicy,
    int? VendorId,
    int? BuyerUserId,
    int? LinkedPurchaseOrderId,
    DateTime? PromiseDate,
    int? DaysLate,
    DemandSourceStatus SourceStatus,
    DemandSupplyStatus SupplyStatus,
    DemandShortageStatus ShortageStatus,
    DemandCostStatus CostStatus,
    DemandAlertStatus AlertStatus,
    BuyerActionState BuyerActionState,
    string? NextActionHint);

public sealed record PurchasingQueuePage(
    PurchasingQueueType QueueType,
    int TotalCount,
    IReadOnlyList<PurchasingQueueRow> Rows);

public sealed record PurchasingExceptionRow(
    string ExceptionKind,
    int? DemandId,
    int? PurchaseOrderId,
    int? PurchaseOrderLineId,
    int? VendorId,
    string Severity,
    string Description,
    DateTime DetectedUtc,
    decimal? CostImpactUsd);

public sealed record PurchasingExceptionLane(
    int TotalCount,
    IReadOnlyList<PurchasingExceptionRow> Rows);

public sealed record PurchasingLifecycleState(
    int DemandId,
    BuyerActionState State,
    string StateLabel,
    IReadOnlyList<BuyerActionTransition> AllowedTransitions,
    string NextActionHint);

public sealed record TransitionLifecycleRequest(
    int DemandId,
    BuyerActionTransition Action,
    string? Notes,
    int? UserId,
    string? UserName);

public sealed record TransitionLifecycleResult(
    int DemandId,
    BuyerActionState PreviousState,
    BuyerActionState NewState,
    DateTime TransitionedAtUtc,
    string? Notes);

// ═══════════════════════════════════════════════════════════════════════════
// SERVICE INTERFACE
// ═══════════════════════════════════════════════════════════════════════════

public interface IPurchasingControlCenterService
{
    /// <summary>
    /// 5-tile KPI band rendered above the §21 tab strip. Tenant-scoped via
    /// ITenantContext. Snapshot is point-in-time; caller can cache for short
    /// periods on the page header.
    /// </summary>
    Task<Result<PurchasingKpiBand>> GetKpiBandAsync(
        int? siteId = null,
        CancellationToken ct = default);

    /// <summary>
    /// 13-type queue dispatch (§7). Returns paged rows + total count for the
    /// requested queue. NextActionHint per row drives the §18 buyer
    /// recommendation column.
    /// </summary>
    Task<Result<PurchasingQueuePage>> GetSupplyDemandQueueAsync(
        PurchasingQueueType queueType,
        PurchasingQueueFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// Cost exceptions + supplier performance alerts. Drives the diagonal
    /// exception lane on the §21 layout. Severity is High / Medium / Low.
    /// </summary>
    Task<Result<PurchasingExceptionLane>> GetExceptionLaneAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// Resolve current lifecycle state + allowed transitions for one demand.
    /// AllowedTransitions enforces the state machine — UI hides illegal
    /// buttons. NextActionHint is the §18 recommendation.
    /// </summary>
    Task<Result<PurchasingLifecycleState>> GetLifecycleStateAsync(
        int demandId,
        CancellationToken ct = default);

    /// <summary>
    /// Apply a guarded lifecycle transition. Service rejects illegal moves
    /// (e.g., Open → Closed) with Result.Failure. Updates
    /// BuyerActionState + BuyerActionStateUpdatedUtc + audit fields in one
    /// SaveChanges.
    /// </summary>
    Task<Result<TransitionLifecycleResult>> TransitionLifecycleAsync(
        TransitionLifecycleRequest request,
        CancellationToken ct = default);
}
