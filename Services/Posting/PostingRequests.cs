// ADR-025 D2 — Strongly-typed source-document request DTOs for the
// IPostingService<TSourceDoc> contract.
//
// Sprint 12.9 PR #2 — Each posting service implements
// IPostingService<TSourceDoc> at least once. The TSourceDoc names the
// logical posting operation. AP has 3 (approval, payment, void) so AP
// gets 3 request DTOs once the full refactor lands. PR #2 ships the
// PRIMARY post for each existing service: ApInvoiceApprovalRequest for
// AP, ReceiveGoodsRequest for Receiving. Secondary operations (payment,
// void, rejection reversal) keep their domain-specific method shapes
// on IApPostingService / IReceivingPostingService until subsequent PRs
// migrate them.
//
// Adding a new request DTO is the recommended way for callers to invoke
// a posting service. The legacy IApPostingService.PostApprovalAsync(int,
// bool, string) overload remains for backward compat but new callers
// should prefer:
//
//   var receipt = await _apPosting.PostAsync(
//       new ApInvoiceApprovalRequest(invoiceId, overrideMatch: false),
//       actorUserId,
//       Guid.NewGuid(),     // mint a new idempotency key per logical call
//       ct);

namespace Abs.FixedAssets.Services.Posting;

/// <summary>
/// Request DTO for posting an AP invoice approval. Implements the
/// <see cref="IPostingService{T}"/> contract on
/// <see cref="AccountsPayable.ApPostingService"/>.
/// </summary>
/// <param name="InvoiceId">
/// The <c>VendorInvoice.Id</c> to post. Service loads, three-way-matches,
/// builds the JE, and writes inventory if applicable.
/// </param>
/// <param name="OverrideMatch">
/// If true, allows posting when the three-way match status is Exception.
/// Admin-only path; audit trail records the override and the approving user.
/// </param>
/// <param name="ApproverUsername">
/// Display name of the user approving the post. Stored on the audit event
/// alongside the actorUserId for human-readable audit reports.
/// </param>
public sealed record ApInvoiceApprovalRequest(
    int InvoiceId,
    bool OverrideMatch = false,
    string ApproverUsername = "");

/// <summary>
/// Request DTO for posting a goods receipt. Implements the
/// <see cref="IPostingService{T}"/> contract on
/// <see cref="Receiving.ReceivingPostingService"/>.
/// </summary>
/// <param name="GoodsReceiptId">
/// The <c>GoodsReceipt.Id</c> to post. Service increments inventory for
/// stock items, debits the appropriate GL accounts, credits GR-Accrued,
/// and writes the inventory transaction ledger rows.
/// </param>
public sealed record ReceiveGoodsRequest(
    int GoodsReceiptId);
