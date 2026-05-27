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

// Sprint 14.3 PR-1 (2026-05-27) — admin probe for IEcrEcoService.
//
// EIGHT WRITE BUTTONS (per the Lock 16 corollary from PR #365 — every
// probe must exercise the INSERT/UPDATE path before merge):
//   1. Create ECR
//   2. Submit ECR
//   3. Approve ECR (atomic ECO creation)
//   4. Reject ECR
//   5. Add ECO Line Item
//   6. Add ECO Approval Stage
//   7. Approve ECO Stage
//   8. Release ECO (atomic DocumentVersion supersede)
// Plus Implement / Close lifecycle buttons + Get-by-ECR and Get-by-ECO reads.
//
// Service-only DI per CHERRY025.
[Authorize(Roles = "Admin")]
public sealed class EcrEcoProbeModel : PageModel
{
    private readonly IEcrEcoService _svc;
    private readonly ILogger<EcrEcoProbeModel> _logger;

    public EcrEcoProbeModel(IEcrEcoService svc, ILogger<EcrEcoProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // Create ECR
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreateEcrNumber { get; set; }
    [BindProperty] public string? CreateTitle { get; set; }
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public ChangeReason CreateChangeReason { get; set; } = ChangeReason.CustomerRequest;
    [BindProperty] public ChangeUrgency CreateUrgency { get; set; } = ChangeUrgency.Routine;
    [BindProperty] public bool CreateAffectsForm { get; set; } = false;
    [BindProperty] public bool CreateAffectsFit { get; set; } = false;
    [BindProperty] public bool CreateAffectsFunction { get; set; } = false;
    [BindProperty] public bool CreateAffectsSafety { get; set; } = false;
    [BindProperty] public bool CreateAffectsCustomers { get; set; } = false;
    [BindProperty] public bool CreateAffectsRegulatory { get; set; } = false;
    [BindProperty] public int? CreateLinkedItemId { get; set; }
    [BindProperty] public int? CreateLinkedDocumentId { get; set; }

    // Submit ECR
    [BindProperty] public int LifecycleEcrId { get; set; }

    // Approve ECR
    [BindProperty] public int ApproveEcrId { get; set; }
    [BindProperty] public string? ApproveEcoNumber { get; set; }
    [BindProperty] public string? ApproveEcoTitle { get; set; }
    [BindProperty] public EcoEffectivityType ApproveEffectivityType { get; set; } = EcoEffectivityType.Immediate;

    // Reject ECR
    [BindProperty] public int RejectEcrId { get; set; }
    [BindProperty] public string? RejectReason { get; set; }

    // Add ECO Line Item
    [BindProperty] public int AddLineEcoId { get; set; }
    [BindProperty] public int? AddLineItemId { get; set; }
    [BindProperty] public int? AddLineDocumentId { get; set; }
    [BindProperty] public int? AddLineAffectedDocVersionId { get; set; }
    [BindProperty] public int? AddLineNewDocVersionId { get; set; }
    [BindProperty] public string? AddLineChangeDescription { get; set; }
    [BindProperty] public string? AddLineBefore { get; set; }
    [BindProperty] public string? AddLineAfter { get; set; }
    [BindProperty] public EcoLineItemDisposition AddLineDisposition { get; set; } = EcoLineItemDisposition.NotApplicable;

    // Add Approval Stage
    [BindProperty] public int AddStageEcoId { get; set; }
    [BindProperty] public int AddStageOrder { get; set; } = 1;
    [BindProperty] public string? AddStageRole { get; set; }
    [BindProperty] public string? AddStageRequiredApprover { get; set; }

    // Approve Stage
    [BindProperty] public int ApproveStageId { get; set; }
    [BindProperty] public string? ApproveStageNotes { get; set; }

    // Release / Implement / Close
    [BindProperty] public int LifecycleEcoId { get; set; }

    // Read forms
    [BindProperty(SupportsGet = true)] public int GetEcrId { get; set; }
    [BindProperty(SupportsGet = true)] public int GetEcoId { get; set; }

    // Output
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public EngineeringChangeRequest? EcrView { get; private set; }
    public EngineeringChangeOrder? EcoView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateEcrAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CreateEcrAsync(
            CreateCompanyId, CreateEcrNumber ?? string.Empty, CreateTitle ?? string.Empty,
            CreateDescription, CreateChangeReason, CreateUrgency,
            CreateAffectsForm, CreateAffectsFit, CreateAffectsFunction,
            CreateAffectsSafety, CreateAffectsCustomers, CreateAffectsRegulatory,
            CreateLinkedItemId, CreateLinkedDocumentId, null, null,
            by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created ECR {r.Value!.Id} '{r.Value.EcrNumber}' Status={r.Value.Status} Reason={r.Value.ChangeReason} Urgency={r.Value.Urgency}."
            : r.Error);
        _logger.LogInformation("EcrEcoProbe CreateEcr: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSubmitEcrAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SubmitEcrAsync(LifecycleEcrId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Submitted ECR {r.Value!.Id} '{r.Value.EcrNumber}' → Status={r.Value.Status} SubmittedAt={r.Value.SubmittedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveEcrAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ApproveEcrAndCreateEcoAsync(
            ApproveEcrId, ApproveEcoNumber ?? string.Empty, ApproveEcoTitle ?? string.Empty,
            ApproveEffectivityType, null, null, null, null, null, null, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Approved ECR {r.Value!.Ecr.Id} → created ECO {r.Value.Eco.Id} '{r.Value.Eco.EcoNumber}' Urgency={r.Value.Eco.Urgency} EffectivityType={r.Value.Eco.EffectivityType} RequiresFAI={r.Value.Eco.RequiresFaiRetrigger} RequiresCustNotice={r.Value.Eco.RequiresCustomerNotice}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRejectEcrAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RejectEcrAsync(RejectEcrId, RejectReason ?? "(no reason given)", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Rejected ECR {r.Value!.Id} → Status={r.Value.Status} Reason='{r.Value.RejectionReason}'."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostAddLineAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.AddEcoLineItemAsync(
            AddLineEcoId, AddLineItemId, AddLineDocumentId,
            AddLineAffectedDocVersionId, AddLineNewDocVersionId,
            AddLineChangeDescription, AddLineBefore, AddLineAfter,
            AddLineDisposition, null, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Added EcoLineItem {r.Value!.Id} on ECO {r.Value.EcoId} Seq={r.Value.Sequence} AffectedItem={r.Value.AffectedItemId} AffectedDoc={r.Value.AffectedDocumentId} NewDocVer={r.Value.NewDocumentVersionId} Disposition={r.Value.Disposition}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostAddStageAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.AddEcoApprovalStageAsync(
            AddStageEcoId, AddStageOrder, AddStageRole ?? string.Empty,
            AddStageRequiredApprover, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Added EcoApproval {r.Value!.Id} on ECO {r.Value.EcoId} StageOrder={r.Value.StageOrder} Role='{r.Value.ApprovalRole}' Status={r.Value.Status}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveStageAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ApproveEcoStageAsync(ApproveStageId, by, ApproveStageNotes, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Approved EcoApproval {r.Value!.Id} (Stage {r.Value.StageOrder} on ECO {r.Value.EcoId}) DecidedAt={r.Value.DecidedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostReleaseEcoAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReleaseEcoAsync(LifecycleEcoId, by, null, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Released ECO {r.Value!.Id} '{r.Value.EcoNumber}' → Status={r.Value.Status} ReleasedAt={r.Value.ReleasedAtUtc:u}. Atomic DocumentVersion supersede fired for each line with NewDocVersionId."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostImplementEcoAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ImplementEcoAsync(LifecycleEcoId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Implemented ECO {r.Value!.Id} '{r.Value.EcoNumber}' → Status={r.Value.Status} ImplementedAt={r.Value.ImplementedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCloseEcoAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CloseEcoAsync(LifecycleEcoId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Closed ECO {r.Value!.Id} '{r.Value.EcoNumber}' → Status={r.Value.Status} ClosedAt={r.Value.ClosedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetEcrAsync(CancellationToken ct)
    {
        if (GetEcrId <= 0) { Set(false, "EcrId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetEcoAsync(CancellationToken ct)
    {
        if (GetEcoId <= 0) { Set(false, "EcoId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetEcrId > 0) EcrView = await _svc.GetEcrAsync(GetEcrId, ct);
        if (GetEcoId > 0) EcoView = await _svc.GetEcoAsync(GetEcoId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
