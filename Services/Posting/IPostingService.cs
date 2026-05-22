// ADR-025 D2 — IPostingService<TSourceDoc> base interface contract.
//
// Sprint 12.9 PR #2 — Locks the pattern every posting service implements.
// Catches the future Sprint 13 (Purchasing) / Sprint 14 (Maintenance) /
// any future posting service BEFORE they freestyle their own shape.
//
// Each existing posting service (ApPostingService, ReceivingPostingService,
// CipCapitalizationService, CapitalImprovementPostingService,
// DepreciationService) implements this interface once per "logical post
// operation" via a TSourceDoc-parametrized request DTO.
//
// Pattern:
//
//   public sealed record ApInvoiceApprovalRequest(int InvoiceId, bool OverrideMatch);
//   public sealed class ApPostingService :
//       IApPostingService,
//       IPostingService<ApInvoiceApprovalRequest>
//   {
//       Task<Result<PostingReceipt>> IPostingService<ApInvoiceApprovalRequest>.PostAsync(
//           ApInvoiceApprovalRequest source,
//           int actorUserId,
//           Guid idempotencyKey,
//           CancellationToken ct) { ... }
//   }
//
// The IPostingService<T>.PostAsync method goes through IIdempotencyMediator
// (ADR-014 D4) for the idempotency guarantee. Inside the mediator's locked
// scope, the inner work:
//
//   1. Calls IPeriodGuard.CanPostAsync(companyId, postingDate) — fail if closed
//   2. Builds the JournalEntry + JournalLines with SourceModule + SourceDocumentId
//   3. Asserts debits == credits before SaveChangesAsync
//   4. Writes an AuditService event with the flat-DTO pattern
//   5. Writes an OutboxEvent in the same transaction (downstream-relevant mutations)
//
// All 5 guarantees collapse into one place. New posting services inherit
// the rigor automatically by implementing the interface.
//
// References:
//   - docs/ADR-025-service-layer-standard.md (D2 — original definition)
//   - docs/ADR-025-posting-service-contract.md (D2 amendment — this PR)
//   - MASTER_PLAN.md Priority 1.6080 (Sprint 12.9 PR #2)

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Posting;

/// <summary>
/// Contract every service that creates JournalEntry rows or mutates
/// inventory ledgers MUST implement (at least once) per ADR-025 D2.
/// Each implementation is parametrized by a strongly-typed source-document
/// request DTO that names the logical posting operation.
/// </summary>
/// <typeparam name="TSourceDoc">
/// The strongly-typed request DTO. Examples:
/// <see cref="ApInvoiceApprovalRequest"/>, <see cref="ReceiveGoodsRequest"/>.
/// </typeparam>
public interface IPostingService<TSourceDoc>
{
    /// <summary>
    /// Post the source document idempotently.
    /// </summary>
    /// <remarks>
    /// <para>Implementations MUST guarantee the following before returning success:</para>
    /// <list type="number">
    ///   <item>Idempotency key check via <see cref="Infrastructure.IIdempotencyMediator"/>.
    ///   Same (actorUserId, idempotencyKey) + same request hash returns the cached
    ///   response. Different hash returns failure.</item>
    ///   <item>Period guard via <see cref="IPeriodGuard.CanPostAsync"/>.
    ///   Failure if the company's fiscal period for the posting date is closed.</item>
    ///   <item>Balanced JE validation. <c>SUM(debits) == SUM(credits)</c> asserted
    ///   before <c>SaveChangesAsync</c>. Failure if not balanced.</item>
    ///   <item>Source document reference on every <c>JournalLine</c>:
    ///   <c>SourceModule</c>, <c>SourceDocumentId</c>, <c>SourceDocumentNo</c>,
    ///   <c>SourceLineId</c> (where applicable).</item>
    ///   <item>Audit event via <c>AuditService.LogAsync(...)</c> using the
    ///   flat-DTO pattern (see <c>feedback_audit_log_serialization</c>).</item>
    /// </list>
    /// <para>For downstream-relevant mutations, implementations SHOULD also write
    /// an <c>OutboxEvent</c> in the same transaction. Most existing services already
    /// do this via <see cref="Webhooks.IOutboxWriter"/>.</para>
    /// </remarks>
    /// <param name="source">The strongly-typed request DTO naming the posting operation.</param>
    /// <param name="actorUserId">The authenticated user performing the post. Required for
    /// idempotency-key scoping (same key from different users = different rows).</param>
    /// <param name="idempotencyKey">Client-minted UUID per logical operation.
    /// Retries with the same UUID return the cached response.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> wrapping the <see cref="PostingReceipt"/> on success,
    /// or an error string on expected failure (validation, period closed, match
    /// exception, etc.). Unexpected failures (DB down, network) throw.
    /// </returns>
    Task<Result<PostingReceipt>> PostAsync(
        TSourceDoc source,
        int actorUserId,
        Guid idempotencyKey,
        CancellationToken ct);
}

/// <summary>
/// The generic outcome of a successful posting operation. Specific posting
/// services may also expose richer per-operation result types (e.g.
/// <c>ApPostingResult</c>, <c>ReceivingPostingResult</c>) — those remain
/// available on the service's domain-specific interface
/// (<c>IApPostingService</c>, <c>IReceivingPostingService</c>) for callers
/// that need the extra fidelity. The generic envelope is what
/// <see cref="IPostingService{T}"/> consumers see.
/// </summary>
/// <param name="JournalEntryId">
/// The new <c>JournalEntry.Id</c> created by this post, or the cached one
/// when <paramref name="WasReplay"/> is true. May be null for "no-op"
/// outcomes (e.g. nothing to reverse).
/// </param>
/// <param name="LinesPosted">
/// Count of <c>JournalLine</c> rows the post created (cached value on replay).
/// </param>
/// <param name="TotalDebits">
/// Sum of debits across all lines. Equal to <paramref name="TotalCredits"/>
/// for a balanced post.
/// </param>
/// <param name="TotalCredits">
/// Sum of credits across all lines. Equal to <paramref name="TotalDebits"/>
/// for a balanced post.
/// </param>
/// <param name="WasReplay">
/// True if the idempotency mediator returned a cached response from a prior
/// successful post with the same (actorUserId, idempotencyKey). For v1 of
/// the contract this is always false from the inner work's perspective; a
/// future enhancement may surface replay detection via the mediator API.
/// </param>
/// <param name="AuditEventId">
/// The <c>AuditEvent</c> row Id written by the post, or null for legacy
/// code paths that have not yet been refactored to emit a typed audit event.
/// </param>
public sealed record PostingReceipt(
    int? JournalEntryId,
    int LinesPosted,
    decimal TotalDebits,
    decimal TotalCredits,
    bool WasReplay,
    string? AuditEventId);
