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

// Sprint 14.3 PR-3 (2026-05-27) — admin probe for IWaiverService.
//
// SEVEN WRITE BUTTONS (per the Lock 16 corollary from PR #365 — every
// probe must exercise the INSERT/UPDATE path before merge):
//   1. Create Waiver
//   2. Submit Waiver (Draft → Submitted)
//   3. Approve Waiver (auto-activates if no effective date)
//   4. Reject Waiver
//   5. Revoke Waiver (customer withdraws — unique to Waiver, not on Deviation)
//   6. Record Consumption (quantity against Active waiver)
//   7. Get Waiver (read form)
//
// Service-only DI per CHERRY025.
[Authorize(Roles = "Admin")]
public sealed class WaiverProbeModel : PageModel
{
    private readonly IWaiverService _svc;
    private readonly ILogger<WaiverProbeModel> _logger;

    public WaiverProbeModel(IWaiverService svc, ILogger<WaiverProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Create Waiver ---
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreateWaiverNumber { get; set; } = "WVR-2026-BRG-001";
    [BindProperty] public string? CreateTitle { get; set; } = "Customer-approved surface finish waiver on bearing outer race";
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public WaiverType CreateType { get; set; } = WaiverType.Material;
    [BindProperty] public int? CreateItemId { get; set; } = 9245; // BRG-6207-2RS
    [BindProperty] public int? CreateCustomerId { get; set; }
    [BindProperty] public int? CreateProductionOrderId { get; set; }
    [BindProperty] public int? CreateOriginatingEcrId { get; set; }
    [BindProperty] public int? CreateRelatedDeviationId { get; set; }
    [BindProperty] public string? CreateCustomerContractReference { get; set; }
    [BindProperty] public decimal? CreateMaxQuantity { get; set; } = 1000m;
    [BindProperty] public DateTime? CreateEffectiveFrom { get; set; }
    [BindProperty] public DateTime? CreateExpirationDate { get; set; }
    [BindProperty] public string? CreateOriginalSpec { get; set; } = "Ra 0.8 µm max per BAMS-3320 §4.2";
    [BindProperty] public string? CreateWaivedCondition { get; set; } = "Ra 1.2 µm measured — customer accepts for non-flight application";
    [BindProperty] public string? CreateJustification { get; set; } = "Customer accepts for non-flight ground-support equipment";
    [BindProperty] public string? CreateDisposition { get; set; } = "Use as-is per customer approval for non-flight applications";

    // --- Submit ---
    [BindProperty] public int WaiverId { get; set; }

    // --- Approve ---
    [BindProperty] public int ApproveWaiverId { get; set; }

    // --- Reject ---
    [BindProperty] public int RejectWaiverId { get; set; }
    [BindProperty] public string? RejectReason { get; set; }

    // --- Revoke ---
    [BindProperty] public int RevokeWaiverId { get; set; }
    [BindProperty] public string? RevokeReason { get; set; }

    // --- Record Consumption ---
    [BindProperty] public int ConsumeWaiverId { get; set; }
    [BindProperty] public decimal ConsumeQuantity { get; set; } = 25m;

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetWaiverId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public Waiver? WaiverView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var wvrNum = CreateWaiverNumber ?? $"WVR-2026-TEST-{ts}";

        var request = new CreateWaiverRequest(
            CompanyId: CreateCompanyId,
            WaiverNumber: wvrNum,
            Title: CreateTitle ?? "Customer-approved surface finish waiver on bearing outer race",
            Type: CreateType,
            ItemId: CreateItemId,
            CustomerId: CreateCustomerId,
            ProductionOrderId: CreateProductionOrderId,
            OriginatingEcrId: CreateOriginatingEcrId,
            RelatedDeviationId: CreateRelatedDeviationId,
            CustomerContractReference: CreateCustomerContractReference,
            MaxQuantity: CreateMaxQuantity,
            EffectiveFromUtc: CreateEffectiveFrom,
            ExpirationDateUtc: CreateExpirationDate,
            OriginalSpecification: CreateOriginalSpec ?? "Ra 0.8 µm max per BAMS-3320 §4.2",
            WaivedCondition: CreateWaivedCondition ?? "Ra 1.2 µm measured — customer accepts for non-flight application",
            Justification: CreateJustification ?? "Customer accepts for non-flight ground-support equipment",
            Disposition: CreateDisposition ?? "Use as-is per customer approval for non-flight applications",
            Description: CreateDescription,
            RequestedBy: by);

        var r = await _svc.CreateAsync(request, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created Waiver {r.Value!.Id} '{r.Value.WaiverNumber}' Status={r.Value.Status} Type={r.Value.Type} Item={r.Value.ItemId} MaxQty={r.Value.MaxQuantity}."
            : r.Error);
        _logger.LogInformation("WaiverProbe Create: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SubmitAsync(WaiverId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Submitted Waiver {r.Value!.Id} '{r.Value.WaiverNumber}' → Status={r.Value.Status}."
            : r.Error);
        _logger.LogInformation("WaiverProbe Submit: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ApproveAsync(ApproveWaiverId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Approved Waiver {r.Value!.Id} '{r.Value.WaiverNumber}' → Status={r.Value.Status} ApprovedAt={r.Value.ApprovedAtUtc:u}."
            : r.Error);
        _logger.LogInformation("WaiverProbe Approve: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRejectAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RejectAsync(RejectWaiverId, by, RejectReason ?? "(no reason given)", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Rejected Waiver {r.Value!.Id} '{r.Value.WaiverNumber}' → Status={r.Value.Status} Reason='{r.Value.RejectionReason}'."
            : r.Error);
        _logger.LogInformation("WaiverProbe Reject: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRevokeAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RevokeAsync(RevokeWaiverId, by, RevokeReason ?? "(no reason given)", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Revoked Waiver {r.Value!.Id} '{r.Value.WaiverNumber}' → Status={r.Value.Status} RevokedAt={r.Value.RevokedAtUtc:u}."
            : r.Error);
        _logger.LogInformation("WaiverProbe Revoke: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRecordConsumptionAsync(CancellationToken ct)
    {
        var r = await _svc.RecordConsumptionAsync(ConsumeWaiverId, ConsumeQuantity, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Recorded consumption on Waiver {r.Value!.Id} '{r.Value.WaiverNumber}' — Consumed={r.Value.ConsumedQuantity:N4} / Max={r.Value.MaxQuantity?.ToString("N4") ?? "unlimited"}."
            : r.Error);
        _logger.LogInformation("WaiverProbe RecordConsumption: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetWaiverAsync(CancellationToken ct)
    {
        if (GetWaiverId <= 0) { Set(false, "WaiverId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetWaiverId > 0) WaiverView = await _svc.GetAsync(GetWaiverId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
