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

// B8 PR-PRO-6 (2026-05-27) — admin probe for IProductionCompletionService.
// Complete + Scrap + Rework atomic posting with auto-advance integration.
//
// NINE WRITE BUTTONS per Lock 16 corollary:
//   1. Record Completion (CREATE INSERT — atomic good+scrap+rework+reject)
//   2. Record Scrap (CREATE INSERT — 5-dimensional root cause)
//   3. Approve Scrap (UPDATE — supervisor sign-off)
//   4. Record Rework (CREATE INSERT — routing + disposition)
//   5-7. Get Completions / Scrap / Rework for Order (READ)
//   8-9. Get single Completion / Scrap by Id (READ)
[Authorize(Roles = "Admin")]
public sealed class CompletionScrapReworkProbeModel : PageModel
{
    private readonly IProductionCompletionService _svc;
    private readonly ILogger<CompletionScrapReworkProbeModel> _logger;

    public CompletionScrapReworkProbeModel(IProductionCompletionService svc, ILogger<CompletionScrapReworkProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Completion ---
    [BindProperty] public int CmpCompanyId { get; set; } = 1;
    [BindProperty] public int CmpProId { get; set; } = 1;
    [BindProperty] public int CmpOpId { get; set; } = 1;
    [BindProperty] public decimal CmpGoodQty { get; set; } = 45;
    [BindProperty] public decimal CmpScrapQty { get; set; } = 3;
    [BindProperty] public decimal CmpReworkQty { get; set; } = 2;
    [BindProperty] public decimal CmpRejectQty { get; set; } = 0;
    [BindProperty] public bool CmpCompleteRemaining { get; set; }
    [BindProperty] public bool CmpIsFinal { get; set; }
    [BindProperty] public decimal CmpMoveQty { get; set; } = 45;
    [BindProperty] public string? CmpEmployee { get; set; } = "J. Martinez — CNC Operator, Studer S33 Grinding Cell";
    [BindProperty] public bool CmpBackflush { get; set; } = true;
    [BindProperty] public bool CmpInspection { get; set; }

    // --- Scrap ---
    [BindProperty] public int ScpCompanyId { get; set; } = 1;
    [BindProperty] public int ScpProId { get; set; } = 1;
    [BindProperty] public int ScpDetectedOpId { get; set; } = 2;
    [BindProperty] public int? ScpCausedOpId { get; set; } = 1;
    [BindProperty] public decimal ScpQty { get; set; } = 3;
    [BindProperty] public ScrapResponsibleArea ScpArea { get; set; } = ScrapResponsibleArea.Machine;
    [BindProperty] public ScrapDisposition ScpDisposition { get; set; } = ScrapDisposition.Scrap;
    [BindProperty] public CostTreatment ScpCostTreatment { get; set; } = CostTreatment.AbsorbToJob;
    [BindProperty] public bool ScpNcr { get; set; } = true;
    [BindProperty] public bool ScpSupervisor { get; set; } = true;
    [BindProperty] public string? ScpNotes { get; set; } = "Inner bore Ra 2.4 µm exceeds spec Ra 0.8 µm max — grinding wheel dresser worn beyond threshold. LOT-2026-SKF-0187.";

    // --- Approve Scrap ---
    [BindProperty] public int ApproveScpId { get; set; }

    // --- Rework ---
    [BindProperty] public int RwkCompanyId { get; set; } = 1;
    [BindProperty] public int RwkProId { get; set; } = 1;
    [BindProperty] public int RwkSourceOpId { get; set; } = 2;
    [BindProperty] public int? RwkDestOpId { get; set; } = 1;
    [BindProperty] public decimal RwkQty { get; set; } = 2;
    [BindProperty] public ReworkRoutingType RwkRoutingType { get; set; } = ReworkRoutingType.ReturnToExistingOp;
    [BindProperty] public string? RwkInstructions { get; set; } = "Re-grind inner bore to Ra 0.4 µm using fresh CBN wheel. 100% inspect post-rework per AS9102 partial delta.";
    [BindProperty] public bool RwkQualityHold { get; set; } = true;
    [BindProperty] public bool RwkReinspect { get; set; } = true;
    [BindProperty] public CostTreatment RwkCostTreatment { get; set; } = CostTreatment.AbsorbToJob;

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int ReadProId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public IReadOnlyList<ProductionCompletionEvent>? CompletionList { get; private set; }
    public IReadOnlyList<ProductionScrapEvent>? ScrapList { get; private set; }
    public IReadOnlyList<ProductionReworkEvent>? ReworkList { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    // 1. Record Completion
    public async Task<IActionResult> OnPostRecordCompletionAsync(CancellationToken ct)
    {
        var r = await _svc.RecordCompletionAsync(new RecordCompletionRequest(
            CompanyId: CmpCompanyId, ProductionOrderId: CmpProId, OperationId: CmpOpId,
            GoodQuantity: CmpGoodQty, ScrapQuantity: CmpScrapQty, ReworkQuantity: CmpReworkQty,
            RejectQuantity: CmpRejectQty, CompleteRemaining: CmpCompleteRemaining,
            IsFinalOperation: CmpIsFinal, MoveQuantityToNextOp: CmpMoveQty,
            EmployeeName: CmpEmployee, EmployeeId: null, ResourceWorkCenterId: null,
            BackflushMaterials: CmpBackflush, AutoIssuePullMaterials: false,
            InspectionRequired: CmpInspection, LotNumbers: null, SerialNumbers: null,
            Notes: null, CompletedBy: User.Identity?.Name ?? "admin-probe"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Completion {r.Value!.Id} '{r.Value.CompletionNumber}' — Good={r.Value.GoodQuantity} Scrap={r.Value.ScrapQuantity} " +
              $"Rework={r.Value.ReworkQuantity} Reject={r.Value.RejectQuantity}. MoveNext={r.Value.MoveQuantityToNextOp}. " +
              $"Final={r.Value.IsFinalOperation} Backflush={r.Value.BackflushMaterials}."
            : r.Error);
        if (r.IsSuccess) ReadProId = r.Value!.ProductionOrderId;
        return await ReloadAsync(ct);
    }

    // 2. Record Scrap
    public async Task<IActionResult> OnPostRecordScrapAsync(CancellationToken ct)
    {
        var r = await _svc.RecordScrapAsync(new RecordScrapRequest(
            CompanyId: ScpCompanyId, ProductionOrderId: ScpProId,
            DetectedAtOperationId: ScpDetectedOpId, CausedAtOperationId: ScpCausedOpId,
            ScrapQuantity: ScpQty, ScrapUom: "EA",
            ScrapReasonCodeId: null, DefectCodeId: null, CauseCodeId: null,
            ResponsibleArea: ScpArea, Disposition: ScpDisposition,
            IsComponentScrap: false, IsOperationScrap: true,
            ReplacementRequired: false, CostTreatment: ScpCostTreatment,
            NcrRequired: ScpNcr, SupervisorApprovalRequired: ScpSupervisor,
            LotNumbers: "LOT-2026-SKF-0187", SerialNumbers: null,
            Notes: ScpNotes, RecordedBy: User.Identity?.Name ?? "admin-probe"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Scrap {r.Value!.Id} '{r.Value.ScrapNumber}' — {r.Value.ScrapQuantity} units. " +
              $"Area={r.Value.ResponsibleArea} Disposition={r.Value.Disposition}. NCR={r.Value.NcrRequired} SuperApproval={r.Value.SupervisorApprovalRequired}."
            : r.Error);
        if (r.IsSuccess) ReadProId = r.Value!.ProductionOrderId;
        return await ReloadAsync(ct);
    }

    // 3. Approve Scrap
    public async Task<IActionResult> OnPostApproveScrapAsync(CancellationToken ct)
    {
        var r = await _svc.ApproveScrapAsync(ApproveScpId, User.Identity?.Name ?? "admin-probe", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Scrap {r.Value!.Id} approved by {r.Value.ApprovedBy} at {r.Value.ApprovedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    // 4. Record Rework
    public async Task<IActionResult> OnPostRecordReworkAsync(CancellationToken ct)
    {
        var r = await _svc.RecordReworkAsync(new RecordReworkRequest(
            CompanyId: RwkCompanyId, ProductionOrderId: RwkProId,
            SourceOperationId: RwkSourceOpId, ReworkOperationId: RwkDestOpId,
            ReworkQuantity: RwkQty, RoutingType: RwkRoutingType,
            ReworkInstructions: RwkInstructions, ReworkReasonCodeId: null,
            ReworkMaterialRequired: false, RemoveDefectiveComponent: false,
            AdditionalLaborPlannedMins: 30, AssignedWorkCenterId: null,
            DueDate: null, QualityHold: RwkQualityHold, ReinspectRequired: RwkReinspect,
            ScrapAfterFailedReworkAllowed: false, ReturnToOriginalFlow: true,
            CostTreatment: RwkCostTreatment, NcrId: null, CarId: null,
            Notes: null, DecidedBy: User.Identity?.Name ?? "admin-probe"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Rework {r.Value!.Id} '{r.Value.ReworkNumber}' — {r.Value.ReworkQuantity} units. " +
              $"Routing={r.Value.RoutingType} Dest={r.Value.ReworkOperationId}. Hold={r.Value.QualityHold} Reinspect={r.Value.ReinspectRequired}."
            : r.Error);
        if (r.IsSuccess) ReadProId = r.Value!.ProductionOrderId;
        return await ReloadAsync(ct);
    }

    // 5. Get events for order
    public async Task<IActionResult> OnPostGetEventsAsync(CancellationToken ct) => await ReloadAsync(ct);

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (ReadProId > 0)
        {
            CompletionList = await _svc.GetCompletionsForOrderAsync(ReadProId, ct);
            ScrapList = await _svc.GetScrapForOrderAsync(ReadProId, ct);
            ReworkList = await _svc.GetReworkForOrderAsync(ReadProId, ct);
        }
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
