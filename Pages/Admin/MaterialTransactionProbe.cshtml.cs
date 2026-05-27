using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B8 PR-PRO-3 (2026-05-27) — admin probe for IProductionMaterialTransactionService.
//
// EIGHT WRITE BUTTONS + 1 read per Lock 16 corollary:
//   1. Issue Material
//   2. Partial Issue
//   3. Over-Issue (reason required)
//   4. Return Material
//   5. Reverse Issue
//   6. Transfer to Job
//   7. Substitute Component
//   8. Scrap Component
//   9. Get Transaction (read)
[Authorize(Roles = "Admin")]
public sealed class MaterialTransactionProbeModel : PageModel
{
    private readonly IProductionMaterialTransactionService _svc;
    private readonly ILogger<MaterialTransactionProbeModel> _logger;

    public MaterialTransactionProbeModel(IProductionMaterialTransactionService svc, ILogger<MaterialTransactionProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Issue ---
    [BindProperty] public int IssueBomLineId { get; set; } = 1;
    [BindProperty] public decimal IssueQuantity { get; set; } = 10m;
    [BindProperty] public string? IssueLotNumber { get; set; } = "LOT-2026-SKF-0187";
    [BindProperty] public string? IssueFromWarehouse { get; set; } = "WH-MAIN";
    [BindProperty] public string? IssueFromBin { get; set; } = "BIN-A3-12";

    // --- Partial Issue ---
    [BindProperty] public int PartialBomLineId { get; set; }
    [BindProperty] public decimal PartialQuantity { get; set; } = 5m;
    [BindProperty] public string? PartialLotNumber { get; set; }

    // --- Over-Issue ---
    [BindProperty] public int OverIssueBomLineId { get; set; }
    [BindProperty] public decimal OverIssueQuantity { get; set; } = 25m;
    [BindProperty] public string? OverIssueReasonCode { get; set; } = "SCRAP-ALLOWANCE";
    [BindProperty] public string? OverIssueReasonDesc { get; set; } = "Additional 5 units to cover anticipated machining scrap on tight-tolerance bore";

    // --- Return ---
    [BindProperty] public int ReturnBomLineId { get; set; }
    [BindProperty] public decimal ReturnQuantity { get; set; } = 2m;
    [BindProperty] public string? ReturnToWarehouse { get; set; } = "WH-MAIN";
    [BindProperty] public string? ReturnToBin { get; set; } = "BIN-A3-12";

    // --- Reverse Issue ---
    [BindProperty] public int ReverseTransactionId { get; set; }

    // --- Transfer to Job ---
    [BindProperty] public int TransferSourceBomLineId { get; set; }
    [BindProperty] public int TransferDestProId { get; set; }
    [BindProperty] public int TransferDestBomLineId { get; set; }
    [BindProperty] public decimal TransferQuantity { get; set; } = 5m;
    [BindProperty] public string? TransferReason { get; set; } = "Rebalance material across concurrent production runs for BRG-6207-2RS";

    // --- Substitute ---
    [BindProperty] public int SubBomLineId { get; set; }
    [BindProperty] public int SubItemId { get; set; }
    [BindProperty] public decimal SubQuantity { get; set; } = 10m;
    [BindProperty] public string? SubReason { get; set; } = "Primary supplier backorder — approved alternate SKF 6207-2Z per ECN-2026-0019";
    [BindProperty] public string? SubAuthRef { get; set; } = "ECN-2026-0019";

    // --- Scrap ---
    [BindProperty] public int ScrapBomLineId { get; set; }
    [BindProperty] public decimal ScrapQuantity { get; set; } = 1m;
    [BindProperty] public string? ScrapReasonCode { get; set; } = "MACHINING-DEFECT";
    [BindProperty] public string? ScrapReasonDesc { get; set; } = "Inner race bore out-of-round after grinding — Ra 2.4 µm exceeds 0.8 µm spec";

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetTransactionId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public ProductionMaterialTransaction? TransactionView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostIssueAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.IssueAsync(new IssueMaterialRequest(
            BomLineId: IssueBomLineId, Quantity: IssueQuantity, PerformedBy: by,
            LotNumber: IssueLotNumber, FromWarehouse: IssueFromWarehouse, FromBin: IssueFromBin), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Issue #{r.Value!.Id} '{r.Value.TransactionNumber}' posted: {r.Value.Quantity:N4} {r.Value.Uom} Item={r.Value.ItemId} BomLine={r.Value.BomLineId} Cost={r.Value.ExtendedCost:N4}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostPartialIssueAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.PartialIssueAsync(new IssueMaterialRequest(
            BomLineId: PartialBomLineId, Quantity: PartialQuantity, PerformedBy: by,
            LotNumber: PartialLotNumber), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Partial Issue #{r.Value!.Id} '{r.Value.TransactionNumber}': {r.Value.Quantity:N4}. Before={r.Value.QuantityBeforeTransaction:N4} After={r.Value.QuantityAfterTransaction:N4}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostOverIssueAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.OverIssueAsync(new IssueMaterialRequest(
            BomLineId: OverIssueBomLineId, Quantity: OverIssueQuantity, PerformedBy: by),
            OverIssueReasonCode ?? "UNKNOWN", OverIssueReasonDesc ?? "Over-issue reason", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Over-Issue #{r.Value!.Id} '{r.Value.TransactionNumber}': {r.Value.Quantity:N4}. Reason={r.Value.ReasonCode}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostReturnAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReturnAsync(new ReturnMaterialRequest(
            BomLineId: ReturnBomLineId, Quantity: ReturnQuantity, PerformedBy: by,
            ToWarehouse: ReturnToWarehouse, ToBin: ReturnToBin), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Return #{r.Value!.Id} '{r.Value.TransactionNumber}': {r.Value.Quantity:N4} returned to {r.Value.ToWarehouse}/{r.Value.ToBin}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostReverseIssueAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReverseIssueAsync(ReverseTransactionId, by, "Probe reversal test", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Reversal #{r.Value!.Id} '{r.Value.TransactionNumber}': reversed original #{r.Value.OriginalTransactionId}. Qty={r.Value.Quantity:N4}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostTransferToJobAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.TransferToJobAsync(new TransferMaterialRequest(
            SourceBomLineId: TransferSourceBomLineId,
            DestinationProductionOrderId: TransferDestProId,
            DestinationBomLineId: TransferDestBomLineId,
            Quantity: TransferQuantity, PerformedBy: by,
            TransferReason: TransferReason ?? "Job rebalancing"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Transfer #{r.Value!.Id} '{r.Value.TransactionNumber}': {r.Value.Quantity:N4} transferred. Pair={r.Value.TransferPairId}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSubstituteAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SubstituteAsync(new SubstituteMaterialRequest(
            BomLineId: SubBomLineId, SubstituteItemId: SubItemId, Quantity: SubQuantity,
            PerformedBy: by, SubstitutionReason: SubReason ?? "Substitute",
            SubstitutionAuthReference: SubAuthRef), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Substitute #{r.Value!.Id} '{r.Value.TransactionNumber}': Item {r.Value.OriginalItemId} → Item {r.Value.ItemId}. Qty={r.Value.Quantity:N4}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostScrapAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ScrapComponentAsync(new ScrapMaterialRequest(
            BomLineId: ScrapBomLineId, Quantity: ScrapQuantity, PerformedBy: by,
            ReasonCode: ScrapReasonCode ?? "DEFECT",
            ReasonDescription: ScrapReasonDesc ?? "Material defect"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Scrap #{r.Value!.Id} '{r.Value.TransactionNumber}': {r.Value.Quantity:N4} scrapped. Reason={r.Value.ReasonCode}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetTransactionAsync(CancellationToken ct)
    {
        if (GetTransactionId <= 0) { Set(false, "TransactionId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetTransactionId > 0) TransactionView = await _svc.GetAsync(GetTransactionId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
