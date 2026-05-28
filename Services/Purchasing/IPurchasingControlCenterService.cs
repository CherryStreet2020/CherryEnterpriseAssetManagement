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
// PR-12 TAB RECORDS — 4 tab-specific row shapes
//
// Each PR-12 tab renders data the generic 13-type ProductionSupplyDemand grid
// can't represent well — subcontract op lifecycle quartet, vendor-WIP qty
// buckets + aging, receipt lifecycle, and inspection-disposition holds across
// two heterogeneous receipt models. Each has its own row record + page record.
// ═══════════════════════════════════════════════════════════════════════════

public sealed record SubcontractTabRow(
    int SubcontractOperationId,
    int ProductionOrderId,
    string? ProductionOrderNumber,
    int OperationSequence,
    string OperationCode,
    string OperationDescription,
    int? SupplierId,
    string? SupplierName,
    int? ServicePurchaseOrderLineId,
    SubcontractOperationStatus OpStatus,
    SubcontractPoCreationStatus PoStatus,
    SubcontractShipmentStatus ShipmentStatus,
    SubcontractReceiptStatus ReceiptStatusForOp,
    decimal QuantityToShip,
    decimal QuantityShipped,
    decimal QuantityReceivedBack,
    decimal QuantityAccepted,
    decimal QuantityRejected,
    decimal QuantityScrappedAtVendor,
    DateTime? RequiredShipDate,
    DateTime? RequiredBackDate,
    int? DaysLateBack,
    bool CertRequired,
    bool InspectionOnReturn,
    string NextActionHint);

public sealed record SubcontractTabPage(
    int TotalCount,
    IReadOnlyList<SubcontractTabRow> Rows);

public sealed record VendorWipTabRow(
    int VendorWipBalanceId,
    int ProductionOrderId,
    string? ProductionOrderNumber,
    int OperationSequence,
    int SupplierId,
    string? SupplierName,
    int? VendorLocationId,
    string? VendorLocationDescription,
    string? PartNumber,
    string? Revision,
    string? LotNumber,
    decimal QuantityShipped,
    decimal QuantityAtVendor,
    decimal QuantityReceivedBack,
    decimal QuantityAccepted,
    decimal QuantityRejected,
    decimal QuantityScrappedAtVendor,
    decimal UnitValue,
    decimal TotalValueAtVendor,
    VendorWipInventoryStatus InventoryStatus,
    VendorWipQualityStatus QualityStatus,
    VendorWipOwnership Ownership,
    int AgingDaysAtVendor,
    DateTime? RequiredReturnDate,
    int? DaysLateReturn,
    DateTime? LastTransactionUtc);

public sealed record VendorWipTabPage(
    int TotalCount,
    decimal TotalValueAtVendorUsd,
    int OverdueReturnCount,
    IReadOnlyList<VendorWipTabRow> Rows);

public sealed record ReceiptsTabRow(
    int SubcontractReceiptId,
    string ReceiptNumber,
    int SubcontractOperationId,
    int ProductionOrderId,
    string? ProductionOrderNumber,
    int OperationSequence,
    int SupplierId,
    string? SupplierName,
    string? VendorPackingSlip,
    DateTime ReceiptDate,
    SubcontractReceiptLifecycle Status,
    bool CertReceived,
    bool InspectionRequired,
    bool ApprovalRequired,
    int LineCount,
    decimal TotalReceived,
    decimal TotalAccepted,
    decimal TotalRejected,
    decimal TotalScrappedAtVendor,
    DateTime? PostedUtc,
    string? ApprovedBy);

public sealed record ReceiptsTabPage(
    int TotalCount,
    int OpenDraftCount,
    int PendingApprovalCount,
    IReadOnlyList<ReceiptsTabRow> Rows);

public enum InspectionHoldSourceKind
{
    /// <summary>Standard incoming inspection (GoodsReceipt path).</summary>
    PurchaseOrderReceipt = 0,
    /// <summary>Subcontract receipt held pending inspection/cert/quality review.</summary>
    SubcontractReceipt = 1,
}

public sealed record InspectionHoldRow(
    InspectionHoldSourceKind SourceKind,
    int SourceLineId,
    int SourceHeaderId,
    string SourceHeaderNumber,
    int? PurchaseOrderId,
    string? PurchaseOrderNumber,
    int? ProductionOrderId,
    string? ProductionOrderNumber,
    int? OperationSequence,
    int? SupplierId,
    string? SupplierName,
    string? PartNumber,
    string? Revision,
    string? LotNumber,
    decimal QuantityReceived,
    decimal QuantityAccepted,
    decimal QuantityRejected,
    decimal QuantityOnHold,
    string HoldReason,
    DateTime ReceiptDate,
    int DaysOnHold,
    string? NcrReference,
    string NextActionHint);

public sealed record InspectionHoldsTabPage(
    int TotalCount,
    int OldHoldsCount,
    IReadOnlyList<InspectionHoldRow> Rows);

