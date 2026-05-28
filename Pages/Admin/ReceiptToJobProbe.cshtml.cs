using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 15.1 PR-1 (2026-05-28) — admin probe for IReceiptToJobService.
//
// SEVEN WRITE/ACTION BUTTONS per Lock 16 corollary:
//   1. Receive To Job  (ReceiveToJobAsync — direct post)
//   2. Reverse Receipt  (ReverseReceiptToJobAsync — undo DTJ)
//   3. Complete Inspection  (CompleteInspectionAndPostAsync — release hold)
//   4. Process All DTJ Lines on Receipt  (ProcessDirectToJobLinesAsync — batch)
//   5. Create Test GR Line (IsDirectToJob)  (seed a test receipt line)
//   6. Load DTJ Lines  (read — list all direct-to-job receipt lines)
//   7. Load BOM Supply Links  (read — show supply status on BOM lines)
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count/list queries and test data seeding. All business writes flow through IReceiptToJobService.")]
public sealed class ReceiptToJobProbeModel : PageModel
{
    private readonly IReceiptToJobService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<ReceiptToJobProbeModel> _logger;

    public ReceiptToJobProbeModel(
        IReceiptToJobService svc,
        AppDbContext db,
        ILogger<ReceiptToJobProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Bind properties ─────────────────────────────────────────
    [BindProperty] public int ReceiveGrLineId { get; set; } = 1;
    [BindProperty] public int ReceiveProId { get; set; } = 1;
    [BindProperty] public int ReceiveBomLineId { get; set; } = 1;

    [BindProperty] public int ReverseGrLineId { get; set; } = 1;

    [BindProperty] public int InspectGrLineId { get; set; } = 1;
    [BindProperty] public decimal InspectQtyAccepted { get; set; } = 10m;
    [BindProperty] public decimal InspectQtyRejected { get; set; } = 0m;

    [BindProperty] public int BatchReceiptId { get; set; } = 1;

    [BindProperty] public int LoadProId { get; set; } = 1;

    // ── Output ──────────────────────────────────────────────────
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    // Summary stats
    public int TotalDtjLines { get; private set; }
    public int TotalDtjPosted { get; private set; }
    public int TotalDtjHeld { get; private set; }
    public int TotalGoodsReceipts { get; private set; }
    public int TotalBomLines { get; private set; }

    // Recent DTJ receipt lines
    public IReadOnlyList<GoodsReceiptLine> RecentDtjLines { get; private set; } = Array.Empty<GoodsReceiptLine>();

    // BOM supply links for a specific PRO
    public IReadOnlyList<ProductionMaterialStructure> BomSupplyLinks { get; private set; } = Array.Empty<ProductionMaterialStructure>();

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    // ═══════════════════════════════════════════════════════════════
    // OnGet — load summary stats
    // ═══════════════════════════════════════════════════════════════

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
    }

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalDtjLines = await _db.GoodsReceiptLines.CountAsync(l => l.IsDirectToJob, ct);
        TotalDtjPosted = await _db.GoodsReceiptLines.CountAsync(
            l => l.IsDirectToJob && l.DirectToJobPostedUtc.HasValue, ct);
        TotalDtjHeld = await _db.GoodsReceiptLines.CountAsync(
            l => l.IsDirectToJob && l.InspectionRequired && !l.DirectToJobPostedUtc.HasValue, ct);
        TotalGoodsReceipts = await _db.GoodsReceipts.CountAsync(ct);
        TotalBomLines = await _db.Set<ProductionMaterialStructure>().CountAsync(ct);

