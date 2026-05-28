// Sprint 15.4 PR-17 — admin probe for IPoAmendmentService.
// 9 write/action buttons covering full lifecycle + impact preview (the BIC
// differentiator) + atomic apply (PO line update + demand-link resync +
// vendor re-ack hook).

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
    "All writes flow through IPoAmendmentService (and via PR-17 the re-ack " +
    "hook into IPoAcknowledgmentService).")]
public sealed class PoAmendmentProbeModel : PageModel
{
    private readonly IPoAmendmentService _service;
    private readonly AppDbContext _db;
    private readonly ILogger<PoAmendmentProbeModel> _logger;

    public PoAmendmentProbeModel(
        IPoAmendmentService service,
        AppDbContext db,
        ILogger<PoAmendmentProbeModel> logger)
    {
        _service = service;
        _db = db;
        _logger = logger;
    }

    // ── 1) Draft a quantity-bump amendment ──
    [BindProperty] public int DraftPurchaseOrderId { get; set; } = 60;
    [BindProperty] public int DraftPoLineId { get; set; } = 1;
    [BindProperty] public decimal DraftNewQty { get; set; } = 105m;
    [BindProperty] public decimal DraftNewPrice { get; set; } = 12.75m;
    [BindProperty] public int DraftPromisePushDays { get; set; } = 5;
    [BindProperty] public POChangeReason DraftReason { get; set; } = POChangeReason.VendorExceptionApproved;
    [BindProperty] public bool DraftRequireReAck { get; set; } = true;

    // ── 2) Preview impact ──
    [BindProperty] public int PreviewAmendmentId { get; set; } = 1;

    // ── 3) Submit for approval ──
    [BindProperty] public int SubmitAmendmentId { get; set; } = 1;

    // ── 4) Approve ──
    [BindProperty] public int ApproveAmendmentId { get; set; } = 1;
    [BindProperty] public int ApproveUserId { get; set; } = 1;
    [BindProperty] public string? ApproveNote { get; set; } = "Schedule absorbs the +5d push-out.";

    // ── 5) Apply (atomic + auto-resync) ──
    [BindProperty] public int ApplyAmendmentId { get; set; } = 1;

    // ── 6) Reject ──
    [BindProperty] public int RejectAmendmentId { get; set; } = 1;
    [BindProperty] public string RejectReason { get; set; }
        = "Customer ship date will slip — re-source.";

    // ── 7) Cancel ──
    [BindProperty] public int CancelAmendmentId { get; set; } = 1;
    [BindProperty] public string? CancelReason { get; set; } = "Buyer abandoned draft.";

    // ── 8) Re-preview (idempotency) ──
    [BindProperty] public int RePreviewAmendmentId { get; set; } = 1;

    // ── R1/R2 reads ──
    [BindProperty] public int SummaryPoId { get; set; } = 60;
    [BindProperty] public int HistoryPoId { get; set; } = 60;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalAmendments { get; private set; }
    public int DraftCount { get; private set; }
    public int PreviewedCount { get; private set; }
    public int PendingApprovalCount { get; private set; }
    public int ApprovedCount { get; private set; }
    public int AppliedCount { get; private set; }
    public int RejectedCount { get; private set; }
    public int CancelledCount { get; private set; }
    public int CurrentShipDateRiskCount { get; private set; }
    public int CurrentAppliedAcksOpened { get; private set; }

    public AmendmentImpactReport? LastImpactReport { get; private set; }
    public ApplyAmendmentResult? LastApplyResult { get; private set; }
    public PoAmendmentSummary? LastSummary { get; private set; }
    public IReadOnlyList<POChangeHistory>? LastHistory { get; private set; }

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
        TotalAmendments = await _db.Set<POChangeHistory>().CountAsync(ct);
        DraftCount = await _db.Set<POChangeHistory>().CountAsync(a => a.Status == POAmendmentStatus.Draft, ct);
        PreviewedCount = await _db.Set<POChangeHistory>().CountAsync(a => a.Status == POAmendmentStatus.Previewed, ct);
        PendingApprovalCount = await _db.Set<POChangeHistory>().CountAsync(a => a.Status == POAmendmentStatus.PendingApproval, ct);
        ApprovedCount = await _db.Set<POChangeHistory>().CountAsync(a => a.Status == POAmendmentStatus.Approved, ct);
        AppliedCount = await _db.Set<POChangeHistory>().CountAsync(a => a.Status == POAmendmentStatus.Applied, ct);
        RejectedCount = await _db.Set<POChangeHistory>().CountAsync(a => a.Status == POAmendmentStatus.Rejected, ct);
        CancelledCount = await _db.Set<POChangeHistory>().CountAsync(a => a.Status == POAmendmentStatus.Cancelled, ct);
        CurrentShipDateRiskCount = await _db.Set<POChangeHistory>()
            .CountAsync(a => a.IsCurrent && a.ShipDateRiskFlag, ct);
        // Count vendor acks opened by amendment-apply paths
        // (BuyerNotes contains the marker "Auto-opened by amendment").
        CurrentAppliedAcksOpened = await _db.Set<POAcknowledgment>()
            .CountAsync(a => a.BuyerNotes != null && a.BuyerNotes.Contains("Auto-opened by amendment"), ct);

