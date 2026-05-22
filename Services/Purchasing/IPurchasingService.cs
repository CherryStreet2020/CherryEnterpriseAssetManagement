// ADR-025 D5 — IPurchasingService (Sprint 12.9 PR #4).
//
// Centralizes write paths off Pages/Purchasing/Details.cshtml.cs (12 direct
// SaveChangesAsync calls per the 2026-05-20 audit, 2nd-worst offender).
//
// Unlike IWorkOrderService, this surface has zero embedded JournalEntry
// construction or inventory movement — PO lifecycle ends at "Approved";
// JE posting happens downstream at Receiving (GR/IR accrual) and AP
// (invoice posting). The Approve/Reject paths still orchestrate
// IApprovalService + IOutboxWriter but those are already services.
//
// De-risks Sprint 13 (Purchasing Control Center) which builds its
// IPurchasingControlCenterService on top of this contract.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Approvals;

namespace Abs.FixedAssets.Services.Purchasing;

/// <summary>
/// Domain service for PurchaseOrder mutations (Sprint 12.9 PR #4).
/// New PageModel callers should inject <see cref="IPurchasingService"/>
/// instead of AppDbContext for any PO mutation.
/// </summary>
public interface IPurchasingService
{
    // === Draft-state CRUD (rejected once PO leaves Draft) ===

    /// <summary>Update the PO header. Draft-only.</summary>
    Task<Result<PurchaseOrder>> UpdateHeaderAsync(UpdatePoHeaderRequest request, CancellationToken ct);

    /// <summary>Add a line to a Draft PO; recomputes Subtotal + Total.</summary>
    Task<Result<PurchaseOrderLine>> AddLineAsync(AddPoLineRequest request, CancellationToken ct);

    /// <summary>Update an existing line's quantity + unit price; recomputes Total.</summary>
    Task<Result<PurchaseOrderLine>> UpdateLineAsync(UpdatePoLineRequest request, CancellationToken ct);

    /// <summary>Delete a Draft PO line; cascades to its releases.</summary>
    Task<Result<int>> DeleteLineAsync(DeletePoLineRequest request, CancellationToken ct);

    /// <summary>Add a delivery release to a Draft PO line. Bounded by remaining quantity.</summary>
    Task<Result<PurchaseOrderRelease>> AddReleaseAsync(AddPoReleaseRequest request, CancellationToken ct);

    /// <summary>Delete a release from a Draft PO line.</summary>
    Task<Result<int>> DeleteReleaseAsync(DeletePoReleaseRequest request, CancellationToken ct);

    // === Lifecycle transitions ===

    /// <summary>Submit a Draft PO for approval. No-op for non-Draft.</summary>
    Task<Result<PurchaseOrder>> SubmitForApprovalAsync(int purchaseOrderId, CancellationToken ct);

    /// <summary>
    /// Record an Approve decision via <see cref="IApprovalService"/>. Handles
    /// the full SoD + N-of-M outcome surface — see <see cref="ApprovePoOutcome"/>.
    /// On <see cref="ApprovalOutcome.FullyApproved"/> or
    /// <see cref="ApprovalOutcome.NoWorkflowApplicable"/>, transitions PO to
    /// Approved and publishes <c>PoApprovedV1</c> on the outbox.
    /// </summary>
    Task<Result<ApprovePoOutcome>> ApproveAsync(ApprovePoRequest request, CancellationToken ct);

    /// <summary>
    /// Record a Reject decision. On full rejection, lands the PO back in
    /// Draft so the requester can revise + resubmit.
    /// </summary>
    Task<Result<RejectPoOutcome>> RejectAsync(RejectPoRequest request, CancellationToken ct);

    // === Lifecycle helpers ===

    /// <summary>
    /// Deep-copy an existing PO into a new Draft. Copies header + all lines;
    /// assigns a new PONumber via the same yy-NNNNN sequence the legacy
    /// page used. Returns the new PO so the caller can redirect.
    /// </summary>
    Task<Result<PurchaseOrder>> DuplicatePoAsync(int sourcePurchaseOrderId, CancellationToken ct);

