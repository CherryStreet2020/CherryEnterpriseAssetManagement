// Sprint 15.4 PR-16 — admin probe for IPoAcknowledgmentService.
// 9 write/action buttons covering full §15 lifecycle:
//   1) Request (open ack cycle on a PO)
//   2) Record header ack (vendor confirms receipt)
//   3) Record line confirmation (per-line vendor confirm — accepted)
//   4) Record line confirmation w/ exception (DatePushOut by default)
//   5) Approve line exception
//   6) Confirm acknowledgment (terminal)
//   7) Reject acknowledgment
//   8) Cancel acknowledgment
//   9) Mark expired (SLA scan)
// Plus 2 read buttons (Load Summary, Load History).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe. AppDbContext used for read-only count queries. " +
    "All writes flow through IPoAcknowledgmentService.")]
public sealed class PoAcknowledgmentProbeModel : PageModel
{
    private readonly IPoAcknowledgmentService _service;
    private readonly AppDbContext _db;
    private readonly ILogger<PoAcknowledgmentProbeModel> _logger;

    public PoAcknowledgmentProbeModel(
        IPoAcknowledgmentService service,
        AppDbContext db,
        ILogger<PoAcknowledgmentProbeModel> logger)
    {
        _service = service;
        _db = db;
        _logger = logger;
    }

    // ── Request ack ──
    [BindProperty] public int RequestPurchaseOrderId { get; set; } = 1;
    [BindProperty] public POAcknowledgmentMethod RequestMethod { get; set; }
        = POAcknowledgmentMethod.VendorPortal;
    [BindProperty] public int? RequestDueInDays { get; set; } = 7;

    // ── Header ack ──
    [BindProperty] public int RecordAckId { get; set; } = 1;
    [BindProperty] public POAcknowledgmentMethod RecordMethod { get; set; }
        = POAcknowledgmentMethod.Email;
    [BindProperty] public string? RecordVendorContact { get; set; } = "sales@vendor.example";
    [BindProperty] public DateTime? RecordOverallPromiseDate { get; set; }

    // ── Line confirm (accepted as-ordered) ──
    [BindProperty] public int LineConfirmLineId { get; set; } = 1;

    // ── Line confirm w/ exception (DatePushOut by default) ──
    [BindProperty] public int LineExceptionLineId { get; set; } = 1;
    [BindProperty] public PoAckLineExceptionType LineExceptionType { get; set; }
        = PoAckLineExceptionType.DatePushOut;
    [BindProperty] public decimal LineExceptionQty { get; set; } = 100m;
    [BindProperty] public decimal LineExceptionPrice { get; set; } = 12.50m;
    [BindProperty] public DateTime? LineExceptionPromise { get; set; }
    [BindProperty] public string? LineExceptionReason { get; set; }
        = "Vendor capacity constraint — pushing promise +5 days.";

    // ── Approve line exception ──
    [BindProperty] public int ApproveLineId { get; set; } = 1;
    [BindProperty] public int ApproverUserId { get; set; } = 1;
    [BindProperty] public string? ApprovalNote { get; set; }
        = "Approved — schedule absorbs the push-out.";

    // ── Confirm ack ──
    [BindProperty] public int ConfirmAckId { get; set; } = 1;

    // ── Reject ack ──
    [BindProperty] public int RejectAckId { get; set; } = 1;
    [BindProperty] public string RejectReason { get; set; }
        = "Vendor unable to fulfill before required date — re-source.";

    // ── Cancel ack ──
    [BindProperty] public int CancelAckId { get; set; } = 1;
    [BindProperty] public string? CancelReason { get; set; }
        = "Buyer cancelled — PO being amended.";

    // ── Summary / history reads ──
    [BindProperty] public int SummaryPoId { get; set; } = 1;
    [BindProperty] public int HistoryPoId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalAcknowledgments { get; private set; }
    public int RequestedCount { get; private set; }
    public int AcknowledgedCount { get; private set; }
    public int ConfirmedCount { get; private set; }
    public int ConfirmedWithExceptionsCount { get; private set; }
    public int RejectedCount { get; private set; }
    public int ExpiredCount { get; private set; }
    public int CancelledCount { get; private set; }
    public int CurrentOverdueCount { get; private set; }
    public int LineExceptionsOpenCount { get; private set; }

    public PoAcknowledgmentSummary? LastSummary { get; private set; }
    public IReadOnlyList<POAcknowledgment>? LastHistory { get; private set; }