        EligiblePos = await _db.Set<PurchaseOrder>()
            .Where(p => p.Status == POStatus.Approved ||
                        p.Status == POStatus.Sent ||
                        p.Status == POStatus.PartiallyReceived)
            .OrderByDescending(p => p.OrderDate)
            .Take(20)
            .Select(p => new PurchaseOrderOption(p.Id, $"PO #{p.Id} — {p.PONumber} ({p.Status})"))
            .ToListAsync(ct);
    }

    // 1) DRAFT
    public async Task<IActionResult> OnPostDraftAsync(CancellationToken ct)
    {
        var poLine = await _db.Set<PurchaseOrderLine>()
            .Where(l => l.Id == DraftPoLineId)
            .FirstOrDefaultAsync(ct);
        if (poLine == null)
        {
            Set(false, $"PO line {DraftPoLineId} not found.");
            await LoadStatsAsync(ct);
            return Page();
        }
        var newPromise = poLine.RequiredDate.HasValue
            ? poLine.RequiredDate.Value.AddDays(DraftPromisePushDays)
            : DateTime.UtcNow.AddDays(DraftPromisePushDays);
        var draft = new DraftAmendmentRequest(
            PurchaseOrderId: DraftPurchaseOrderId,
            Reason: DraftReason,
            ReasonNarrative: "Admin probe — synthetic quantity-bump + price-bump + date-push amendment.",
            DraftedByUserId: 1,
            VendorReAcknowledgmentRequired: DraftRequireReAck,
            Lines: new[]
            {
                new AmendmentLineDraft(
                    PurchaseOrderLineId: DraftPoLineId,
                    ChangeType: POAmendmentLineChangeType.MultipleChanges,
                    NewQuantity: DraftNewQty,
                    NewUnitPrice: DraftNewPrice,
                    NewPromiseDate: newPromise,
                    NewRequiredDate: newPromise,
                    LineNarrative: "Vendor capacity constraint — qty up, price up, promise +5d.")
            });
        var r = await _service.DraftAmendmentAsync(draft, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Drafted {r.Value!.AmendmentNumber} on PO #{DraftPurchaseOrderId}: " +
              $"{r.Value.LinesDrafted} line(s) in status {r.Value.Status}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) PREVIEW IMPACT
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    {
        var r = await _service.PreviewAmendmentImpactAsync(PreviewAmendmentId, ct);
        if (r.IsSuccess) LastImpactReport = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Preview computed: {r.Value!.LinesChanged} lines changed, " +
              $"{r.Value.AffectedDemandLinks} demand link(s) across {r.Value.AffectedProductionOrders} PRO(s) " +
              $"({r.Value.AffectedOperations} op(s))." +
              (r.Value.ShipDateRiskFlag ? " ⚠ SHIP-DATE RISK." : "")
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) SUBMIT FOR APPROVAL
    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken ct)
    {
        var r = await _service.SubmitForApprovalAsync(SubmitAmendmentId, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Amendment #{r.Value!.Id} → PendingApproval (submitted at {r.Value.SubmittedForApprovalAtUtc:u})."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) APPROVE
    public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
    {
        var r = await _service.ApproveAmendmentAsync(
            new ApproveAmendmentRequest(ApproveAmendmentId, ApproveUserId, ApproveNote), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Amendment #{r.Value!.Id} → Approved by user {ApproveUserId}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) APPLY (atomic + auto-resync)
    public async Task<IActionResult> OnPostApplyAsync(CancellationToken ct)
    {
        var r = await _service.ApplyAmendmentAsync(ApplyAmendmentId, ct);
        if (r.IsSuccess) LastApplyResult = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? r.Value!.Message ?? "Applied."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6) REJECT
    public async Task<IActionResult> OnPostRejectAsync(CancellationToken ct)
    {
        var r = await _service.RejectAmendmentAsync(
            new RejectAmendmentRequest(RejectAmendmentId, 1, RejectReason), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Amendment #{r.Value!.Id} → Rejected."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 7) CANCEL
    public async Task<IActionResult> OnPostCancelAsync(CancellationToken ct)
    {
        var r = await _service.CancelAmendmentAsync(CancelAmendmentId, CancelReason, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Amendment #{r.Value!.Id} → Cancelled (IsCurrent stays {r.Value.IsCurrent} until next Draft)."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 8) RE-PREVIEW (idempotency demonstration)
    // P2-11 fix: invoke the service directly with RePreviewAmendmentId rather
    // than overwriting the PreviewAmendmentId BindProperty as a side effect.
    public async Task<IActionResult> OnPostRePreviewAsync(CancellationToken ct)
    {
        var r = await _service.PreviewAmendmentImpactAsync(RePreviewAmendmentId, ct);
        if (r.IsSuccess) LastImpactReport = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Re-preview computed (idempotency check): {r.Value!.LinesChanged} lines, " +
              $"{r.Value.AffectedDemandLinks} demand link(s), " +
              $"{r.Value.AffectedProductionOrders} PRO(s). Should match first preview."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // R1) LOAD SUMMARY
    public async Task<IActionResult> OnPostLoadSummaryAsync(CancellationToken ct)
    {
        LastSummary = await _service.GetSummaryAsync(SummaryPoId, ct);
        Set(true,
            LastSummary.CurrentAmendmentId == null
                ? $"PO #{SummaryPoId} has no amendment yet (history count: {LastSummary.TotalAmendmentsInHistory})."
                : $"Current {LastSummary.CurrentAmendmentNumber} status {LastSummary.CurrentStatus}, " +
                  $"{LastSummary.AffectedDemandLinksInCurrent} demand link(s), " +
                  $"{LastSummary.AffectedProductionOrdersInCurrent} PRO(s), " +
                  $"value Δ {LastSummary.TotalValueDelta:N2}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R2) LOAD HISTORY
    public async Task<IActionResult> OnPostLoadHistoryAsync(CancellationToken ct)
    {
        LastHistory = await _service.GetHistoryAsync(HistoryPoId, ct);
        Set(true, $"PO #{HistoryPoId} amendment history: {LastHistory.Count} record(s).");
        await LoadStatsAsync(ct);
        return Page();
    }
}