        RecentDtjLines = await _db.GoodsReceiptLines
            .Include(l => l.GoodsReceipt)
            .Include(l => l.PurchaseOrderLine)
            .Include(l => l.DirectToJobProductionOrder)
            .Include(l => l.DirectToJobBomLine)
            .Where(l => l.IsDirectToJob)
            .OrderByDescending(l => l.Id)
            .Take(20)
            .ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. Receive To Job — direct-charge receipt
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostReceiveToJobAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReceiveToJobAsync(
            new ReceiveToJobRequest(
                ReceiveGrLineId,
                ReceiveProId,
                ReceiveBomLineId,
                PostedBy: by),
            ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"DTJ posted: GR line {r.Value!.GoodsReceiptLineId} → PRO {r.Value.ProductionOrderId} BOM {r.Value.BomLineId}. " +
              $"Qty={r.Value.QuantityPosted:N4}, Amt=${r.Value.AmountPosted:N2}. " +
              $"CostTxn={r.Value.CostTransactionId}, JE={r.Value.JournalEntryId}. " +
              (r.Value.InspectionHeld ? "INSPECTION HOLD — cost deferred." : "Posted.") +
              $" {r.Value.Message}"
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Reverse Receipt — undo DTJ
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostReverseReceiptAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReverseReceiptToJobAsync(ReverseGrLineId, by, ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"REVERSED: GR line {r.Value!.GoodsReceiptLineId} from PRO {r.Value.ProductionOrderId}. " +
              $"Qty={r.Value.QuantityPosted:N4}, Amt=${r.Value.AmountPosted:N2}. JE={r.Value.JournalEntryId}. {r.Value.Message}"
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Complete Inspection — release hold
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostCompleteInspectionAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CompleteInspectionAndPostAsync(
            InspectGrLineId,
            InspectQtyAccepted,
            InspectQtyRejected,
            by,
            ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"Inspection complete: GR line {r.Value!.GoodsReceiptLineId}. " +
              $"Accepted={InspectQtyAccepted:N4}, Rejected={InspectQtyRejected:N4}. {r.Value.Message}"
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Process All DTJ Lines on a Receipt — batch
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostBatchProcessAsync(CancellationToken ct)
    {
        var results = await _svc.ProcessDirectToJobLinesAsync(BatchReceiptId, ct);

        Set(true, $"Batch processed {results.Count} DTJ lines on GR {BatchReceiptId}. " +
            string.Join("; ", results.Select(r =>
                $"Line {r.GoodsReceiptLineId}: {r.QuantityPosted:N4} × ${r.AmountPosted:N2}")));

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. Create Test GR Line (IsDirectToJob) — seed data for probe
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostCreateTestLineAsync(CancellationToken ct)
    {
        // Find the first GR + first PO line + first PRO + first BOM line
        var gr = await _db.GoodsReceipts
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(ct);
        var poLine = await _db.PurchaseOrderLines
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        var pro = await _db.Set<ProductionOrder>()
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        var bomLine = await _db.Set<ProductionMaterialStructure>()
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (gr == null || poLine == null)
        {
            Set(false, "Cannot seed: no GoodsReceipt or PurchaseOrderLine found.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var testLine = new GoodsReceiptLine
        {
            GoodsReceiptId = gr.Id,
            PurchaseOrderLineId = poLine.Id,
            LineNumber = 900 + new Random().Next(1, 99),
            QuantityReceived = 10m,
            QuantityAccepted = 10m,
            IsDirectToJob = true,
            DirectToJobProductionOrderId = pro?.Id,
            DirectToJobBomLineId = bomLine?.Id,
            InspectionRequired = false,
            Notes = $"Admin probe test DTJ line — {DateTime.UtcNow:O}"
        };

        _db.GoodsReceiptLines.Add(testLine);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Test DTJ GR line #{testLine.Id} created on GR {gr.Id}. " +
            $"PO line={poLine.Id}, PRO={pro?.Id}, BOM={bomLine?.Id}. " +
            $"Qty=10. Ready for ReceiveToJob.");

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. Load DTJ Lines — already loaded in OnGet/LoadStatsAsync
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostLoadDtjLinesAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, $"Loaded {RecentDtjLines.Count} DTJ lines.");
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. Load BOM Supply Links for a PRO
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostLoadBomSupplyAsync(CancellationToken ct)
    {
        BomSupplyLinks = await _db.Set<ProductionMaterialStructure>()
            .Where(b => b.ProductionOrderId == LoadProId)
            .OrderBy(b => b.Sequence)
            .Take(50)
            .ToListAsync(ct);

        Set(true, $"Loaded {BomSupplyLinks.Count} BOM lines for PRO {LoadProId}.");
        await LoadStatsAsync(ct);
        return Page();
    }
}
