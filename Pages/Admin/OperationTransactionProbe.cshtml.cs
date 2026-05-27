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

// B8 PR-PRO-4 (2026-05-27) — admin probe for IProductionOperationTransactionService.
// 8 WRITE BUTTONS covering key state transitions + 1 read.
[Authorize(Roles = "Admin")]
public sealed class OperationTransactionProbeModel : PageModel
{
    private readonly IProductionOperationTransactionService _svc;
    private readonly ILogger<OperationTransactionProbeModel> _logger;
    public OperationTransactionProbeModel(IProductionOperationTransactionService svc, ILogger<OperationTransactionProbeModel> logger)
    { _svc = svc; _logger = logger; }

    // --- Start ---
    [BindProperty] public int StartOpId { get; set; } = 1;
    // --- Pause ---
    [BindProperty] public int PauseOpId { get; set; }
    [BindProperty] public string? PauseReason { get; set; } = "Operator break — awaiting tool change completion on adjacent grinder";
    // --- Complete ---
    [BindProperty] public int CompleteOpId { get; set; }
    [BindProperty] public decimal CompleteGoodQty { get; set; } = 50m;
    [BindProperty] public decimal CompleteScrapQty { get; set; } = 2m;
    [BindProperty] public string? CompleteScrapReason { get; set; } = "BORE-OOR";
    // --- Skip ---
    [BindProperty] public int SkipOpId { get; set; }
    [BindProperty] public string? SkipReason { get; set; } = "Optional deburr operation — customer-supplied bearings pre-deburred";
    // --- LogTime ---
    [BindProperty] public int LogTimeOpId { get; set; }
    [BindProperty] public decimal LogRunMins { get; set; } = 45m;
    [BindProperty] public decimal LogSetupMins { get; set; } = 12m;
    // --- ChangeResource ---
    [BindProperty] public int ChangeResOpId { get; set; }
    [BindProperty] public int NewWcId { get; set; } = 2;
    // --- ReverseCompletion ---
    [BindProperty] public int ReverseOpId { get; set; }
    // --- AddOperation ---
    [BindProperty] public int AddOpProId { get; set; } = 1;
    [BindProperty] public int AddOpAfterSeq { get; set; } = 20;
    [BindProperty] public string? AddOpDesc { get; set; } = "Additional demagnetization pass — residual magnetism detected post-grinding";
    [BindProperty] public int AddOpWcId { get; set; } = 1;
    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetTxnId { get; set; }
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public ProductionOperationTransaction? TxnView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostStartAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.StartAsync(StartOpId, by, ct: ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Start #{r.Value!.Id} '{r.Value.TransactionNumber}': Op {r.Value.OperationSequence} {r.Value.StatusBefore}→{r.Value.StatusAfter}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostPauseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.PauseAsync(PauseOpId, by, PauseReason, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Pause #{r.Value!.Id}: Op {r.Value.OperationSequence} {r.Value.StatusBefore}→{r.Value.StatusAfter}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCompleteAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CompleteAsync(new CompleteOperationRequest(
            OperationId: CompleteOpId, PerformedBy: by, GoodQuantity: CompleteGoodQty,
            ScrapQuantity: CompleteScrapQty, ScrapReasonCode: CompleteScrapReason), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Complete #{r.Value!.Id}: Op {r.Value.OperationSequence} good={r.Value.GoodQuantity:N0} scrap={r.Value.ScrapQuantity:N0} {r.Value.StatusBefore}→{r.Value.StatusAfter}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSkipAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SkipOperationAsync(SkipOpId, by, SkipReason ?? "Optional operation skipped", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Skip #{r.Value!.Id}: Op {r.Value.OperationSequence} → Skipped."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostLogTimeAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.LogTimeAsync(new LogTimeRequest(
            OperationId: LogTimeOpId, PerformedBy: by, RunMinutes: LogRunMins, SetupMinutes: LogSetupMins), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"LogTime #{r.Value!.Id}: Op {r.Value.OperationSequence} run={r.Value.RunMinutes}m setup={r.Value.SetupMinutes}m."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostChangeResourceAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ChangeResourceAsync(ChangeResOpId, NewWcId, null, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"ChangeResource #{r.Value!.Id}: Op {r.Value.OperationSequence} WC {r.Value.PreviousWorkCenterId}→{r.Value.WorkCenterId}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostReverseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReverseCompletionAsync(ReverseOpId, by, "Probe reversal test", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Reverse #{r.Value!.Id}: Op {r.Value.OperationSequence} {r.Value.StatusBefore}→{r.Value.StatusAfter}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostAddOpAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.AddOperationAsync(new AddOperationRequest(
            ProductionOrderId: AddOpProId, AfterOperationSequence: AddOpAfterSeq,
            Description: AddOpDesc ?? "Additional operation", WorkCenterId: AddOpWcId, PerformedBy: by), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"AddOp #{r.Value!.Id}: new op {r.Value.NewOperationSequence} on PRO {r.Value.ProductionOrderId}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetTxnAsync(CancellationToken ct)
    {
        if (GetTxnId <= 0) { Set(false, "TxnId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetTxnId > 0) TxnView = await _svc.GetAsync(GetTxnId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
