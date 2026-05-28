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

// Sprint 15.1 PR-3 (2026-05-28) — admin probe for IPoLineDemandLinkService.
//
// SEVEN WRITE/ACTION BUTTONS per Lock 16 corollary:
//   1. Link PO Line → Demand   (LinkAsync)
//   2. Record Receipt vs Link  (RecordReceiptAgainstLinkAsync)
//   3. Release Link            (ReleaseAsync)
//   4. Mark PO Line DTJ        (MarkPoLineDirectToJobAsync)
//   5. Mark PO Line Subcontract (MarkPoLineSubcontractAsync)
//   6. Load Links for PO Line  (read)
//   7. Load Links for Demand   (read)
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count/list queries. All writes flow through IPoLineDemandLinkService.")]
public sealed class PoLineDemandLinkProbeModel : PageModel
{
    private readonly IPoLineDemandLinkService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<PoLineDemandLinkProbeModel> _logger;

    public PoLineDemandLinkProbeModel(
        IPoLineDemandLinkService svc,
        AppDbContext db,
        ILogger<PoLineDemandLinkProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // Inputs
    [BindProperty] public int LinkPoLineId { get; set; } = 1;
    [BindProperty] public int LinkDemandId { get; set; } = 1;
    [BindProperty] public decimal LinkQuantity { get; set; } = 10m;
    [BindProperty] public int? LinkReleaseId { get; set; }

    [BindProperty] public int ReceiptLinkId { get; set; } = 1;
    [BindProperty] public decimal ReceiptQuantity { get; set; } = 5m;

    [BindProperty] public int ReleaseLinkId { get; set; } = 1;

    [BindProperty] public int MarkPoLineId { get; set; } = 1;
    [BindProperty] public bool MarkAsDirectToJob { get; set; } = true;

    [BindProperty] public int MarkScPoLineId { get; set; } = 1;
    [BindProperty] public bool MarkAsSubcontract { get; set; } = true;

    [BindProperty] public int LoadByPoLineId { get; set; } = 1;
    [BindProperty] public int LoadByDemandId { get; set; } = 1;

    // Output
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalLinks { get; private set; }
    public int LinksActive { get; private set; }
    public int LinksPartiallyReceived { get; private set; }
    public int LinksFullyReceived { get; private set; }
    public int LinksReleased { get; private set; }
    public int PoLinesDirectToJob { get; private set; }
    public int PoLinesSubcontract { get; private set; }

    public IReadOnlyList<PurchaseOrderLineDemandLink> LoadedLinks { get; private set; }
        = Array.Empty<PurchaseOrderLineDemandLink>();

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalLinks = await _db.Set<PurchaseOrderLineDemandLink>().CountAsync(ct);
        LinksActive = await _db.Set<PurchaseOrderLineDemandLink>()
            .CountAsync(l => l.Status == PoDemandLinkStatus.Active, ct);
        LinksPartiallyReceived = await _db.Set<PurchaseOrderLineDemandLink>()
            .CountAsync(l => l.Status == PoDemandLinkStatus.PartiallyReceived, ct);
        LinksFullyReceived = await _db.Set<PurchaseOrderLineDemandLink>()
            .CountAsync(l => l.Status == PoDemandLinkStatus.FullyReceived, ct);
        LinksReleased = await _db.Set<PurchaseOrderLineDemandLink>()
            .CountAsync(l => l.Status == PoDemandLinkStatus.Released, ct);
        PoLinesDirectToJob = await _db.PurchaseOrderLines.CountAsync(p => p.IsDirectToJob, ct);
        PoLinesSubcontract = await _db.PurchaseOrderLines.CountAsync(p => p.IsSubcontract, ct);
    }

    public async Task<IActionResult> OnPostLinkAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.LinkAsync(
            new LinkPoLineToDemandRequest(
                LinkPoLineId, LinkDemandId, LinkQuantity,
                LinkReleaseId, null, null, "Admin probe link", by),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Link #{r.Value!.LinkId}: PO line {r.Value.PurchaseOrderLineId} ↔ demand {r.Value.ProductionSupplyDemandId} = {r.Value.AllocatedQuantity:N4}. " +
              $"Aggregate {r.Value.AggregateAllocatedAcrossDemand:N4}, remaining {r.Value.RemainingDemand:N4}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRecordReceiptAsync(CancellationToken ct)
    {
        var r = await _svc.RecordReceiptAgainstLinkAsync(
            new RecordReceiptAgainstLinkRequest(ReceiptLinkId, ReceiptQuantity, null, "Admin probe receipt"),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Link #{r.Value!.LinkId} cumulative received {r.Value.CumulativeReceivedOnLink:N4}, status={r.Value.NewStatus}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReleaseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReleaseAsync(ReleaseLinkId, "Admin probe release", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Released link #{r.Value!.LinkId}: {r.Value.QuantityReleased:N4}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostMarkDtjAsync(CancellationToken ct)
    {
        var r = await _svc.MarkPoLineDirectToJobAsync(MarkPoLineId, MarkAsDirectToJob, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"PO line {r.Value} IsDirectToJob = {MarkAsDirectToJob}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostMarkSubcontractAsync(CancellationToken ct)
    {
        var r = await _svc.MarkPoLineSubcontractAsync(MarkScPoLineId, MarkAsSubcontract, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"PO line {r.Value} IsSubcontract = {MarkAsSubcontract}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadByPoLineAsync(CancellationToken ct)
    {
        LoadedLinks = await _svc.GetLinksForPoLineAsync(LoadByPoLineId, ct);
        Set(true, $"Loaded {LoadedLinks.Count} links for PO line {LoadByPoLineId}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadByDemandAsync(CancellationToken ct)
    {
        LoadedLinks = await _svc.GetLinksForDemandAsync(LoadByDemandId, ct);
        Set(true, $"Loaded {LoadedLinks.Count} links for demand {LoadByDemandId}.");
        await LoadStatsAsync(ct);
        return Page();
    }
}
