// Sprint 15.4 PR-16 — IPoAcknowledgmentService implementation.
//
// Patterns adopted (Wave 1+2 reinforcement):
//   - ITenantContext.VisibleCompanyIds gating on every read/write.
//   - Result.Success / Result.Failure return shape (consistent with sibling
//     purchasing services: IPurchasingService, IVendorWipService, etc.).
//   - Two-phase numbering for AcknowledgmentNumber (Lesson 2, Session 19):
//     save with Guid placeholder, then patch POACK-YYYY-NNNNNN using
//     EF-assigned Id post-save. Zero CountAsync race.
//   - Multi-line ops wrapped in BeginTransactionAsync per Wave 1+2 lock.
//   - IsCurrent management: only one IsCurrent ack per PO at a time.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public class PoAcknowledgmentService : IPoAcknowledgmentService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PoAcknowledgmentService> _logger;

    public PoAcknowledgmentService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<PoAcknowledgmentService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1) RequestAcknowledgmentAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<RequestAcknowledgmentResult>> RequestAcknowledgmentAsync(
        RequestAcknowledgmentRequest request, CancellationToken ct = default)
    {
        var po = await _db.Set<PurchaseOrder>()
            .Include(p => p.Lines)
            .Where(p => p.Id == request.PurchaseOrderId)
            .FirstOrDefaultAsync(ct);
        if (po == null)
            return Result.Failure<RequestAcknowledgmentResult>(
                $"PurchaseOrder {request.PurchaseOrderId} not found.");

        if (po.CompanyId == null ||
            !_tenantContext.VisibleCompanyIds.Contains(po.CompanyId.Value))
            return Result.Failure<RequestAcknowledgmentResult>(
                $"PurchaseOrder {request.PurchaseOrderId} out of tenant scope.");

        if (po.Lines == null || po.Lines.Count == 0)
            return Result.Failure<RequestAcknowledgmentResult>(
                "Cannot request acknowledgment on a PO with no lines.");

        // P2-6: pre-compute open lines BEFORE entering the transaction so
        // the all-closed-PO case (Lines.Count > 0 but every line IsClosed)
        // returns a clean error without an inserted-then-rolled-back header.
        var openPoLines = po.Lines.Where(l => !l.IsClosed).OrderBy(l => l.LineNumber).ToList();
        if (openPoLines.Count == 0)
            return Result.Failure<RequestAcknowledgmentResult>(
                "Cannot request acknowledgment — all PO lines are closed.");

        // Only Sent / Approved / PartiallyReceived POs are ack-eligible.
        // Draft / PendingApproval / Closed / Cancelled / Received / Invoiced — not.
        if (po.Status != POStatus.Sent &&
            po.Status != POStatus.Approved &&
            po.Status != POStatus.PartiallyReceived)
        {
            return Result.Failure<RequestAcknowledgmentResult>(
                $"PO status {po.Status} is not ack-eligible " +
                "(must be Approved, Sent, or PartiallyReceived).");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var nowUtc = DateTime.UtcNow;

            // Flip prior IsCurrent ack (if any) to false so only one
            // IsCurrent ack exists per PO at a time.
            var priorCurrent = await _db.Set<POAcknowledgment>()
                .Where(a => a.PurchaseOrderId == po.Id && a.IsCurrent)
                .ToListAsync(ct);
            foreach (var prior in priorCurrent)
            {
                prior.IsCurrent = false;
                prior.UpdatedAt = nowUtc;
            }

            // Phase 1: insert header with Guid placeholder for the unique
            // AcknowledgmentNumber (eliminates CountAsync race on the
            // tenant-unique constraint). Guid "N" format = 32 chars, "TMP-"
            // prefix = 4, total 36 chars — well under the 40-char column
            // limit. No Substring needed.
            var header = new POAcknowledgment
            {
                CompanyId = po.CompanyId,
                PurchaseOrderId = po.Id,
                AcknowledgmentNumber = $"TMP-{Guid.NewGuid():N}",
                Status = POAcknowledgmentStatus.Requested,
                Method = request.RequestedMethod,
                RequestedAtUtc = nowUtc,
                ResponseDueByUtc = request.ResponseDueByUtc,
                RequestedByUserId = request.RequestedByUserId,
                BuyerNotes = request.BuyerNotes,
                IsCurrent = true,
                AllLinesAcceptedAsOrdered = false,
                CreatedAt = nowUtc,
            };
            _db.Add(header);

            // Initialize a line for every open PO line (snapshot
            // OrderedQuantity / OrderedUnitPrice / RequiredDate).
            // openPoLines computed BEFORE the transaction (P2-6 fix) is
            // already filtered + ordered by LineNumber.
            foreach (var poLine in openPoLines)
            {
                var ackLine = new POAcknowledgmentLine
                {
                    POAcknowledgment = header,
                    PurchaseOrderLineId = poLine.Id,
                    OrderedQuantity = poLine.QuantityOrdered,
                    ConfirmedQuantity = poLine.QuantityOrdered, // default to as-ordered
                    OrderedUnitPrice = poLine.UnitPrice,
                    ConfirmedUnitPrice = poLine.UnitPrice,
                    RequiredDate = poLine.RequiredDate,
                    ConfirmedPromiseDate = poLine.RequiredDate,
                    IsAccepted = false, // becomes true upon vendor confirmation
                    ExceptionType = PoAckLineExceptionType.None,
                    CreatedAt = nowUtc,
                };
                _db.Add(ackLine);
            }
            var linesInitialized = openPoLines.Count;

            await _db.SaveChangesAsync(ct);

            // Phase 2: patch the AcknowledgmentNumber using the EF-assigned Id.
            header.AcknowledgmentNumber = BuildAckNumber(nowUtc.Year, header.Id);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return Result.Success(new RequestAcknowledgmentResult(
                header.Id,
                header.AcknowledgmentNumber,
                linesInitialized,
                header.Status,
                $"Acknowledgment {header.AcknowledgmentNumber} requested " +
                $"for PO {po.PONumber} with {linesInitialized} line(s)."));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "RequestAcknowledgmentAsync failed for PO {PurchaseOrderId}",
                request.PurchaseOrderId);
            return Result.Failure<RequestAcknowledgmentResult>(
                $"Failed to create acknowledgment: {ex.Message}");
        }
    }

    private static string BuildAckNumber(int year, int id)
        => $"POACK-{year:0000}-{id:000000}";

    // ═══════════════════════════════════════════════════════════════════════
    // 2) RecordAcknowledgmentAsync — vendor confirms PO receipt (header)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POAcknowledgment>> RecordAcknowledgmentAsync(
        RecordAcknowledgmentRequest request, CancellationToken ct = default)
    {
        var ack = await LoadAckWithScopeAsync(request.POAcknowledgmentId, ct);
        if (ack == null)
            return Result.Failure<POAcknowledgment>(
                $"POAcknowledgment {request.POAcknowledgmentId} not found or out of tenant scope.");

        if (ack.Status != POAcknowledgmentStatus.Requested)
            return Result.Failure<POAcknowledgment>(
                $"Cannot record vendor ack from status {ack.Status} — must be Requested.");

        // Codex P2 (PRRT_kwDOSSj3Wc6Fg48K): reject writes on non-current acks.
        // A vendor response that arrives after a fresh cycle was opened would
        // otherwise mutate the historical row and corrupt the audit trail.
        if (!ack.IsCurrent)
            return Result.Failure<POAcknowledgment>(
                $"Ack #{ack.Id} is no longer the current cycle for its PO — refusing to mutate a historical record.");

        var nowUtc = DateTime.UtcNow;
        ack.Status = POAcknowledgmentStatus.Acknowledged;
        ack.Method = request.Method;
        ack.AcknowledgedAtUtc = nowUtc;
        ack.AcknowledgedByVendorContact = request.VendorContact;
        ack.ConfirmedPromiseDate = request.OverallConfirmedPromiseDate;
        ack.VendorNotes = AppendNote(ack.VendorNotes, request.VendorNotes);
        ack.UpdatedAt = nowUtc;

        await _db.SaveChangesAsync(ct);
        return Result.Success(ack);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3) RecordLineConfirmationAsync — per-line vendor confirmation
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POAcknowledgmentLine>> RecordLineConfirmationAsync(
        RecordLineConfirmationRequest request, CancellationToken ct = default)
    {
        if (request.ConfirmedQuantity < 0m)
            return Result.Failure<POAcknowledgmentLine>(
                "ConfirmedQuantity cannot be negative.");
        if (request.ConfirmedUnitPrice < 0m)
            return Result.Failure<POAcknowledgmentLine>(
                "ConfirmedUnitPrice cannot be negative.");

        var line = await _db.Set<POAcknowledgmentLine>()
            .Include(l => l.POAcknowledgment)
            .Where(l => l.Id == request.POAcknowledgmentLineId)
            .FirstOrDefaultAsync(ct);
        if (line == null)
            return Result.Failure<POAcknowledgmentLine>(
                $"POAcknowledgmentLine {request.POAcknowledgmentLineId} not found.");

        var ack = line.POAcknowledgment;
        if (ack.CompanyId == null ||
            !_tenantContext.VisibleCompanyIds.Contains(ack.CompanyId.Value))
            return Result.Failure<POAcknowledgmentLine>(
                "Line out of tenant scope.");

        if (ack.Status != POAcknowledgmentStatus.Requested &&
            ack.Status != POAcknowledgmentStatus.Acknowledged)
            return Result.Failure<POAcknowledgmentLine>(
                $"Cannot record line confirmation in ack status {ack.Status}.");

        // Codex P2: refuse writes on non-current acks (PRRT_kwDOSSj3Wc6Fg48K).
        if (!ack.IsCurrent)
            return Result.Failure<POAcknowledgmentLine>(
                $"Ack #{ack.Id} is no longer the current cycle for its PO — refusing to mutate a historical record.");

        // P1-3 / P2-11: snapshot-vs-confirmed mismatch must require an
        // explicit ExceptionType. Vendor cannot pass ExceptionType=None with
        // different qty/price/date and silently land as IsAccepted=false but
        // unflagged (which would block confirm with the P1-2 untouched
        // guard, but still misclassifies the data).
        var qtyMatches = request.ConfirmedQuantity == line.OrderedQuantity;
        var priceMatches = request.ConfirmedUnitPrice == line.OrderedUnitPrice;
        // Date matches iff BOTH null OR both equal — null required + non-null
        // promise (or vice versa) is NOT a match.
        var dateMatches = line.RequiredDate == request.ConfirmedPromiseDate;
        var anyMismatch = !qtyMatches || !priceMatches || !dateMatches;

        if (anyMismatch && request.ExceptionType == PoAckLineExceptionType.None)
            return Result.Failure<POAcknowledgmentLine>(
                "Confirmed values differ from ordered snapshots " +
                $"(qty {(qtyMatches ? "OK" : "differs")}, " +
                $"price {(priceMatches ? "OK" : "differs")}, " +
                $"date {(dateMatches ? "OK" : "differs")}) " +
                "— provide an explicit ExceptionType (QuantityShort / PriceDifference / DatePushOut / etc.).");

        var nowUtc = DateTime.UtcNow;
        line.ConfirmedQuantity = request.ConfirmedQuantity;
        line.ConfirmedUnitPrice = request.ConfirmedUnitPrice;
        line.ConfirmedPromiseDate = request.ConfirmedPromiseDate;
        line.ExceptionType = request.ExceptionType;
        line.ExceptionReason = request.ExceptionReason;

        // IsAccepted iff exception is None AND every snapshot matches.
        line.IsAccepted =
            request.ExceptionType == PoAckLineExceptionType.None &&
            qtyMatches && priceMatches && dateMatches;

        line.UpdatedAt = nowUtc;
        ack.UpdatedAt = nowUtc;

        await _db.SaveChangesAsync(ct);
        return Result.Success(line);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4) ApproveLineExceptionAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POAcknowledgmentLine>> ApproveLineExceptionAsync(
        ApproveLineExceptionRequest request, CancellationToken ct = default)
    {
        var line = await _db.Set<POAcknowledgmentLine>()
            .Include(l => l.POAcknowledgment)
            .Where(l => l.Id == request.POAcknowledgmentLineId)
            .FirstOrDefaultAsync(ct);
        if (line == null)
            return Result.Failure<POAcknowledgmentLine>(
                $"POAcknowledgmentLine {request.POAcknowledgmentLineId} not found.");

        if (line.POAcknowledgment.CompanyId == null ||
            !_tenantContext.VisibleCompanyIds.Contains(line.POAcknowledgment.CompanyId.Value))
            return Result.Failure<POAcknowledgmentLine>("Line out of tenant scope.");

        if (line.ExceptionType == PoAckLineExceptionType.None)
            return Result.Failure<POAcknowledgmentLine>(
                "Line has no exception to approve (ExceptionType=None).");

        if (line.ExceptionApproved)
            return Result.Failure<POAcknowledgmentLine>(
                "Line exception already approved (idempotency guard).");

        // Codex P2: refuse writes on non-current acks (PRRT_kwDOSSj3Wc6Fg48K).
        if (!line.POAcknowledgment.IsCurrent)
            return Result.Failure<POAcknowledgmentLine>(
                $"Ack #{line.POAcknowledgment.Id} is no longer the current cycle for its PO — refusing to approve exception on a historical record.");

        var nowUtc = DateTime.UtcNow;
        line.ExceptionApproved = true;
        line.ExceptionApprovedByUserId = request.ApproverUserId;
        line.ExceptionApprovedAtUtc = nowUtc;
        line.ApprovalNote = request.ApprovalNote;
        line.UpdatedAt = nowUtc;
        line.POAcknowledgment.UpdatedAt = nowUtc;

        await _db.SaveChangesAsync(ct);
        return Result.Success(line);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5) ConfirmAcknowledgmentAsync — roll up to terminal state
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<ConfirmAcknowledgmentResult>> ConfirmAcknowledgmentAsync(
        ConfirmAcknowledgmentRequest request, CancellationToken ct = default)
    {
        var ack = await LoadAckWithLinesAsync(request.POAcknowledgmentId, ct);
        if (ack == null)
            return Result.Failure<ConfirmAcknowledgmentResult>(
                $"POAcknowledgment {request.POAcknowledgmentId} not found or out of tenant scope.");

        if (ack.Status != POAcknowledgmentStatus.Requested &&
            ack.Status != POAcknowledgmentStatus.Acknowledged)
            return Result.Failure<ConfirmAcknowledgmentResult>(
                $"Cannot confirm from status {ack.Status} — must be Requested or Acknowledged.");

        // Codex P2: refuse confirm on non-current acks (PRRT_kwDOSSj3Wc6Fg48K).
        if (!ack.IsCurrent)
            return Result.Failure<ConfirmAcknowledgmentResult>(
                $"Ack #{ack.Id} is no longer the current cycle for its PO — refusing to confirm a historical record.");

        if (ack.Lines == null || ack.Lines.Count == 0)
            return Result.Failure<ConfirmAcknowledgmentResult>(
                "Cannot confirm an ack with zero lines.");

        var nowUtc = DateTime.UtcNow;
        var accepted = ack.Lines.Count(l => l.IsAccepted);
        var withExceptions = ack.Lines.Count(l => l.ExceptionType != PoAckLineExceptionType.None);
        var approvedExceptions = ack.Lines.Count(
            l => l.ExceptionType != PoAckLineExceptionType.None && l.ExceptionApproved);
        var unapprovedExceptions = withExceptions - approvedExceptions;

        // P1-2 guard: lines that were never confirmed (IsAccepted=false AND
        // ExceptionType=None) must NOT silently roll up as success. Each line
        // must be either accepted-as-ordered or flagged with an exception
        // before the ack can close.
        var untouched = ack.Lines.Count(
            l => !l.IsAccepted && l.ExceptionType == PoAckLineExceptionType.None);
        if (untouched > 0)
            return Result.Failure<ConfirmAcknowledgmentResult>(
                $"Cannot confirm — {untouched} line(s) have not been confirmed yet " +
                "(neither accepted as-ordered nor flagged with an exception).");

        if (unapprovedExceptions > 0)
            return Result.Failure<ConfirmAcknowledgmentResult>(
                $"Cannot confirm — {unapprovedExceptions} line exception(s) remain unapproved. " +
                "Approve or reject each before confirming.");

        ack.Status = withExceptions > 0
            ? POAcknowledgmentStatus.ConfirmedWithExceptions
            : POAcknowledgmentStatus.Confirmed;
        ack.AllLinesAcceptedAsOrdered = accepted == ack.Lines.Count && withExceptions == 0;
        ack.ClosedAtUtc = nowUtc;
        ack.UpdatedAt = nowUtc;
        ack.BuyerNotes = AppendNote(ack.BuyerNotes, request.BuyerNotes);

        await _db.SaveChangesAsync(ct);

        return Result.Success(new ConfirmAcknowledgmentResult(
            ack.Id,
            ack.Status,
            accepted,
            withExceptions,
            approvedExceptions,
            $"Acknowledgment {ack.AcknowledgmentNumber} closed as {ack.Status}."));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6) RejectAcknowledgmentAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POAcknowledgment>> RejectAcknowledgmentAsync(
        RejectAcknowledgmentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<POAcknowledgment>("Reason required for rejection.");

        var ack = await LoadAckWithScopeAsync(request.POAcknowledgmentId, ct);
        if (ack == null)
            return Result.Failure<POAcknowledgment>(
                $"POAcknowledgment {request.POAcknowledgmentId} not found or out of tenant scope.");

        if (ack.Status != POAcknowledgmentStatus.Requested &&
            ack.Status != POAcknowledgmentStatus.Acknowledged)
            return Result.Failure<POAcknowledgment>(
                $"Cannot reject from status {ack.Status} — only Requested or Acknowledged are rejectable.");

        // Codex P2: refuse reject on non-current acks (PRRT_kwDOSSj3Wc6Fg48K).
        if (!ack.IsCurrent)
            return Result.Failure<POAcknowledgment>(
                $"Ack #{ack.Id} is no longer the current cycle for its PO — refusing to reject a historical record.");

        var nowUtc = DateTime.UtcNow;
        ack.Status = POAcknowledgmentStatus.Rejected;
        ack.ClosedAtUtc = nowUtc;
        ack.VendorNotes = AppendNote(ack.VendorNotes, $"REJECTED: {request.Reason}");
        ack.UpdatedAt = nowUtc;

        await _db.SaveChangesAsync(ct);
        return Result.Success(ack);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7) MarkExpiredAsync — SLA scan
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<int>> MarkExpiredAsync(
        DateTime nowUtc, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;

        // P1-4 guard: empty scope means a system scheduler/job ran without a
        // tenant context — the .Contains(CompanyId) filter would silently
        // match zero rows and no acks expire. Log loudly so the missed-SLA
        // root cause is discoverable, and return a failure rather than 0.
        if (visible == null || visible.Count == 0)
        {
            _logger.LogWarning(
                "MarkExpiredAsync called with empty VisibleCompanyIds — SLA scan skipped. " +
                "Caller must supply a tenant scope (or system-job bypass) to expire acks.");
            return Result.Failure<int>(
                "Empty tenant scope: cannot run SLA scan. Caller must establish tenant context.");
        }

        var due = await _db.Set<POAcknowledgment>()
            .Where(a => a.IsCurrent &&
                        a.ResponseDueByUtc != null &&
                        a.ResponseDueByUtc < nowUtc &&
                        (a.Status == POAcknowledgmentStatus.Requested ||
                         a.Status == POAcknowledgmentStatus.Acknowledged) &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .ToListAsync(ct);

        foreach (var ack in due)
        {
            ack.Status = POAcknowledgmentStatus.Expired;
            ack.ClosedAtUtc = nowUtc;
            ack.UpdatedAt = nowUtc;
        }
        if (due.Count > 0)
            await _db.SaveChangesAsync(ct);

        return Result.Success(due.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8) CancelAcknowledgmentAsync — buyer cancellation
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POAcknowledgment>> CancelAcknowledgmentAsync(
        int poAcknowledgmentId, string? reason, CancellationToken ct = default)
    {
        var ack = await LoadAckWithScopeAsync(poAcknowledgmentId, ct);
        if (ack == null)
            return Result.Failure<POAcknowledgment>(
                $"POAcknowledgment {poAcknowledgmentId} not found or out of tenant scope.");

        if (ack.Status != POAcknowledgmentStatus.Requested &&
            ack.Status != POAcknowledgmentStatus.Acknowledged)
            return Result.Failure<POAcknowledgment>(
                $"Cannot cancel from status {ack.Status}.");

        // Codex P2: refuse cancel on non-current acks (PRRT_kwDOSSj3Wc6Fg48K).
        if (!ack.IsCurrent)
            return Result.Failure<POAcknowledgment>(
                $"Ack #{ack.Id} is no longer the current cycle for its PO — refusing to cancel a historical record.");

        // P2-9: IsCurrent semantic is "most recent ack record for this PO".
        // It stays true on Cancelled / Confirmed / Rejected — the next
        // RequestAcknowledgmentAsync flips priors when a new cycle opens
        // (typically PR-17 amendment path). Status alone tells the consumer
        // whether the latest ack is still actionable.
        var nowUtc = DateTime.UtcNow;
        ack.Status = POAcknowledgmentStatus.Cancelled;
        ack.ClosedAtUtc = nowUtc;
        ack.UpdatedAt = nowUtc;
        ack.BuyerNotes = AppendNote(ack.BuyerNotes, $"CANCELLED: {reason ?? "(no reason given)"}");

        await _db.SaveChangesAsync(ct);
        return Result.Success(ack);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 9-11) Read methods
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<POAcknowledgment?> GetCurrentAsync(
        int purchaseOrderId, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POAcknowledgment>()
            .Include(a => a.Lines)
            .Where(a => a.PurchaseOrderId == purchaseOrderId &&
                        a.IsCurrent &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .OrderByDescending(a => a.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<POAcknowledgment>> GetHistoryAsync(
        int purchaseOrderId, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POAcknowledgment>()
            .Where(a => a.PurchaseOrderId == purchaseOrderId &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .OrderByDescending(a => a.RequestedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<PoAcknowledgmentSummary> GetSummaryAsync(
        int purchaseOrderId, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        var history = await _db.Set<POAcknowledgment>()
            .Include(a => a.Lines)
            .Where(a => a.PurchaseOrderId == purchaseOrderId &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .ToListAsync(ct);

        var current = history.FirstOrDefault(a => a.IsCurrent);
        var nowUtc = DateTime.UtcNow;

        if (current == null)
        {
            return new PoAcknowledgmentSummary(
                null, null, null, null, null, null, null,
                history.Count, 0, 0, 0, 0, false);
        }

        var lines = current.Lines ?? new List<POAcknowledgmentLine>();
        var withExc = lines.Where(l => l.ExceptionType != PoAckLineExceptionType.None).ToList();
        var isOverdue = current.ResponseDueByUtc != null &&
                        current.ResponseDueByUtc < nowUtc &&
                        (current.Status == POAcknowledgmentStatus.Requested ||
                         current.Status == POAcknowledgmentStatus.Acknowledged);

        return new PoAcknowledgmentSummary(
            current.Id,
            current.AcknowledgmentNumber,
            current.Status,
            current.RequestedAtUtc,
            current.AcknowledgedAtUtc,
            current.ClosedAtUtc,
            current.ResponseDueByUtc,
            history.Count,
            lines.Count,
            lines.Count(l => l.IsAccepted),
            withExc.Count,
            withExc.Count(l => l.ExceptionApproved),
            isOverdue);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<POAcknowledgment?> LoadAckWithScopeAsync(
        int ackId, CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POAcknowledgment>()
            .Where(a => a.Id == ackId &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<POAcknowledgment?> LoadAckWithLinesAsync(
        int ackId, CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POAcknowledgment>()
            .Include(a => a.Lines)
            .Where(a => a.Id == ackId &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .FirstOrDefaultAsync(ct);
    }

    private static string? AppendNote(string? existing, string? addition)
    {
        if (string.IsNullOrWhiteSpace(addition)) return existing;
        if (string.IsNullOrWhiteSpace(existing)) return addition;
        return $"{existing}\n{addition}";
    }
}
