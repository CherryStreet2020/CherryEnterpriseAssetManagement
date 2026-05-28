// Sprint 15.2 PR-8 — admin probe for ISubcontractCostingService.
// 8 write/action buttons covering all 4 cost-posting paths + summary read.

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count queries. All writes flow through ISubcontractCostingService and ultimately ICostTransactionService.")]
public sealed class SubcontractCostingProbeModel : PageModel
{
    private readonly ISubcontractCostingService _costing;
    private readonly AppDbContext _db;
    private readonly ILogger<SubcontractCostingProbeModel> _logger;

    public SubcontractCostingProbeModel(
        ISubcontractCostingService costing,
        AppDbContext db,
        ILogger<SubcontractCostingProbeModel> logger)
    {
        _costing = costing;
        _db = db;
        _logger = logger;
    }

    // ── Ship-time costs ──
    [BindProperty] public int ShipCostOpId { get; set; } = 1;
    [BindProperty] public int ShipCostShipmentId { get; set; } = 1;
    [BindProperty] public decimal? ShipCostFreightOut { get; set; } = 45m;
    [BindProperty] public decimal? ShipCostPackaging { get; set; } = 15m;
    [BindProperty] public decimal? ShipCostExpedite { get; set; }

    // ── Receipt-time costs ──
    [BindProperty] public int RecvCostOpId { get; set; } = 1;
    [BindProperty] public int RecvCostReceiptId { get; set; } = 1;
    [BindProperty] public decimal RecvCostQtyAccepted { get; set; } = 100m;
    [BindProperty] public decimal? RecvCostServiceUnitCost { get; set; } = 8.50m;
    [BindProperty] public decimal? RecvCostFreightReturn { get; set; } = 35m;
    [BindProperty] public decimal? RecvCostCertFee { get; set; } = 25m;
    [BindProperty] public decimal? RecvCostInspectionFee { get; set; } = 20m;
    [BindProperty] public decimal? RecvCostScrapCharge { get; set; }
    [BindProperty] public decimal? RecvCostVendorCredit { get; set; }

    // ── Invoice true-up ──
    [BindProperty] public int InvoiceOpId { get; set; } = 1;
    [BindProperty] public decimal InvoiceAmount { get; set; } = 875m;
    [BindProperty] public decimal InvoiceQuantity { get; set; } = 100m;
    [BindProperty] public string? InvoiceNumber { get; set; } = "VENDOR-INV-2026-001";

    // ── Close settlement ──
    [BindProperty] public int CloseOpId { get; set; } = 1;

    // ── Summary read ──
    [BindProperty] public int SummaryOpId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalSubcontractOps { get; private set; }
    public int TotalCostTransactions { get; private set; }
    public int SubcontractCostTransactions { get; private set; }
    public decimal SubcontractCostTotalUsd { get; private set; }

