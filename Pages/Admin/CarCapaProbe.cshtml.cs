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

// Sprint 14.3 PR-6 (2026-05-27) — admin probe for ICorrectiveActionService.
//
// NINE WRITE BUTTONS per Lock 16 corollary:
//   1. Create CAR
//   2. Issue CAR (Draft → Issued)
//   3. Begin Investigation (Issued → UnderInvestigation)
//   4. Record Root Cause (UnderInvestigation → RootCauseIdentified)
//   5. Plan Corrective Action (RootCauseIdentified → CorrectiveActionPlanned)
//   6. Begin Implementation (CorrectiveActionPlanned → ImplementationInProgress)
//   7. Complete Implementation (ImplementationInProgress → VerificationPending)
//   8. Verify and Close (VerificationPending → Closed)
//   9. Get CAR (read)
[Authorize(Roles = "Admin")]
public sealed class CarCapaProbeModel : PageModel
{
    private readonly ICorrectiveActionService _svc;
    private readonly ILogger<CarCapaProbeModel> _logger;

    public CarCapaProbeModel(ICorrectiveActionService svc, ILogger<CarCapaProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Create ---
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreateCarNumber { get; set; } = "CAR-2026-BRG-001";
    [BindProperty] public string? CreateTitle { get; set; } = "Inner race grinding process out-of-control — 3 consecutive lots above Cpk 1.33 threshold";
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public CarSource CreateSource { get; set; } = CarSource.InspectionRejection;
    [BindProperty] public CarSeverity CreateSeverity { get; set; } = CarSeverity.Major;
    [BindProperty] public int? CreateItemId { get; set; } = 9245;
    [BindProperty] public string? CreateNcrRef { get; set; } = "NCR-2026-0043";
    [BindProperty] public string? CreateNonConformance { get; set; } = "Inner race bore diameter Ra 2.4 µm vs spec Ra 0.8 µm max on LOT-2026-SKF-0187 (3 of 50 bearings)";

    // --- Issue ---
    [BindProperty] public int IssueCarId { get; set; }
    [BindProperty] public string? IssueAssignedTo { get; set; } = "Quality Engineering";
    [BindProperty] public string? IssueDepartment { get; set; } = "Manufacturing Engineering";

    // --- Investigation ---
    [BindProperty] public int InvestigateCarId { get; set; }
    [BindProperty] public string? ContainmentAction { get; set; } = "Quarantine LOT-2026-SKF-0187. 100% sort remaining stock. Hold all in-process production pending investigation.";

    // --- Root Cause ---
    [BindProperty] public int RootCauseCarId { get; set; }
    [BindProperty] public string? RootCauseAnalysis { get; set; } = "Grinding wheel dresser diamond worn beyond replacement threshold (actual 0.3mm vs 0.1mm max). Dresser replacement log shows 40% overrun on scheduled interval.";
    [BindProperty] public string? Methodology { get; set; } = "5-Why + Fishbone";

    // --- Plan ---
    [BindProperty] public int PlanCarId { get; set; }
    [BindProperty] public string? CorrectiveActionPlan { get; set; } = "Replace grinding wheel dresser diamond. Recalibrate wheel dresser to Ra 0.4 µm target. Re-qualify with 30-piece first article run.";
    [BindProperty] public string? PreventiveActionPlan { get; set; } = "Implement automated dresser wear monitoring via vibration sensor. Reduce dresser replacement interval from 500 to 350 cycles. Add Cpk gate at SPC station 3.";

    // --- Implement ---
    [BindProperty] public int ImplementCarId { get; set; }

    // --- Complete Implementation ---
    [BindProperty] public int CompleteCarId { get; set; }
    [BindProperty] public string? ImplNotes { get; set; } = "Dresser replaced. 30-piece FAI run completed — all 30 within spec (Ra 0.38-0.52 µm). Vibration sensor installed on grinder #7.";

    // --- Verify + Close ---
    [BindProperty] public int VerifyCarId { get; set; }
    [BindProperty] public string? VerificationMethod { get; set; } = "30-day Cpk monitoring on grinder #7 — 5 consecutive lots";
    [BindProperty] public string? VerificationResults { get; set; } = "Cpk = 2.14 across 5 lots (250 bearings). Zero non-conformances. Vibration sensor alarmed correctly on test dresser at 0.08mm wear.";
    [BindProperty] public bool VerificationEffective { get; set; } = true;

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetCarId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public CorrectiveActionRequest? CarView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CreateAsync(new CreateCarRequest(
            CompanyId: CreateCompanyId, CarNumber: CreateCarNumber ?? $"CAR-2026-TEST-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Title: CreateTitle ?? "Corrective Action Request", Source: CreateSource, Severity: CreateSeverity,
            ItemId: CreateItemId, NcrReference: CreateNcrRef,
            NonConformanceDescription: CreateNonConformance, Description: CreateDescription, CreatedBy: by), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created CAR {r.Value!.Id} '{r.Value.CarNumber}' Status={r.Value.Status} Source={r.Value.Source} Severity={r.Value.Severity} Item={r.Value.ItemId}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostIssueAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.IssueAsync(IssueCarId, by, IssueAssignedTo, IssueDepartment, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CAR {r.Value!.Id} '{r.Value.CarNumber}' → Issued. Assigned={r.Value.AssignedTo} Dept={r.Value.ResponsibleDepartment}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostInvestigateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.BeginInvestigationAsync(InvestigateCarId, by, ContainmentAction, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CAR {r.Value!.Id} '{r.Value.CarNumber}' → UnderInvestigation. Containment={r.Value.ContainmentAction?[..Math.Min(80, r.Value.ContainmentAction.Length)]}..."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRootCauseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RecordRootCauseAsync(RootCauseCarId, RootCauseAnalysis ?? "(root cause)", Methodology ?? "5-Why", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CAR {r.Value!.Id} '{r.Value.CarNumber}' → RootCauseIdentified via {r.Value.RootCauseMethodology}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostPlanAsync(CancellationToken ct)
    {
        var r = await _svc.PlanCorrectiveActionAsync(PlanCarId, CorrectiveActionPlan ?? "(plan)", PreventiveActionPlan, null, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CAR {r.Value!.Id} '{r.Value.CarNumber}' → CorrectiveActionPlanned."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostBeginImplAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.BeginImplementationAsync(ImplementCarId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CAR {r.Value!.Id} '{r.Value.CarNumber}' → ImplementationInProgress."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCompleteImplAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CompleteImplementationAsync(CompleteCarId, ImplNotes ?? "(completed)", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CAR {r.Value!.Id} '{r.Value.CarNumber}' → VerificationPending. Completed at {r.Value.ImplementationCompletedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostVerifyCloseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.VerifyAndCloseAsync(VerifyCarId, VerificationMethod ?? "(method)", VerificationResults ?? "(results)", VerificationEffective, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CAR {r.Value!.Id} '{r.Value.CarNumber}' → CLOSED. Effective={r.Value.VerificationEffective} Days={r.Value.DaysToClose}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetCarAsync(CancellationToken ct)
    {
        if (GetCarId <= 0) { Set(false, "CAR Id must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetCarId > 0) CarView = await _svc.GetAsync(GetCarId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