    /// <summary>
    /// Hard-delete a Draft PO with all lines + releases cascaded. No-op
    /// for non-Draft. Returns true if delete happened, false if rejected.
    /// </summary>
    Task<Result<bool>> DeletePoAsync(int purchaseOrderId, CancellationToken ct);
}

// === Request DTOs ===

/// <summary>Inputs for <see cref="IPurchasingService.UpdateHeaderAsync"/>.</summary>
public sealed record UpdatePoHeaderRequest(
    int PurchaseOrderId,
    int VendorId,
    int POTypeLookupValueId,
    System.DateTime OrderDate,
    System.DateTime? RequiredDate,
    string? Notes,
    int? CipProjectId);

/// <summary>Inputs for <see cref="IPurchasingService.AddLineAsync"/>.</summary>
public sealed record AddPoLineRequest(
    int PurchaseOrderId,
    int? ItemId,
    string Description,
    string? PartNumber,
    string? MfrPartNumber,
    string? VendorPartNumber,
    string? Revision,
    string Uom,
    decimal Quantity,
    decimal UnitPrice,
    int? GlAccountId,
    string? Notes);

/// <summary>Inputs for <see cref="IPurchasingService.UpdateLineAsync"/>.</summary>
public sealed record UpdatePoLineRequest(
    int PurchaseOrderId,
    int LineId,
    decimal Quantity,
    decimal UnitPrice);

/// <summary>Inputs for <see cref="IPurchasingService.DeleteLineAsync"/>.</summary>
public sealed record DeletePoLineRequest(int PurchaseOrderId, int LineId);

/// <summary>Inputs for <see cref="IPurchasingService.AddReleaseAsync"/>.</summary>
public sealed record AddPoReleaseRequest(
    int PurchaseOrderId,
    int LineId,
    decimal Quantity,
    int? ShipToLocationId,
    System.DateTime? DueDate,
    string? Notes);

/// <summary>Inputs for <see cref="IPurchasingService.DeleteReleaseAsync"/>.</summary>
public sealed record DeletePoReleaseRequest(int PurchaseOrderId, int LineId, int ReleaseId);

/// <summary>Inputs for <see cref="IPurchasingService.ApproveAsync"/>.</summary>
public sealed record ApprovePoRequest(
    int PurchaseOrderId,
    string ApproverUsername,
    System.Collections.Generic.IReadOnlyList<string> ApproverRoles,
    string? Comment);

/// <summary>Inputs for <see cref="IPurchasingService.RejectAsync"/>.</summary>
public sealed record RejectPoRequest(
    int PurchaseOrderId,
    string ApproverUsername,
    System.Collections.Generic.IReadOnlyList<string> ApproverRoles,
    string? Comment);

// === Outcome records ===

/// <summary>Outcome of <see cref="IPurchasingService.ApproveAsync"/>.</summary>
public sealed record ApprovePoOutcome(
    int PurchaseOrderId,
    string? PoNumber,
    ApprovePoStatus Status,
    int ApprovalsRecorded,
    int ApprovalsRequired,
    string? Message);

/// <summary>Status flag for <see cref="ApprovePoOutcome"/>.</summary>
public enum ApprovePoStatus
{
    /// <summary>PO fully approved; status set to Approved; PoApprovedV1 published. TempData["StatusMessage"].</summary>
    Approved,
    /// <summary>Approval recorded but more approvers needed. TempData["StatusMessage"].</summary>
    PartiallyApproved,
    /// <summary>Approval blocked by SoD / DuplicateApprover / InsufficientRole. TempData["ErrorMessage"].</summary>
    Rejected
}

/// <summary>Outcome of <see cref="IPurchasingService.RejectAsync"/>.</summary>
public sealed record RejectPoOutcome(
    int PurchaseOrderId,
    string? PoNumber,
    RejectPoStatus Status,
    string? Message);

/// <summary>Status flag for <see cref="RejectPoOutcome"/>.</summary>
public enum RejectPoStatus
{
    /// <summary>Rejection recorded; PO returned to Draft. TempData["StatusMessage"].</summary>
    Rejected,
    /// <summary>Rejection blocked by SoD / InsufficientRole. TempData["ErrorMessage"].</summary>
    Blocked
}
