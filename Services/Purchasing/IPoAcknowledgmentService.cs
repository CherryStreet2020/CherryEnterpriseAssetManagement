// Sprint 15.4 PR-16 — PO Acknowledgment / Vendor Confirmation service.
//
// Spec ref: docs/research/purchasing-subcontracting-supply-demand-dean-research.txt
//   §15  Supplier acknowledgment + confirmed promise date
//   §23  POAcknowledgment entity surface
//   §24  Validations (cannot send PO without supplier ack on controlled material)
//
// Lifecycle:
//   Requested → Acknowledged → Confirmed | ConfirmedWithExceptions
//                                ↓
//                            (Rejected | Expired | Cancelled)
//
// Two-phase numbering pattern (Lesson 2, Session 19): RequestAcknowledgment
// saves with Guid placeholder for AcknowledgmentNumber, then patches it to
// POACK-YYYY-NNNNNN using the EF-assigned Id post-save. Eliminates
// CountAsync race on (CompanyId, AcknowledgmentNumber) tenant-unique
// constraint.
//
// PR-17 hook surface: ApproveExceptionAsync and the IsCurrent flag both feed
// the PR-17 PO amendment flow. When an amendment is approved, PR-17 will
// flip prior IsCurrent acks to false and create a fresh Requested ack via
// RequestAcknowledgmentAsync.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Purchasing;

// === Request DTOs ===

/// <summary>Inputs for <see cref="IPoAcknowledgmentService.RequestAcknowledgmentAsync"/>.</summary>
public sealed record RequestAcknowledgmentRequest(
    int PurchaseOrderId,
    POAcknowledgmentMethod RequestedMethod,
    System.DateTime? ResponseDueByUtc,
    int? RequestedByUserId,
    string? BuyerNotes);

/// <summary>Inputs for <see cref="IPoAcknowledgmentService.RecordAcknowledgmentAsync"/>.</summary>
public sealed record RecordAcknowledgmentRequest(
    int POAcknowledgmentId,
    POAcknowledgmentMethod Method,
    string? VendorContact,
    System.DateTime? OverallConfirmedPromiseDate,
    string? VendorNotes);

/// <summary>Inputs for <see cref="IPoAcknowledgmentService.RecordLineConfirmationAsync"/>.</summary>
public sealed record RecordLineConfirmationRequest(
    int POAcknowledgmentLineId,
    decimal ConfirmedQuantity,
    decimal ConfirmedUnitPrice,
    System.DateTime? ConfirmedPromiseDate,
    PoAckLineExceptionType ExceptionType,
    string? ExceptionReason);

/// <summary>Inputs for <see cref="IPoAcknowledgmentService.ApproveLineExceptionAsync"/>.</summary>
public sealed record ApproveLineExceptionRequest(
    int POAcknowledgmentLineId,
    int ApproverUserId,
    string? ApprovalNote);

/// <summary>Inputs for <see cref="IPoAcknowledgmentService.ConfirmAcknowledgmentAsync"/>.</summary>
public sealed record ConfirmAcknowledgmentRequest(
    int POAcknowledgmentId,
    string? BuyerNotes);

/// <summary>Inputs for <see cref="IPoAcknowledgmentService.RejectAcknowledgmentAsync"/>.</summary>
/// <remarks>
/// No RejectedByUserId field — vendor rejections come from the supplier side,
/// not an internal user. The reason text is the audit trail. If buyer-side
/// "buyer rejected vendor's terms" semantics are needed later (post PR-17),
/// a separate dedicated service op can be added.
/// </remarks>
public sealed record RejectAcknowledgmentRequest(
    int POAcknowledgmentId,
    string Reason);

// === Outcome records ===

/// <summary>Outcome of <see cref="IPoAcknowledgmentService.RequestAcknowledgmentAsync"/>.</summary>
public sealed record RequestAcknowledgmentResult(
    int POAcknowledgmentId,
    string AcknowledgmentNumber,
    int LinesInitialized,
    POAcknowledgmentStatus Status,
    string? Message);

/// <summary>Outcome of <see cref="IPoAcknowledgmentService.ConfirmAcknowledgmentAsync"/>.</summary>
public sealed record ConfirmAcknowledgmentResult(
    int POAcknowledgmentId,
    POAcknowledgmentStatus FinalStatus,
    int LinesAccepted,
    int LinesWithExceptions,
    int LinesApprovedExceptions,
    string? Message);