// ═══════════════════════════════════════════════════════════════════════════
// PR-13 TAB RECORDS — POs tab + Cost Exceptions tab. Expedites + Approvals
// reuse the existing PurchasingQueuePage shape (they dispatch through
// GetSupplyDemandQueueAsync with PurchasingQueueType.ExpediteRequired /
// .ApprovalRequired respectively, so they share the demand-grid UI).
// ═══════════════════════════════════════════════════════════════════════════

public sealed record PosTabRow(
    int PurchaseOrderId,
    string PoNumber,
    POStatus Status,
    int VendorId,
    string? VendorName,
    DateTime OrderDate,
    DateTime? RequiredDate,
    DateTime? PromiseDate,
    int? DaysLate,
    int LineCount,
    decimal Subtotal,
    decimal Total,
    string Currency,
    int? ShipToSiteId,
    int? RequestedById,
    int? ApprovedById,
    DateTime? ApprovedAt,
    int? CipProjectId,
    string? Notes,
    string NextActionHint);

public sealed record PosTabPage(
    int TotalCount,
    /// <summary>
    /// Currency-agnostic sum of PurchaseOrder.Total across visible POs in
    /// the filtered set. Multi-currency tenants will get a mixed-currency
    /// number — the page header should label "PO value" not "$ USD". When
    /// FX conversion is added (Wave 4 polish or beyond), this becomes a
    /// per-currency dictionary.
    /// </summary>
    decimal OpenTotalValue,
    int LateCount,
    int PendingApprovalCount,
    IReadOnlyList<PosTabRow> Rows);

public sealed record CostExceptionsTabPage(
    int TotalCount,
    int HighSeverityCount,
    int MediumSeverityCount,
    int LowSeverityCount,
    IReadOnlyList<PurchasingExceptionRow> Rows);

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

    // ═══════════════════════════════════════════════════════════════════════
    // PR-12 TAB READS — 4 tab-specific reads (§21 tabs 3-6)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// §21 tab 3 — Subcontract. List of active SubcontractOperations across
    /// PROs. Reads SubcontractOperation + Supplier + ProductionOrder. Filters
    /// out Closed ops. Used by the Subcontract tab on /Purchasing/ControlCenter.
    /// </summary>
    Task<Result<SubcontractTabPage>> GetSubcontractTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// §21 tab 4 — Vendor WIP. VendorWipBalance rows with QuantityAtVendor &gt; 0
    /// (i.e., material currently outside at a supplier). Includes aging +
    /// total value summaries used by the page header/footer.
    /// </summary>
    Task<Result<VendorWipTabPage>> GetVendorWipTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// §21 tab 5 — Receipts. SubcontractReceipt headers across all lifecycle
    /// states (Draft / Posting / Posted / PendingApproval / Approved /
    /// Reversed / Closed). Includes line aggregates per receipt.
    /// </summary>
    Task<Result<ReceiptsTabPage>> GetReceiptsTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// §21 tab 6 — Inspection Holds. Heterogeneous list of:
    ///   (a) GoodsReceiptLine rows with InspectionRequired = true AND any of:
    ///       direct-to-job awaiting post (DirectToJobPostedUtc IS NULL while
    ///       IsDirectToJob), parent receipt status Inspecting, or partial
    ///       inspection (QuantityReceived &gt; 0 and accepted+rejected &lt; received);
    ///   (b) SubcontractReceiptLine rows with disposition HoldForInspection /
    ///       HoldForDocs / HoldForQuality (PendingApproval lives on Receipts
    ///       tab via its own header counter, deliberately excluded here so a
    ///       row never counts on two tabs with conflicting next-action hints).
    /// Each row has a SourceKind discriminator so the UI can route detail
    /// links correctly. Skip/Take is split across the two source paths
    /// (Skip/2 + Take/2 each) and merged before sorting by days-on-hold desc.
    /// </summary>
    Task<Result<InspectionHoldsTabPage>> GetInspectionHoldsTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════════════════════
    // PR-13 TAB READS — §21 tabs 7-10 (POs / Cost Exceptions)
    //
    // Expedites (tab 8) + Approvals (tab 9) reuse GetSupplyDemandQueueAsync
    // with PurchasingQueueType.ExpediteRequired / .ApprovalRequired — they
    // are demand-grid views, no dedicated read needed.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// §21 tab 7 — POs. PurchaseOrder headers across active lifecycle states
    /// (excludes Closed + Cancelled). Includes page-level summaries: total
    /// open value, late count (PromiseDate or RequiredDate past today),
    /// pending-approval count.
    /// </summary>
    Task<Result<PosTabPage>> GetPosTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// §21 tab 10 — Cost Exceptions. Re-uses GetExceptionLaneAsync but
    /// wraps the result with severity-bucketed counters so the tab header
    /// can show "X total · Y high / Z medium / W low". The Rows payload is
    /// identical to the exception lane the §21 diagonal stripe consumes.
    /// </summary>
    Task<Result<CostExceptionsTabPage>> GetCostExceptionsTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default);
}
