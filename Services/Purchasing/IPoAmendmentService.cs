// Sprint 15.4 PR-17 — PO Amendment / Change Order service interface.
//
// Spec ref: docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §22, §15
//           project_wave4_enhancement_decisions_2026_05_28.md (Dean's PR-17 enhancement)
//
// THE BIC DIFFERENTIATOR — demand-link impact preview + auto-resync.
//
// Lifecycle:
//   Draft → Previewed → PendingApproval → Approved → Applied
//                                           ↓
//                                       Rejected | Cancelled
//
// Two-phase numbering for POAMD-YYYY-NNNNNN (Lesson 2, Session 19).
//
// PR-16 hook: ApplyAmendmentAsync flips the prior POAcknowledgment IsCurrent
// → false and (when VendorReAcknowledgmentRequired is true) opens a new
// Requested ack via IPoAcknowledgmentService.RequestAcknowledgmentAsync.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Purchasing;

// ═══════════════════════════════════════════════════════════════════════════
// Request DTOs
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>One proposed change on a single PO line, drafted by the buyer.</summary>
public sealed record AmendmentLineDraft(
    int? PurchaseOrderLineId,   // null for NewLine
    POAmendmentLineChangeType ChangeType,
    decimal NewQuantity,
    decimal NewUnitPrice,
    System.DateTime? NewPromiseDate,
    System.DateTime? NewRequiredDate,
    string? LineNarrative);

/// <summary>Inputs for <see cref="IPoAmendmentService.DraftAmendmentAsync"/>.</summary>
public sealed record DraftAmendmentRequest(
    int PurchaseOrderId,
    POChangeReason Reason,
    string? ReasonNarrative,
    int? DraftedByUserId,
    bool VendorReAcknowledgmentRequired,
    IReadOnlyList<AmendmentLineDraft> Lines);

/// <summary>Inputs for <see cref="IPoAmendmentService.ApproveAmendmentAsync"/>.</summary>
public sealed record ApproveAmendmentRequest(
    int POChangeHistoryId,
    int ApproverUserId,
    string? ApprovalNote);

/// <summary>Inputs for <see cref="IPoAmendmentService.RejectAmendmentAsync"/>.</summary>
public sealed record RejectAmendmentRequest(
    int POChangeHistoryId,
    int RejecterUserId,
    string Reason);

// ═══════════════════════════════════════════════════════════════════════════
// Outcome / read DTOs
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Outcome of <see cref="IPoAmendmentService.DraftAmendmentAsync"/>.</summary>
public sealed record DraftAmendmentResult(
    int POChangeHistoryId,
    string AmendmentNumber,
    int LinesDrafted,
    POAmendmentStatus Status,
    string? Message);

/// <summary>One row of impact for a single affected demand link.</summary>
public sealed record AmendmentImpactRow(
    int PurchaseOrderLineId,
    int PoLineNumber,
    int? ProductionSupplyDemandId,
    int? ProductionOrderId,
    string? ProductionOrderNumber,
    int? BomLineId,
    int? OperationSequence,
    decimal AllocatedQuantity,
    decimal RemainingQuantity,
    System.DateTime? CurrentPromiseDate,
    System.DateTime? NewPromiseDate,
    System.DateTime? NeedByDate,
    int? PushOutDays,
    bool ShipDateAtRisk,
    string? Narrative);

/// <summary>
/// Aggregate report returned by <see cref="IPoAmendmentService.PreviewAmendmentImpactAsync"/>.
/// This is the BIC differentiator data structure — the impact partial renders
/// this list before the buyer hits Approve.
/// </summary>
public sealed record AmendmentImpactReport(
    int POChangeHistoryId,
    string AmendmentNumber,
    int PurchaseOrderId,
    string? PurchaseOrderNumber,
    int LinesChanged,
    int AffectedDemandLinks,
    int AffectedProductionOrders,
    int AffectedOperations,
    bool ShipDateRiskFlag,
    decimal TotalValueDelta,
    decimal TotalQuantityDelta,
    string? ImpactNarrative,
    IReadOnlyList<AmendmentImpactRow> Rows);

/// <summary>Outcome of <see cref="IPoAmendmentService.ApplyAmendmentAsync"/>.</summary>
public sealed record ApplyAmendmentResult(
    int POChangeHistoryId,
    int PoLinesUpdated,
    int DemandLinksResynced,
    int ProductionSupplyAllocationsResynced,
    bool VendorAckFlipped,
    int? NewVendorAcknowledgmentId,
    string? NewVendorAcknowledgmentNumber,
    POAmendmentStatus Status,
    string? Message);