    public IReadOnlyList<PurchaseOrderOption> EligiblePos { get; private set; }
        = Array.Empty<PurchaseOrderOption>();

    public sealed record PurchaseOrderOption(int Id, string Display);

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalAcknowledgments = await _db.Set<POAcknowledgment>().CountAsync(ct);
        RequestedCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.Status == POAcknowledgmentStatus.Requested, ct);
        AcknowledgedCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.Status == POAcknowledgmentStatus.Acknowledged, ct);
        ConfirmedCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.Status == POAcknowledgmentStatus.Confirmed, ct);
        ConfirmedWithExceptionsCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.Status == POAcknowledgmentStatus.ConfirmedWithExceptions, ct);
        RejectedCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.Status == POAcknowledgmentStatus.Rejected, ct);
        ExpiredCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.Status == POAcknowledgmentStatus.Expired, ct);
        CancelledCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.Status == POAcknowledgmentStatus.Cancelled, ct);

        var nowUtc = DateTime.UtcNow;
        CurrentOverdueCount = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.IsCurrent &&
                              a.ResponseDueByUtc != null &&
                              a.ResponseDueByUtc < nowUtc &&
                              (a.Status == POAcknowledgmentStatus.Requested ||
                               a.Status == POAcknowledgmentStatus.Acknowledged), ct);
        LineExceptionsOpenCount = await _db.Set<POAcknowledgmentLine>()
            .CountAsync(l => l.ExceptionType != PoAckLineExceptionType.None &&
                              !l.ExceptionApproved, ct);

        EligiblePos = await _db.Set<PurchaseOrder>()
            .Where(p => p.Status == POStatus.Sent ||
                        p.Status == POStatus.Approved ||
                        p.Status == POStatus.PartiallyReceived)
            .OrderByDescending(p => p.OrderDate)
            .Take(20)
            .Select(p => new PurchaseOrderOption(
                p.Id, $"PO #{p.Id} — {p.PONumber} ({p.Status})"))
            .ToListAsync(ct);
    }

    // 1) REQUEST ACKNOWLEDGMENT
    public async Task<IActionResult> OnPostRequestAsync(CancellationToken ct)
    {
        DateTime? due = RequestDueInDays.HasValue
            ? DateTime.UtcNow.AddDays(RequestDueInDays.Value)
            : null;
        var r = await _service.RequestAcknowledgmentAsync(
            new RequestAcknowledgmentRequest(
                PurchaseOrderId: RequestPurchaseOrderId,
                RequestedMethod: RequestMethod,
                ResponseDueByUtc: due,
                RequestedByUserId: null,
                BuyerNotes: "Admin probe — opening ack cycle."),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created {r.Value!.AcknowledgmentNumber} on PO #{RequestPurchaseOrderId}: " +
              $"{r.Value.LinesInitialized} line(s) initialized in status {r.Value.Status}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) RECORD HEADER ACKNOWLEDGMENT
    public async Task<IActionResult> OnPostRecordAckAsync(CancellationToken ct)
    {
        var r = await _service.RecordAcknowledgmentAsync(
            new RecordAcknowledgmentRequest(
                POAcknowledgmentId: RecordAckId,
                Method: RecordMethod,
                VendorContact: RecordVendorContact,
                OverallConfirmedPromiseDate: RecordOverallPromiseDate ?? DateTime.UtcNow.AddDays(14),
                VendorNotes: "Vendor confirms receipt of PO."),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Ack #{r.Value!.Id} → {r.Value.Status} (vendor contact: {r.Value.AcknowledgedByVendorContact})."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) RECORD LINE CONFIRMATION (accepted as-ordered)
    public async Task<IActionResult> OnPostLineAcceptedAsync(CancellationToken ct)
    {
        // Look up the line so we can mirror its snapshot values, producing
        // IsAccepted=true (no exception).
        var line = await _db.Set<POAcknowledgmentLine>()
            .Where(l => l.Id == LineConfirmLineId)
            .FirstOrDefaultAsync(ct);
        if (line == null)
        {
            Set(false, $"POAcknowledgmentLine {LineConfirmLineId} not found.");
            await LoadStatsAsync(ct);
            return Page();
        }
        var r = await _service.RecordLineConfirmationAsync(
            new RecordLineConfirmationRequest(
                POAcknowledgmentLineId: LineConfirmLineId,
                ConfirmedQuantity: line.OrderedQuantity,
                ConfirmedUnitPrice: line.OrderedUnitPrice,
                ConfirmedPromiseDate: line.RequiredDate,
                ExceptionType: PoAckLineExceptionType.None,
                ExceptionReason: null),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Line #{r.Value!.Id} confirmed as-ordered (IsAccepted={r.Value.IsAccepted})."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) RECORD LINE CONFIRMATION (with exception)
    public async Task<IActionResult> OnPostLineExceptionAsync(CancellationToken ct)
    {
        var r = await _service.RecordLineConfirmationAsync(
            new RecordLineConfirmationRequest(
                POAcknowledgmentLineId: LineExceptionLineId,
                ConfirmedQuantity: LineExceptionQty,
                ConfirmedUnitPrice: LineExceptionPrice,
                ConfirmedPromiseDate: LineExceptionPromise ?? DateTime.UtcNow.AddDays(20),
                ExceptionType: LineExceptionType,
                ExceptionReason: LineExceptionReason),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Line #{r.Value!.Id} flagged {r.Value.ExceptionType} " +
              $"(IsAccepted={r.Value.IsAccepted}, awaiting buyer approval)."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) APPROVE LINE EXCEPTION
    public async Task<IActionResult> OnPostApproveExceptionAsync(CancellationToken ct)
    {
        var r = await _service.ApproveLineExceptionAsync(
            new ApproveLineExceptionRequest(
                POAcknowledgmentLineId: ApproveLineId,
                ApproverUserId: ApproverUserId,
                ApprovalNote: ApprovalNote),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Line #{r.Value!.Id} exception {r.Value.ExceptionType} approved by user {ApproverUserId}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6) CONFIRM ACKNOWLEDGMENT
    public async Task<IActionResult> OnPostConfirmAsync(CancellationToken ct)
    {
        var r = await _service.ConfirmAcknowledgmentAsync(
            new ConfirmAcknowledgmentRequest(
                POAcknowledgmentId: ConfirmAckId,
                BuyerNotes: "Admin probe — closing ack cycle."),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Ack #{r.Value!.POAcknowledgmentId} → {r.Value.FinalStatus} " +
              $"({r.Value.LinesAccepted} accepted, {r.Value.LinesWithExceptions} with exceptions, " +
              $"{r.Value.LinesApprovedExceptions} approved)."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 7) REJECT ACKNOWLEDGMENT
    public async Task<IActionResult> OnPostRejectAsync(CancellationToken ct)
    {
        var r = await _service.RejectAcknowledgmentAsync(
            new RejectAcknowledgmentRequest(
                POAcknowledgmentId: RejectAckId,
                Reason: RejectReason),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Ack #{r.Value!.Id} rejected. Status → {r.Value.Status}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 8) CANCEL ACKNOWLEDGMENT
    public async Task<IActionResult> OnPostCancelAsync(CancellationToken ct)
    {
        var r = await _service.CancelAcknowledgmentAsync(
            poAcknowledgmentId: CancelAckId,
            reason: CancelReason,
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Ack #{r.Value!.Id} cancelled. Status → {r.Value.Status} (IsCurrent stays {r.Value.IsCurrent} until next Request)."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 9) MARK EXPIRED (SLA scan)
    public async Task<IActionResult> OnPostMarkExpiredAsync(CancellationToken ct)
    {
        var r = await _service.MarkExpiredAsync(DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SLA scan complete: {r.Value} ack(s) moved to Expired."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // R1) LOAD SUMMARY (read)
    public async Task<IActionResult> OnPostLoadSummaryAsync(CancellationToken ct)
    {
        LastSummary = await _service.GetSummaryAsync(SummaryPoId, ct);
        Set(true,
            LastSummary.CurrentAcknowledgmentId == null
                ? $"PO #{SummaryPoId} has no acknowledgment yet (history count: " +
                  $"{LastSummary.TotalAcknowledgmentsInHistory})."
                : $"Current {LastSummary.CurrentAcknowledgmentNumber} status " +
                  $"{LastSummary.CurrentStatus} — {LastSummary.LinesAccepted}/{LastSummary.LinesTotal} accepted, " +
                  $"{LastSummary.LinesWithExceptions} exceptions ({LastSummary.LinesExceptionsApproved} approved), " +
                  $"overdue={LastSummary.IsOverdue}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R2) LOAD HISTORY (read)
    public async Task<IActionResult> OnPostLoadHistoryAsync(CancellationToken ct)
    {
        LastHistory = await _service.GetHistoryAsync(HistoryPoId, ct);
        Set(true, $"PO #{HistoryPoId} ack history: {LastHistory.Count} record(s).");
        await LoadStatsAsync(ct);
        return Page();
    }
}
