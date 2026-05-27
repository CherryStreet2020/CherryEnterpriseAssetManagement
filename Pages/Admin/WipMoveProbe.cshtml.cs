using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B8 PR-PRO-5 (2026-05-27) — admin probe for IProductionWipMoveService.
//
// NINE WRITE BUTTONS per Lock 16 corollary:
//   1. Auto-Advance on Completion (CREATE INSERT — the DEFAULT flow)
//   2. Manual Move to Next (CREATE INSERT)
//   3. Send Back to Prior (CREATE INSERT — rework exception)
//   4. Move to Specific Operation (CREATE INSERT — non-sequential exception)
//   5. Hold at Operation (UPDATE — quality hold)
//   6. Release Hold (UPDATE — triggers pending moves)
//   7. Reverse Move (CREATE INSERT — counter-move)
//   8. Get Moves for Order (READ)
//   9. Get Moves for Operation (READ)
[Authorize(Roles = "Admin")]
public sealed class WipMoveProbeModel : PageModel
{
    private readonly IProductionWipMoveService _svc;
    private readonly ILogger<WipMoveProbeModel> _logger;

    public WipMoveProbeModel(IProductionWipMoveService svc, ILogger<WipMoveProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Auto-Advance ---
    [BindProperty] public int AutoFromOpId { get; set; } = 1;
    [BindProperty] public decimal AutoQty { get; set; } = 50;
    [BindProperty] public int? AutoTriggerTxnId { get; set; }

    // --- Manual Move Next ---
    [BindProperty] public int ManualFromOpId { get; set; } = 1;
    [BindProperty] public decimal ManualQty { get; set; } = 25;
    [BindProperty] public string? ManualReason { get; set; } = "Pre-completion partial move — 25 units ready for grinding while remaining 25 finish CNC cycle";

    // --- Send Back ---
    [BindProperty] public int SendBackFromOpId { get; set; } = 2;
    [BindProperty] public int SendBackToOpId { get; set; } = 1;
    [BindProperty] public decimal SendBackQty { get; set; } = 5;
    [BindProperty] public string? SendBackReason { get; set; } = "Inner bore diameter out-of-tolerance (47.012 vs 47.005 max) — rework on Studer S33 CNC grinder";

    // --- Move to Specific ---
    [BindProperty] public int SpecificFromOpId { get; set; } = 1;
    [BindProperty] public int SpecificToOpId { get; set; } = 3;
    [BindProperty] public decimal SpecificQty { get; set; } = 10;
    [BindProperty] public string? SpecificReason { get; set; } = "Skip heat treatment — material already pre-hardened per supplier cert BAMS-3320 Rev D";

    // --- Hold ---
    [BindProperty] public int HoldOpId { get; set; } = 2;
    [BindProperty] public string? HoldReason { get; set; } = "FAI inspection required per ECO-2026-001 form/fit/function change — AS9102 §3.2";

    // --- Release Hold ---
    [BindProperty] public int ReleaseOpId { get; set; } = 2;

    // --- Reverse ---
    [BindProperty] public int ReverseMoveId { get; set; }
    [BindProperty] public string? ReverseReason { get; set; } = "Incorrect quantity moved — operator entered 50, actual completed was 45";

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int ReadOrderId { get; set; }
    [BindProperty(SupportsGet = true)] public int ReadOpId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public IReadOnlyList<ProductionWipMove>? MoveListView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    // 1. Auto-Advance
    public async Task<IActionResult> OnPostAutoAdvanceAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.AutoAdvanceOnCompletionAsync(AutoFromOpId, AutoQty, AutoTriggerTxnId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Auto-advance: Move {r.Value!.Id} '{r.Value.MoveNumber}' — Op {r.Value.FromSequenceNumber}→{r.Value.ToSequenceNumber}. " +
              $"{r.Value.Quantity} units. Status={r.Value.Status}. QualityHold={r.Value.QualityHoldBlocked}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 2. Manual Move Next
    public async Task<IActionResult> OnPostManualMoveAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MoveToNextOperationAsync(ManualFromOpId, ManualQty, ManualReason, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Manual move: Move {r.Value!.Id} '{r.Value.MoveNumber}' — Op {r.Value.FromSequenceNumber}→{r.Value.ToSequenceNumber}. {r.Value.Quantity} units."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 3. Send Back
    public async Task<IActionResult> OnPostSendBackAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SendBackToPriorOperationAsync(SendBackFromOpId, SendBackToOpId, SendBackQty, SendBackReason ?? "(rework)", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SEND-BACK: Move {r.Value!.Id} '{r.Value.MoveNumber}' — Op {r.Value.FromSequenceNumber}→{r.Value.ToSequenceNumber}. {r.Value.Quantity} units for rework."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 4. Move to Specific
    public async Task<IActionResult> OnPostMoveSpecificAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MoveToSpecificOperationAsync(SpecificFromOpId, SpecificToOpId, SpecificQty, SpecificReason ?? "(non-sequential)", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Non-sequential: Move {r.Value!.Id} '{r.Value.MoveNumber}' — Op {r.Value.FromSequenceNumber}→{r.Value.ToSequenceNumber}. {r.Value.Quantity} units."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 5. Hold
    public async Task<IActionResult> OnPostHoldAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.HoldAtOperationAsync(HoldOpId, HoldReason ?? "(quality hold)", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"HOLD placed on Op {r.Value!.SequenceNumber} (Id={r.Value.Id}). QualityHold={r.Value.QualityHoldActive}. Auto-advance BLOCKED."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 6. Release Hold
    public async Task<IActionResult> OnPostReleaseHoldAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReleaseHoldAsync(ReleaseOpId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"HOLD released on Op {r.Value!.SequenceNumber} (Id={r.Value.Id}). Available={r.Value.AvailableQty}. Pending moves executed."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 7. Reverse
    public async Task<IActionResult> OnPostReverseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReverseMoveAsync(ReverseMoveId, ReverseReason ?? "(reversal)", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"REVERSED: Move {r.Value!.Id} '{r.Value.MoveNumber}'. Original move marked Reversed."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 8+9. Read
    public async Task<IActionResult> OnPostGetMovesAsync(CancellationToken ct) => await ReloadAsync(ct);

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (ReadOrderId > 0)
            MoveListView = await _svc.GetMovesForOrderAsync(ReadOrderId, ct);
        else if (ReadOpId > 0)
            MoveListView = await _svc.GetMovesForOperationAsync(ReadOpId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