    public SubcontractCostSummary? CostSummary { get; private set; }
    public SubcontractCostPostResult? LastPostResult { get; private set; }

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalSubcontractOps = await _db.Set<SubcontractOperation>().CountAsync(ct);
        TotalCostTransactions = await _db.Set<CostTransaction>().CountAsync(ct);
        SubcontractCostTransactions = await _db.Set<CostTransaction>()
            .CountAsync(t => t.SourceTransactionType == "SubcontractShipment" ||
                              t.SourceTransactionType == "SubcontractReceipt" ||
                              t.SourceTransactionType == "SubcontractClose" ||
                              (t.SourceTransactionType != null && t.SourceTransactionType.StartsWith("SubcontractInvoice")), ct);
        SubcontractCostTotalUsd = await _db.Set<CostTransaction>()
            .Where(t => t.SourceTransactionType == "SubcontractShipment" ||
                         t.SourceTransactionType == "SubcontractReceipt" ||
                         t.SourceTransactionType == "SubcontractClose" ||
                         (t.SourceTransactionType != null && t.SourceTransactionType.StartsWith("SubcontractInvoice")))
            .SumAsync(t => (decimal?)t.ExtendedCost, ct) ?? 0m;
    }

    // 1) POST SHIPMENT COSTS (freight-out + packaging + expedite)
    public async Task<IActionResult> OnPostShipmentCostsAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costing.PostShipmentCostsAsync(new PostShipmentCostsRequest(
            SubcontractOperationId: ShipCostOpId,
            SubcontractShipmentId: ShipCostShipmentId,
            FreightOutCost: ShipCostFreightOut,
            PackagingCost: ShipCostPackaging,
            ExpediteCost: ShipCostExpedite,
            CurrencyCode: "USD",
            PostedBy: by,
            Notes: "Admin probe — shipment cost"), ct);
        if (r.IsSuccess) LastPostResult = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Posted {r.Value!.CostTransactionsPosted} ship-time transaction(s), total ${r.Value.TotalExtendedCost:N4}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) POST RECEIPT COSTS (service + freight + cert + inspection + scrap + credit)
    public async Task<IActionResult> OnPostReceiptCostsAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costing.PostReceiptCostsAsync(new PostReceiptCostsRequest(
            SubcontractOperationId: RecvCostOpId,
            SubcontractReceiptId: RecvCostReceiptId,
            QuantityAccepted: RecvCostQtyAccepted,
            ServiceUnitCost: RecvCostServiceUnitCost,
            FreightReturnCost: RecvCostFreightReturn,
            CertFee: RecvCostCertFee,
            InspectionFee: RecvCostInspectionFee,
            ScrapChargeAtVendor: RecvCostScrapCharge,
            VendorCredit: RecvCostVendorCredit,
            CurrencyCode: "USD",
            PostedBy: by,
            Notes: "Admin probe — receipt cost"), ct);
        if (r.IsSuccess) LastPostResult = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Posted {r.Value!.CostTransactionsPosted} receipt-time transaction(s), total ${r.Value.TotalExtendedCost:N4}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) POST INVOICE TRUE-UP (variance + PPV)
    public async Task<IActionResult> OnPostInvoiceTrueUpAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costing.PostInvoiceTrueUpAsync(new PostInvoiceTrueUpRequest(
            SubcontractOperationId: InvoiceOpId,
            InvoicedAmount: InvoiceAmount,
            QuantityInvoiced: InvoiceQuantity,
            InvoiceNumber: InvoiceNumber,
            CurrencyCode: "USD",
            PostedBy: by,
            Notes: "Admin probe — invoice true-up"), ct);
        if (r.IsSuccess) LastPostResult = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Invoice true-up: {r.Value!.CostTransactionsPosted} variance transaction(s), total ${r.Value.TotalExtendedCost:N4}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) SETTLE AT CLOSE
    public async Task<IActionResult> OnPostSettleAtCloseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costing.SettleAtCloseAsync(new SettleAtCloseRequest(
            SubcontractOperationId: CloseOpId,
            PostedBy: by,
            Notes: "Admin probe — close settlement"), ct);
        if (r.IsSuccess) LastPostResult = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Close: {r.Value!.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) LOAD COST SUMMARY (read by §12 element)
    public async Task<IActionResult> OnPostLoadSummaryAsync(CancellationToken ct)
    {
        var r = await _costing.GetCostSummaryAsync(SummaryOpId, ct);
        if (r.IsSuccess) CostSummary = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Loaded cost summary for op #{SummaryOpId}: total ${r.Value!.TotalSubcontractCost:N4} across {r.Value.TotalTransactionCount} transactions."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6-8: idempotency demonstration buttons — re-running the same post twice
    //     should yield count=0 on the second call (the inline idempotency
    //     guard suppresses duplicates).

    public async Task<IActionResult> OnPostShipmentCostsAgainAsync(CancellationToken ct) =>
        await OnPostShipmentCostsAsync(ct);

    public async Task<IActionResult> OnPostReceiptCostsAgainAsync(CancellationToken ct) =>
        await OnPostReceiptCostsAsync(ct);

    public async Task<IActionResult> OnPostInvoiceTrueUpAgainAsync(CancellationToken ct) =>
        await OnPostInvoiceTrueUpAsync(ct);
}
