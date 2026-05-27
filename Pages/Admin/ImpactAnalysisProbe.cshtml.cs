using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services.Engineering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 14.3 PR-7 (2026-05-27) — admin probe for IChangeImpactService + IDocumentRedlineService.
// CLOSES Sprint 14.3 Engineering Change Management cascade.
//
// NINE WRITE BUTTONS per Lock 16 corollary:
//   1. Analyze ECO Impact (creates ChangeImpactAnalysis + walks the chain)
//   2. Trigger FAI Re-trigger (auto-creates FaiReports for affected items)
//   3. Resolve Impact Line (marks an individual line resolved)
//   4. Create Redline (adds markup annotation to a document version)
//   5. Submit Redline for Review (Draft → UnderReview)
//   6. Approve Redline (UnderReview → Approved)
//   7. Reject Redline (UnderReview → Rejected)
//   8. Supersede Redline
//   9. Get Analysis (read)
[Authorize(Roles = "Admin")]
public sealed class ImpactAnalysisProbeModel : PageModel
{
    private readonly IChangeImpactService _impactSvc;
    private readonly IDocumentRedlineService _redlineSvc;
    private readonly ILogger<ImpactAnalysisProbeModel> _logger;

    public ImpactAnalysisProbeModel(
        IChangeImpactService impactSvc,
        IDocumentRedlineService redlineSvc,
        ILogger<ImpactAnalysisProbeModel> logger)
    {
        _impactSvc = impactSvc;
        _redlineSvc = redlineSvc;
        _logger = logger;
    }

    // --- Analyze ECO Impact ---
    [BindProperty] public int AnalyzeEcoId { get; set; } = 1;
    [BindProperty] public int AnalyzeCompanyId { get; set; } = 1;
    [BindProperty] public string? AnalysisNumber { get; set; } = "CIA-2026-ECO-001";

    // --- Trigger FAI ---
    [BindProperty] public int TriggerFaiAnalysisId { get; set; }

    // --- Resolve Impact Line ---
    [BindProperty] public int ResolveLineId { get; set; }
    [BindProperty] public string? ResolveActionTaken { get; set; } = "BOM snapshot updated to Rev C. WIP material dispositioned per MRB decision.";

    // --- Create Redline ---
    [BindProperty] public int RedlineCompanyId { get; set; } = 1;
    [BindProperty] public string? RedlineNumber { get; set; } = "DRL-2026-BRG-001";
    [BindProperty] public int RedlineDocVersionId { get; set; } = 1;
    [BindProperty] public int? RedlineEcoId { get; set; } = 1;
    [BindProperty] public int? RedlineItemId { get; set; } = 9245;
    [BindProperty] public RedlineType RedlineType { get; set; } = RedlineType.Tolerance;
    [BindProperty] public RedlineSeverity RedlineSeverity { get; set; } = RedlineSeverity.Major;
    [BindProperty] public string? RedlineAffectedArea { get; set; } = "Section A-A, Inner Race Bore — Detail View B, Dimension 47.000 ±0.005";
    [BindProperty] public string? RedlineOriginalValue { get; set; } = "Ra 0.8 µm max surface finish";
    [BindProperty] public string? RedlineNewValue { get; set; } = "Ra 0.4 µm max surface finish (tighter per bearing life improvement program)";
    [BindProperty] public string? RedlineMarkupDesc { get; set; } = "Inner race grinding surface finish reduced from Ra 0.8 to Ra 0.4 µm to improve L10 bearing life. Requires CBN grinding wheel upgrade on Studer S33 CNC grinder.";
    [BindProperty] public string? RedlineSpecRef { get; set; } = "SKF GP 6207-2RS rev. C §4.3.2";
    [BindProperty] public string? RedlineDrawingZone { get; set; } = "B3";
    [BindProperty] public string? RedlineDrawingView { get; set; } = "Section A-A Detail B";
    [BindProperty] public bool RedlineAffectsForm { get; set; }
    [BindProperty] public bool RedlineAffectsFit { get; set; }
    [BindProperty] public bool RedlineAffectsFunction { get; set; } = true;
    [BindProperty] public bool RedlineCustApproval { get; set; } = true;
    [BindProperty] public bool RedlineFaiRequired { get; set; } = true;

