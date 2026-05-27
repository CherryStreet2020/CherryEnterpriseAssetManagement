using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services.Engineering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 14.3 PR-4 (2026-05-27) — admin probe for IConcessionService.
//
// SIX WRITE BUTTONS (per the Lock 16 corollary from PR #365 — every
// probe must exercise the INSERT/UPDATE path before merge):
//   1. Create Concession
//   2. Submit Concession (Draft → Submitted)
//   3. Accept Concession (customer accepts non-conforming material)
//   4. Reject Concession (with RejectedDisposition + reason)
//   5. Close Concession (Accepted → Closed administrative close)
//   6. Get Concession (read form)
//
// Service-only DI per CHERRY025.
[Authorize(Roles = "Admin")]
public sealed class ConcessionProbeModel : PageModel
{
    private readonly IConcessionService _svc;
    private readonly ILogger<ConcessionProbeModel> _logger;

    public ConcessionProbeModel(IConcessionService svc, ILogger<ConcessionProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Create Concession ---
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreateConcessionNumber { get; set; } = "CON-2026-BRG-001";
    [BindProperty] public string? CreateTitle { get; set; } = "Retroactive acceptance of BRG-6207-2RS bearing with Ra 1.2 µm surface finish vs spec Ra 0.8 µm";
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public ConcessionType CreateType { get; set; } = ConcessionType.Cosmetic;
    [BindProperty] public int? CreateItemId { get; set; } = 9245; // BRG-6207-2RS
    [BindProperty] public int? CreateProductionOrderId { get; set; }
    [BindProperty] public decimal CreateAffectedQuantity { get; set; } = 50m;
    [BindProperty] public string? CreateAffectedLotSerials { get; set; }
    [BindProperty] public int? CreateCustomerId { get; set; }
    [BindProperty] public string? CreateOriginalSpec { get; set; } = "Ra 0.8 µm max per BAMS-3320 §4.2";
    [BindProperty] public string? CreateActualCondition { get; set; } = "Ra 1.2 µm measured on 3 of 50 bearings";
    [BindProperty] public string? CreateJustification { get; set; } = "Non-critical ground-support application — stress analysis confirms adequate margin";
    [BindProperty] public string? CreateDisposition { get; set; } = "Use as-is per customer acceptance for non-flight applications";
    [BindProperty] public string? CreateNcrReference { get; set; } = "NCR-2026-0042";

    // --- Submit ---
    [BindProperty] public int ConcessionId { get; set; }

    // --- Accept ---
    [BindProperty] public int AcceptConcessionId { get; set; }

    // --- Reject ---
    [BindProperty] public int RejectConcessionId { get; set; }
    [BindProperty] public RejectedDisposition RejectDisposition { get; set; } = RejectedDisposition.Rework;
    [BindProperty] public string? RejectReason { get; set; }

    // --- Close ---
    [BindProperty] public int CloseConcessionId { get; set; }

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetConcessionId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public Concession? ConcessionView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var conNum = CreateConcessionNumber ?? $"CON-2026-TEST-{ts}";

        var request = new CreateConcessionRequest(
            CompanyId: CreateCompanyId,
            ConcessionNumber: conNum,
            Title: CreateTitle ?? "Retroactive acceptance of BRG-6207-2RS bearing with Ra 1.2 µm surface finish vs spec Ra 0.8 µm",
            Type: CreateType,
            ItemId: CreateItemId,
            ProductionOrderId: CreateProductionOrderId,
            AffectedQuantity: CreateAffectedQuantity,
            AffectedLotSerials: CreateAffectedLotSerials,
            CustomerId: CreateCustomerId,
            OriginalSpecification: CreateOriginalSpec ?? "Ra 0.8 µm max per BAMS-3320 §4.2",
            ActualCondition: CreateActualCondition ?? "Ra 1.2 µm measured on 3 of 50 bearings",
            Justification: CreateJustification ?? "Non-critical ground-support application — stress analysis confirms adequate margin",
            Disposition: CreateDisposition ?? "Use as-is per customer acceptance for non-flight applications",
            Description: CreateDescription,
            RequestedBy: by,
            NcrReference: CreateNcrReference ?? "NCR-2026-0042");

        var r = await _svc.CreateAsync(request, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created Concession {r.Value!.Id} '{r.Value.ConcessionNumber}' Status={r.Value.Status} Type={r.Value.Type} Item={r.Value.ItemId} AffectedQty={r.Value.AffectedQuantity}."
            : r.Error);
        _logger.LogInformation("ConcessionProbe Create: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SubmitAsync(ConcessionId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Submitted Concession {r.Value!.Id} '{r.Value.ConcessionNumber}' → Status={r.Value.Status}."
            : r.Error);
        _logger.LogInformation("ConcessionProbe Submit: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostAcceptAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.AcceptAsync(AcceptConcessionId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Accepted Concession {r.Value!.Id} '{r.Value.ConcessionNumber}' → Status={r.Value.Status} AcceptedAt={r.Value.AcceptedAtUtc:u}."
            : r.Error);
        _logger.LogInformation("ConcessionProbe Accept: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRejectAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RejectAsync(RejectConcessionId, by, RejectDisposition, RejectReason ?? "(no reason given)", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Rejected Concession {r.Value!.Id} '{r.Value.ConcessionNumber}' → Status={r.Value.Status} Disposition={r.Value.RejectedDisposition} Reason='{r.Value.RejectionReason}'."
            : r.Error);
        _logger.LogInformation("ConcessionProbe Reject: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CloseAsync(CloseConcessionId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Closed Concession {r.Value!.Id} '{r.Value.ConcessionNumber}' → Status={r.Value.Status}."
            : r.Error);
        _logger.LogInformation("ConcessionProbe Close: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetConcessionAsync(CancellationToken ct)
    {
        if (GetConcessionId <= 0) { Set(false, "ConcessionId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetConcessionId > 0) ConcessionView = await _svc.GetAsync(GetConcessionId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