/// <summary>One-shot summary for the Purchasing CC Expedites tab.</summary>
public sealed record PoAmendmentSummary(
    int? CurrentAmendmentId,
    string? CurrentAmendmentNumber,
    POAmendmentStatus? CurrentStatus,
    POChangeReason? CurrentReason,
    int TotalAmendmentsInHistory,
    int LinesInCurrent,
    int AffectedDemandLinksInCurrent,
    int AffectedProductionOrdersInCurrent,
    bool ShipDateRiskFlag,
    decimal TotalValueDelta);

// ═══════════════════════════════════════════════════════════════════════════
// Service surface
// ═══════════════════════════════════════════════════════════════════════════

public interface IPoAmendmentService
{
    /// <summary>
    /// Open a new amendment cycle for the PO. Inserts header + per-line
    /// snapshots with the buyer's proposed new values. Flips any prior
    /// IsCurrent amendment to false (history preserved). Two-phase numbering
    /// assigns POAMD-YYYY-NNNNNN post-save. Atomic.
    /// </summary>
    Task<Result<DraftAmendmentResult>> DraftAmendmentAsync(
        DraftAmendmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// **BIC differentiator.** Walks the demand-link graph for every changed
    /// line and computes:
    ///   - Affected PurchaseOrderLineDemandLink count
    ///   - Affected ProductionOrders + Operations
    ///   - Per-link promise-date push-out vs. NeedByDate (ship-date risk)
    ///   - Aggregate value delta + qty delta
    /// Caches results on the header (counts) + per-line rows + ImpactNarrative
    /// + sets Status → Previewed. Idempotent — re-running re-computes from the
    /// current line snapshots.
    /// </summary>
    Task<Result<AmendmentImpactReport>> PreviewAmendmentImpactAsync(
        int poChangeHistoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Move the amendment from Previewed → PendingApproval (so the approver
    /// queue picks it up). Optional — buyers can also Approve directly.
    /// </summary>
    Task<Result<POChangeHistory>> SubmitForApprovalAsync(
        int poChangeHistoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Approver decision: Approved. Status → Approved; stamps approver +
    /// timestamp. Does NOT yet mutate PO lines — that happens at Apply.
    /// </summary>
    Task<Result<POChangeHistory>> ApproveAmendmentAsync(
        ApproveAmendmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Approver decision: Rejected. Status → Rejected; persists reason. PO
    /// header / lines / demand links untouched.
    /// </summary>
    Task<Result<POChangeHistory>> RejectAmendmentAsync(
        RejectAmendmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// **The atomic core of the enhancement.** Inside a single transaction:
    ///   1) Update PurchaseOrderLine qty / price / promise date / line totals
    ///   2) Recompute PO header Subtotal + Total
    ///   3) Re-sync PurchaseOrderLineDemandLink AllocatedQuantity / PromiseDate
    ///      / RemainingQuantity (preserving §17 traceability)
    ///   4) Mirror the resync to ProductionSupplyAllocation (generic table)
    ///   5) If VendorReAcknowledgmentRequired: flip the current POAcknowledgment
    ///      IsCurrent → false and open a new Requested ack on the PO via
    ///      IPoAcknowledgmentService (PR-16 hook)
    ///   6) Stamp AppliedAtUtc; Status → Applied; ClosedAtUtc
    /// On any failure the whole transaction rolls back — never a partial apply.
    /// </summary>
    Task<Result<ApplyAmendmentResult>> ApplyAmendmentAsync(
        int poChangeHistoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Buyer abandons the draft before approval. Status → Cancelled;
    /// ClosedAtUtc stamped. IsCurrent stays true (P2-9 semantic) until the
    /// next DraftAmendmentAsync opens a fresh cycle.
    /// </summary>
    Task<Result<POChangeHistory>> CancelAmendmentAsync(
        int poChangeHistoryId,
        string? reason,
        CancellationToken ct = default);

    // ── Read methods ────────────────────────────────────────────────────────

    /// <summary>Return the latest IsCurrent amendment for the PO (null if none).</summary>
    Task<POChangeHistory?> GetCurrentAsync(
        int purchaseOrderId,
        CancellationToken ct = default);

    /// <summary>Full amendment history for the PO, most-recent first.</summary>
    Task<IReadOnlyList<POChangeHistory>> GetHistoryAsync(
        int purchaseOrderId,
        CancellationToken ct = default);

    /// <summary>Snapshot summary for the Purchasing CC Expedites tab / probe KPI band.</summary>
    Task<PoAmendmentSummary> GetSummaryAsync(
        int purchaseOrderId,
        CancellationToken ct = default);

    /// <summary>Re-read the impact report for an already-previewed amendment without recompute.</summary>
    Task<Result<AmendmentImpactReport>> GetImpactReportAsync(
        int poChangeHistoryId,
        CancellationToken ct = default);
}