    // --- Submit/Approve/Reject/Supersede Redline ---
    [BindProperty] public int SubmitRedlineId { get; set; }
    [BindProperty] public int ApproveRedlineId { get; set; }
    [BindProperty] public int RejectRedlineId { get; set; }
    [BindProperty] public string? RejectNotes { get; set; }
    [BindProperty] public int SupersedeRedlineId { get; set; }

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetAnalysisId { get; set; }
    [BindProperty(SupportsGet = true)] public int GetEcoId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public ChangeImpactAnalysis? AnalysisView { get; private set; }
    public IReadOnlyList<DocumentRedline>? RedlineListView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    // ---- 1. Analyze ECO Impact ----
    public async Task<IActionResult> OnPostAnalyzeAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _impactSvc.AnalyzeEcoImpactAsync(
            AnalyzeEcoId, AnalysisNumber ?? $"CIA-{DateTime.UtcNow:yyyyMMddHHmmss}", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Analysis {r.Value!.Id} '{r.Value.AnalysisNumber}' — {r.Value.TotalImpactLines} lines " +
              $"({r.Value.CriticalImpactLines} critical). PROs={r.Value.AffectedProductionOrderCount} " +
              $"Devs={r.Value.AffectedDeviationCount} CARs={r.Value.AffectedCarCount} Docs={r.Value.AffectedDocumentCount}. " +
              $"FAI={r.Value.RequiresFaiRetrigger} CustomerNotice={r.Value.RequiresCustomerNotice}. Status={r.Value.Status}."
            : r.Error);
        if (r.IsSuccess) GetAnalysisId = r.Value!.Id;
        return await ReloadAsync(ct);
    }

    // ---- 2. Trigger FAI Re-trigger ----
    public async Task<IActionResult> OnPostTriggerFaiAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _impactSvc.TriggerFaiRetriggerAsync(TriggerFaiAnalysisId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"FAI triggered on analysis {r.Value!.Id}: {r.Value.FaiReportsCreated} FAI reports created. " +
              $"Resolved={r.Value.ResolvedImpactLines}/{r.Value.TotalImpactLines}. Status={r.Value.Status}."
            : r.Error);
        if (r.IsSuccess) GetAnalysisId = r.Value!.Id;
        return await ReloadAsync(ct);
    }

    // ---- 3. Resolve Impact Line ----
    public async Task<IActionResult> OnPostResolveLineAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _impactSvc.ResolveImpactLineAsync(
            ResolveLineId, ResolveActionTaken ?? "(resolved)", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Line {r.Value!.Id} resolved: {r.Value.LineType} — '{r.Value.ActionTaken?[..Math.Min(80, r.Value.ActionTaken?.Length ?? 0)]}'."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // ---- 4. Create Redline ----
    public async Task<IActionResult> OnPostCreateRedlineAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _redlineSvc.CreateRedlineAsync(new CreateRedlineRequest(
            CompanyId: RedlineCompanyId,
            RedlineNumber: RedlineNumber ?? $"DRL-{DateTime.UtcNow:yyyyMMddHHmmss}",
            DocumentVersionId: RedlineDocVersionId,
            EcoId: RedlineEcoId,
            ItemId: RedlineItemId,
            Type: RedlineType,
            Severity: RedlineSeverity,
            AffectedArea: RedlineAffectedArea ?? "(area)",
            OriginalValue: RedlineOriginalValue,
            NewValue: RedlineNewValue,
            MarkupDescription: RedlineMarkupDesc,
            SpecificationReference: RedlineSpecRef,
            DrawingZone: RedlineDrawingZone,
            DrawingView: RedlineDrawingView,
            AffectsForm: RedlineAffectsForm,
            AffectsFit: RedlineAffectsFit,
            AffectsFunction: RedlineAffectsFunction,
            CustomerApprovalRequired: RedlineCustApproval,
            RequiresFaiRetrigger: RedlineFaiRequired,
            CreatedBy: by), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Redline {r.Value!.Id} '{r.Value.RedlineNumber}' — {r.Value.Type} {r.Value.Severity} on DocVer={r.Value.DocumentVersionId}. " +
              $"F/F/F={r.Value.AffectsForm}/{r.Value.AffectsFit}/{r.Value.AffectsFunction}. Status={r.Value.Status}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // ---- 5. Submit Redline ----
    public async Task<IActionResult> OnPostSubmitRedlineAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _redlineSvc.SubmitForReviewAsync(SubmitRedlineId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Redline {r.Value!.Id} '{r.Value.RedlineNumber}' → UnderReview."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // ---- 6. Approve Redline ----
    public async Task<IActionResult> OnPostApproveRedlineAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _redlineSvc.ApproveRedlineAsync(ApproveRedlineId, by, null, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Redline {r.Value!.Id} '{r.Value.RedlineNumber}' → Approved by {r.Value.ApprovedBy} at {r.Value.ApprovedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // ---- 7. Reject Redline ----
    public async Task<IActionResult> OnPostRejectRedlineAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _redlineSvc.RejectRedlineAsync(RejectRedlineId, by, RejectNotes, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Redline {r.Value!.Id} '{r.Value.RedlineNumber}' → Rejected."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // ---- 8. Supersede Redline ----
    public async Task<IActionResult> OnPostSupersedeRedlineAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _redlineSvc.SupersedeRedlineAsync(SupersedeRedlineId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Redline {r.Value!.Id} '{r.Value.RedlineNumber}' → Superseded."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // ---- 9. Get Analysis ----
    public async Task<IActionResult> OnPostGetAnalysisAsync(CancellationToken ct)
    {
        if (GetAnalysisId <= 0 && GetEcoId <= 0) { Set(false, "Provide Analysis Id or ECO Id."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetAnalysisId > 0)
            AnalysisView = await _impactSvc.GetAnalysisAsync(GetAnalysisId, ct);
        else if (GetEcoId > 0)
            AnalysisView = await _impactSvc.GetAnalysisForEcoAsync(GetEcoId, ct);

        // Load redlines for the ECO if we have an analysis
        if (AnalysisView?.EcoId > 0)
            RedlineListView = await _redlineSvc.GetRedlinesForEcoAsync(AnalysisView.EcoId, ct);

        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
