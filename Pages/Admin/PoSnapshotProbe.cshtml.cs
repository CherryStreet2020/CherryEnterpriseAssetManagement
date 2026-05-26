using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 14.1 PR-1 (2026-05-26) — admin probe for IPoSnapshotService.
//
// Three operations:
//   • Get — read the current snapshot (no writes; safe to spam).
//   • Capture — freeze the source MaterialStructure into per-PO snapshot
//     rows. Idempotent: re-clicking after capture returns the existing
//     snapshot without re-writing.
//   • Clear — admin-only recovery path. Deletes snapshot rows + nulls
//     header timestamps so a subsequent Capture can re-freeze.
//
// Service-only DI per CHERRY025.
[Authorize(Roles = "Admin")]
public sealed class PoSnapshotProbeModel : PageModel
{
    private readonly IPoSnapshotService _svc;
    private readonly ILogger<PoSnapshotProbeModel> _logger;

    public PoSnapshotProbeModel(IPoSnapshotService svc, ILogger<PoSnapshotProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int ProductionOrderId { get; set; }

    [BindProperty]
    public string? CapturedBy { get; set; }

    [BindProperty]
    public string? ClearReason { get; set; }

    public PoSnapshotSummary? Snapshot { get; private set; }
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (ProductionOrderId > 0)
        {
            Snapshot = await _svc.GetSnapshotAsync(ProductionOrderId, ct);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostGetAsync(CancellationToken ct)
    {
        if (ProductionOrderId <= 0)
        {
            Outcome = "ProductionOrderId is required (must be > 0).";
            OutcomeIsError = true;
            return Page();
        }
        Snapshot = await _svc.GetSnapshotAsync(ProductionOrderId, ct);
        _logger.LogInformation(
            "PoSnapshotProbe Get: PRO={PoId} CapturedAt={At} Lines={Count}",
            ProductionOrderId, Snapshot.SnapshotCapturedAtUtc, Snapshot.Lines.Count);
        return Page();
    }

    public async Task<IActionResult> OnPostCaptureAsync(CancellationToken ct)
    {
        if (ProductionOrderId <= 0)
        {
            Outcome = "ProductionOrderId is required (must be > 0).";
            OutcomeIsError = true;
            return Page();
        }
        var by = string.IsNullOrWhiteSpace(CapturedBy)
            ? (User.Identity?.Name ?? "admin-probe")
            : CapturedBy!;

        var result = await _svc.CaptureAsync(ProductionOrderId, by, ct);
        if (result.IsSuccess)
        {
            Snapshot = result.Value;
            Outcome = $"Snapshot captured ({Snapshot!.Lines.Count} lines). Re-click returns the same result (idempotent).";
            OutcomeIsError = false;
        }
        else
        {
            Outcome = result.Error;
            OutcomeIsError = true;
            Snapshot = await _svc.GetSnapshotAsync(ProductionOrderId, ct);
        }
        _logger.LogInformation(
            "PoSnapshotProbe Capture: PRO={PoId} By={By} Success={Ok} Error={Err}",
            ProductionOrderId, by, result.IsSuccess, result.Error);
        return Page();
    }

    public async Task<IActionResult> OnPostClearAsync(CancellationToken ct)
    {
        if (ProductionOrderId <= 0)
        {
            Outcome = "ProductionOrderId is required (must be > 0).";
            OutcomeIsError = true;
            return Page();
        }
        var reason = string.IsNullOrWhiteSpace(ClearReason) ? "admin-probe-clear" : ClearReason!;
        var by = User.Identity?.Name ?? "admin-probe";

        var result = await _svc.ClearSnapshotAsync(ProductionOrderId, by, reason, ct);
        if (result.IsSuccess)
        {
            Snapshot = result.Value;
            Outcome = "Snapshot cleared. Capture can now re-freeze against the current source MaterialStructure.";
            OutcomeIsError = false;
        }
        else
        {
            Outcome = result.Error;
            OutcomeIsError = true;
            Snapshot = await _svc.GetSnapshotAsync(ProductionOrderId, ct);
        }
        _logger.LogWarning(
            "PoSnapshotProbe Clear: PRO={PoId} By={By} Reason={Reason} Success={Ok} Error={Err}",
            ProductionOrderId, by, reason, result.IsSuccess, result.Error);
        return Page();
    }
}