/// <summary>Read summary for a PO's current acknowledgment state.</summary>
public sealed record PoAcknowledgmentSummary(
    int? CurrentAcknowledgmentId,
    string? CurrentAcknowledgmentNumber,
    POAcknowledgmentStatus? CurrentStatus,
    System.DateTime? RequestedAtUtc,
    System.DateTime? AcknowledgedAtUtc,
    System.DateTime? ClosedAtUtc,
    System.DateTime? ResponseDueByUtc,
    int TotalAcknowledgmentsInHistory,
    int LinesTotal,
    int LinesAccepted,
    int LinesWithExceptions,
    int LinesExceptionsApproved,
    bool IsOverdue);

public interface IPoAcknowledgmentService
{
    /// <summary>
    /// Open a new acknowledgment cycle for the given PO. Creates a header in
    /// Requested status and initializes one POAcknowledgmentLine per active
    /// PurchaseOrderLine with snapshot qty / price / required-date. Flips any
    /// prior IsCurrent ack for the PO to false (history preserved).
    /// Two-phase numbering: header saves with a Guid placeholder for
    /// AcknowledgmentNumber, then post-save patch sets POACK-YYYY-NNNNNN
    /// using the EF-assigned Id.
    /// </summary>
    Task<Result<RequestAcknowledgmentResult>> RequestAcknowledgmentAsync(
        RequestAcknowledgmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Vendor confirms PO receipt (header-level). Moves ack from Requested →
    /// Acknowledged. Captures vendor contact + overall confirmed promise date.
    /// Does NOT confirm line details — RecordLineConfirmationAsync drives per-
    /// line confirmation. Returns the updated ack.
    /// </summary>
    Task<Result<POAcknowledgment>> RecordAcknowledgmentAsync(
        RecordAcknowledgmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Record vendor's confirmation for a single line. If ExceptionType=None
    /// and confirmed values match snapshots, IsAccepted=true. Otherwise the
    /// exception flag is stored and buyer must run ApproveLineExceptionAsync.
    /// Updates parent header's UpdatedAt.
    /// </summary>
    Task<Result<POAcknowledgmentLine>> RecordLineConfirmationAsync(
        RecordLineConfirmationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Buyer approves a vendor-raised line exception (date push, price diff,
    /// qty short, etc.). Stamps approver + timestamp. PR-17 will couple this
    /// approval to an automatic PO amendment so the PO header reflects the
    /// vendor-confirmed reality.
    /// </summary>
    Task<Result<POAcknowledgmentLine>> ApproveLineExceptionAsync(
        ApproveLineExceptionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Close the ack cycle. Rolls up per-line state to header status:
    ///   all IsAccepted + no exceptions       → Confirmed
    ///   any exception (approved or pending)  → ConfirmedWithExceptions
    /// Blocks if any unapproved exception remains (buyer must approve or
    /// reject first). Stamps ClosedAtUtc + AllLinesAcceptedAsOrdered.
    /// </summary>
    Task<Result<ConfirmAcknowledgmentResult>> ConfirmAcknowledgmentAsync(
        ConfirmAcknowledgmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Vendor explicitly refuses the PO. Moves ack to Rejected, stamps
    /// ClosedAtUtc, persists reason. PO header status untouched here — the
    /// buyer's response to a rejection is a separate workflow (Cancel PO,
    /// re-source via RFQ, etc.).
    /// </summary>
    Task<Result<POAcknowledgment>> RejectAcknowledgmentAsync(
        RejectAcknowledgmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// SLA scan helper. Moves all IsCurrent Requested/Acknowledged acks
    /// whose ResponseDueByUtc &lt; nowUtc to Expired. Returns count expired.
    /// </summary>
    Task<Result<int>> MarkExpiredAsync(
        System.DateTime nowUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Buyer cancels the open ack cycle (e.g., PO is being voided or
    /// rewritten). Only IsCurrent Requested/Acknowledged acks may be
    /// cancelled. Stamps ClosedAtUtc and IsCurrent=false.
    /// </summary>
    Task<Result<POAcknowledgment>> CancelAcknowledgmentAsync(
        int poAcknowledgmentId,
        string? reason,
        CancellationToken ct = default);

    /// <summary>Return the latest IsCurrent ack for the PO (null if never opened).</summary>
    Task<POAcknowledgment?> GetCurrentAsync(
        int purchaseOrderId,
        CancellationToken ct = default);

    /// <summary>
    /// Return the full ack history for a PO (most-recent first). PR-17 vendor
    /// re-acknowledgment loop consumes this to show the timeline.
    /// </summary>
    Task<IReadOnlyList<POAcknowledgment>> GetHistoryAsync(
        int purchaseOrderId,
        CancellationToken ct = default);

    /// <summary>One-shot KPI summary for a PO's ack state — drives the probe + Purchasing CC.</summary>
    Task<PoAcknowledgmentSummary> GetSummaryAsync(
        int purchaseOrderId,
        CancellationToken ct = default);
}
