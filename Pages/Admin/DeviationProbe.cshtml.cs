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

// Sprint 14.3 PR-2 (2026-05-27) — admin probe for IDeviationService.
//
// SIX WRITE BUTTONS (per the Lock 16 corollary from PR #365 — every
// probe must exercise the INSERT/UPDATE path before merge):
//   1. Create Deviation
//   2. Submit Deviation
//   3. Approve Deviation (auto-activates if no effective date)
//   4. Reject Deviation
//   5. Record Consumption (quantity against Active deviation)
//   6. Close Deviation (manual close before expiry)
// Plus a Get-by-ID read form.
//
// Service-only DI per CHERRY025.
[Authorize(Roles = "Admin")]
public sealed class DeviationProbeModel : PageModel
{
    private readonly IDeviationService _svc;
    private readonly ILogger<DeviationProbeModel> _logger;

    public DeviationProbeModel(IDeviationService svc, ILogger<DeviationProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Create Deviation ---
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreateDeviationNumber { get; set; }
    [BindProperty] public string? CreateTitle { get; set; }
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public DeviationType CreateType { get; set; } = DeviationType.Material;
    [BindProperty] public int? CreateItemId { get; set; } = 9245; // BRG-6207-2RS
    [BindProperty] public int? CreateProductionOrderId { get; set; }
    [BindProperty] public int? CreateOriginatingEcrId { get; set; }
    [BindProperty] public decimal? CreateMaxQuantity { get; set; } = 500m;
    [BindProperty] public DateTime? CreateEffectiveFrom { get; set; }
    [BindProperty] public DateTime? CreateExpirationDate { get; set; }
    [BindProperty] public int? CreateMaxProductionOrders { get; set; }
    [BindProperty] public bool CreateAffectsForm { get; set; } = true;
    [BindProperty] public bool CreateAffectsFit { get; set; } = false;
    [BindProperty] public bool CreateAffectsFunction { get; set; } = false;
    [BindProperty] public bool CreateSafetyImpact { get; set; } = false;
    [BindProperty] public bool CreateCustomerApprovalRequired { get; set; } = false;
    [BindProperty] public string? CreateOriginalSpec { get; set; }
    [BindProperty] public string? CreateDeviatedCondition { get; set; }
    [BindProperty] public string? CreateJustification { get; set; }
    [BindProperty] public string? CreateDisposition { get; set; }

    // --- Submit ---
    [BindProperty] public int DeviationId { get; set; }

    // --- Approve ---
    [BindProperty] public int ApproveDeviationId { get; set; }

    // --- Reject ---
    [BindProperty] public int RejectDeviationId { get; set; }
    [BindProperty] public string? RejectReason { get; set; }

    // --- Record Consumption ---
    [BindProperty] public int ConsumeDeviationId { get; set; }
    [BindProperty] public decimal ConsumeQuantity { get; set; } = 25m;

    // --- Close ---
    [BindProperty] public int CloseDeviationId { get; set; }

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetDeviationId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public Deviation? DeviationView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var devNum = CreateDeviationNumber ?? $"DEV-2026-TEST-{ts}";

        var request = new CreateDeviationRequest(
            CompanyId: CreateCompanyId,
            DeviationNumber: devNum,
            Title: CreateTitle ?? "Dimensional tolerance deviation on bearing inner race",
            Type: CreateType,
            ItemId: CreateItemId,
            ProductionOrderId: CreateProductionOrderId,
            OriginatingEcrId: CreateOriginatingEcrId,
            MaxQuantity: CreateMaxQuantity,
            EffectiveFromUtc: CreateEffectiveFrom,
            ExpirationDateUtc: CreateExpirationDate,
            MaxProductionOrders: CreateMaxProductionOrders,
            AffectsForm: CreateAffectsForm,
            AffectsFit: CreateAffectsFit,
            AffectsFunction: CreateAffectsFunction,
            SafetyImpact: CreateSafetyImpact,
            CustomerApprovalRequired: CreateCustomerApprovalRequired,
            OriginalSpecification: CreateOriginalSpec ?? "AMS 6520 ±0.002 dia",
            DeviatedCondition: CreateDeviatedCondition ?? "0.498 dia — 0.002 below nominal",
            Justification: CreateJustification ?? "Non-critical application, stress analysis confirms adequate margin",
            Disposition: CreateDisposition ?? "Use as-is for non-critical applications only",
            Description: CreateDescription,
            RequestedBy: by);

        var r = await _svc.CreateAsync(request, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created Deviation {r.Value!.Id} '{r.Value.DeviationNumber}' Status={r.Value.Status} Type={r.Value.Type} Item={r.Value.ItemId} MaxQty={r.Value.MaxQuantity}."
            : r.Error);
        _logger.LogInformation("DeviationProbe Create: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SubmitAsync(DeviationId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Submitted Deviation {r.Value!.Id} '{r.Value.DeviationNumber}' → Status={r.Value.Status}."
            : r.Error);
        _logger.LogInformation("DeviationProbe Submit: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ApproveAsync(ApproveDeviationId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Approved Deviation {r.Value!.Id} '{r.Value.DeviationNumber}' → Status={r.Value.Status} ApprovedAt={r.Value.ApprovedAtUtc:u}."
            : r.Error);
        _logger.LogInformation("DeviationProbe Approve: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRejectAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RejectAsync(RejectDeviationId, by, RejectReason ?? "(no reason given)", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Rejected Deviation {r.Value!.Id} '{r.Value.DeviationNumber}' → Status={r.Value.Status} Reason='{r.Value.RejectionReason}'."
            : r.Error);
        _logger.LogInformation("DeviationProbe Reject: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRecordConsumptionAsync(CancellationToken ct)
    {
        var r = await _svc.RecordConsumptionAsync(ConsumeDeviationId, ConsumeQuantity, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Recorded consumption on Deviation {r.Value!.Id} '{r.Value.DeviationNumber}' — Consumed={r.Value.ConsumedQuantity:N4} / Max={r.Value.MaxQuantity?.ToString("N4") ?? "unlimited"}."
            : r.Error);
        _logger.LogInformation("DeviationProbe RecordConsumption: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CloseAsync(CloseDeviationId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Closed Deviation {r.Value!.Id} '{r.Value.DeviationNumber}' → Status={r.Value.Status}."
            : r.Error);
        _logger.LogInformation("DeviationProbe Close: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetDeviationAsync(CancellationToken ct)
    {
        if (GetDeviationId <= 0) { Set(false, "DeviationId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetDeviationId > 0) DeviationView = await _svc.GetAsync(GetDeviationId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
