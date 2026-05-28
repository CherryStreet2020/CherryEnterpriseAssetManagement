// Sprint 15.2 PR-7 — admin probe for ISubcontractFlowService.
// 9 write/action buttons walking the §5 8-step flow + aggregate state.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count queries. All writes flow through ISubcontractFlowService.")]
public sealed class SubcontractFlowProbeModel : PageModel
{
    private readonly ISubcontractFlowService _flow;
    private readonly AppDbContext _db;
    private readonly ILogger<SubcontractFlowProbeModel> _logger;

    public SubcontractFlowProbeModel(
        ISubcontractFlowService flow,
        AppDbContext db,
        ILogger<SubcontractFlowProbeModel> logger)
    {
        _flow = flow;
        _db = db;
        _logger = logger;
    }

    // ── Step 1 (read-only scan) ──
    [BindProperty] public int Step1ProId { get; set; } = 1;

    // ── Step 2 (ensure) ──
    [BindProperty] public int Step2ProId { get; set; } = 1;
    [BindProperty] public int Step2OpSeq { get; set; } = 40;
    [BindProperty] public string Step2OpCode { get; set; } = "OP-040-HT";
    [BindProperty] public string Step2OpDesc { get; set; } = "Heat treat — outside vendor";
    [BindProperty] public int? Step2SupplierId { get; set; } = 1;
    [BindProperty] public int? Step2ServiceItemId { get; set; }
    [BindProperty] public decimal Step2Qty { get; set; } = 100m;

    // ── Step 3 (link PO line) ──
    [BindProperty] public int Step3OpId { get; set; } = 1;
    [BindProperty] public int Step3PoLineId { get; set; } = 1;

    // ── Step 4 (readiness gate) ──
    [BindProperty] public int Step4OpId { get; set; } = 1;

    // ── Step 5 (ship) ──
    [BindProperty] public int Step5OpId { get; set; } = 1;
    [BindProperty] public int Step5WipItemId { get; set; } = 1;
    [BindProperty] public decimal Step5Qty { get; set; } = 100m;
    [BindProperty] public string? Step5LotNumber { get; set; } = "LOT-2026-001";
    [BindProperty] public decimal? Step5UnitCost { get; set; } = 12.50m;

    // ── Step 6 (status read) ──
    [BindProperty] public int Step6OpId { get; set; } = 1;

    // ── Step 7 (receive) ──
    [BindProperty] public int Step7OpId { get; set; } = 1;
    [BindProperty] public int? Step7ShipmentId { get; set; }
    [BindProperty] public int Step7WipItemId { get; set; } = 1;
    [BindProperty] public decimal Step7Received { get; set; } = 100m;
    [BindProperty] public decimal Step7Accepted { get; set; } = 100m;
    [BindProperty] public decimal Step7Rejected { get; set; } = 0m;
    [BindProperty] public decimal Step7Scrapped { get; set; } = 0m;
    [BindProperty] public SubcontractReceiptScenario Step7Scenario { get; set; } = SubcontractReceiptScenario.FullGoodReceipt;

    // ── Step 8 (advance) ──
    [BindProperty] public int Step8OpId { get; set; } = 1;

    // ── Aggregate ──
    [BindProperty] public int FlowStateOpId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalProductionOrders { get; private set; }
    public int TotalSubcontractOps { get; private set; }
    public int OpsReadyToShip { get; private set; }
    public int OpsAtVendor { get; private set; }
    public int OpsComplete { get; private set; }

    public EvaluateRoutingResult? Step1Result { get; private set; }
    public EnsureOpsAndDemandsResult? Step2Result { get; private set; }
    public LinkServicePoLineResult? Step3Result { get; private set; }
    public ReadinessGateResult? Step4Result { get; private set; }
    public Step5ShipResult? Step5Result { get; private set; }
    public VendorProcessingStatus? Step6Result { get; private set; }
    public Step7ReceiveResult? Step7Result { get; private set; }
    public AdvanceToNextOpResult? Step8Result { get; private set; }
    public FlowStateSummary? FlowState { get; private set; }

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalProductionOrders = await _db.Set<ProductionOrder>().CountAsync(ct);
        TotalSubcontractOps = await _db.Set<SubcontractOperation>().CountAsync(ct);
        OpsReadyToShip = await _db.Set<SubcontractOperation>()
            .CountAsync(s => s.Status == SubcontractOperationStatus.ReadyToShip, ct);
        OpsAtVendor = await _db.Set<SubcontractOperation>()
            .CountAsync(s => s.Status == SubcontractOperationStatus.ShippedToVendor ||
                              s.Status == SubcontractOperationStatus.AtVendor ||
                              s.Status == SubcontractOperationStatus.PartiallyReceived, ct);
        OpsComplete = await _db.Set<SubcontractOperation>()
            .CountAsync(s => s.Status == SubcontractOperationStatus.Complete ||
                              s.Status == SubcontractOperationStatus.Closed, ct);
    }

    // 1) STEP 1 — Evaluate routing
    public async Task<IActionResult> OnPostStep1Async(CancellationToken ct)
    {
        var r = await _flow.EvaluateRoutingForSubcontractAsync(Step1ProId, ct);
        if (r.IsSuccess) Step1Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 1: {r.Value!.Candidates.Count} candidate(s) for PRO #{Step1ProId}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) STEP 2 — Ensure ops + dual-demands
    public async Task<IActionResult> OnPostStep2Async(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var candidates = new List<SubcontractCandidate>
        {
            new SubcontractCandidate(
                Step2ProId, Step2OpSeq, Step2OpCode, Step2OpDesc,
                Step2SupplierId, Step2ServiceItemId, Step2Qty,
                "Admin probe manual candidate")
        };
        var r = await _flow.EnsureOpsAndDemandsAsync(Step2ProId, candidates, by, ct);
        if (r.IsSuccess) Step2Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 2: ops created={r.Value!.OpsCreated}/existed={r.Value.OpsAlreadyExisted}, demands created={r.Value.DemandsCreated}/existed={r.Value.DemandsAlreadyExisted}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) STEP 3 — Link service PO line
    public async Task<IActionResult> OnPostStep3Async(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _flow.LinkServicePoLineAsync(Step3OpId, Step3PoLineId, by, ct);
        if (r.IsSuccess) Step3Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 3: linked PO line #{r.Value!.ServicePurchaseOrderLineId} to op #{r.Value.SubcontractOperationId}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) STEP 4 — Readiness gate
    public async Task<IActionResult> OnPostStep4Async(CancellationToken ct)
    {
        var r = await _flow.EvaluateReadinessGateAsync(Step4OpId, ct);
        if (r.IsSuccess) Step4Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 4: op #{Step4OpId} ready={r.Value!.IsReady}, priorOp={r.Value.PriorOpComplete}, poCreated={r.Value.ServicePoCreated}. {(r.Value.BlockingReason ?? "Ready to ship.")}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) STEP 5 — Ship (create shipment + line + mark in transit)
    public async Task<IActionResult> OnPostStep5Async(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _flow.ExecuteShipmentAsync(new Step5ShipRequest(
            SubcontractOperationId: Step5OpId,
            VendorLocationId: null,
            ShipFromLocationId: null,
            Carrier: "FedEx",
            ShippingMethod: "Ground",
            TrackingNumber: null,
            FreightCost: 45m,
            FreightCurrency: "USD",
            RequiredShipDate: DateTime.UtcNow.AddDays(1),
            ExpectedDeliveryDate: DateTime.UtcNow.AddDays(3),
            WipItemId: Step5WipItemId,
            PartNumber: "BRACKET-001",
            Description: "Outbound WIP — flow probe Step 5",
            DrawingRevision: "RevA",
            LotNumber: Step5LotNumber,
            SerialNumber: null,
            QuantityShipped: Step5Qty,
            Uom: "EA",
            UnitCostSnapshot: Step5UnitCost,
            CertRequired: false,
            PackingInstructions: "Crate per AS9100",
            CreatedBy: by,
            Notes: "Step 5 — admin probe"), ct);
        if (r.IsSuccess) Step5Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 5: shipment {r.Value!.ShipmentNumber} (id #{r.Value.SubcontractShipmentId}) line #{r.Value.SubcontractShipmentLineId} → {r.Value.ShipmentStatus}, op→{r.Value.OpStatus}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6) STEP 6 — Vendor processing status (read)
    public async Task<IActionResult> OnPostStep6Async(CancellationToken ct)
    {
        var r = await _flow.GetVendorProcessingStatusAsync(Step6OpId, ct);
        if (r.IsSuccess) Step6Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 6: op #{Step6OpId} status={r.Value!.OpStatus}, shipped={r.Value.QuantityShipped:N4}, atVendor={r.Value.QuantityAtVendor:N4}, received={r.Value.QuantityReceivedBack:N4}, openShipments={r.Value.OpenShipmentCount}, openReceipts={r.Value.OpenReceiptCount}, daysLate={r.Value.DaysLate?.ToString() ?? "—"}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 7) STEP 7 — Receive (create receipt + line + post)
    public async Task<IActionResult> OnPostStep7Async(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _flow.ExecuteReceiptAsync(new Step7ReceiveRequest(
            SubcontractOperationId: Step7OpId,
            SubcontractShipmentId: Step7ShipmentId,
            ReceivingLocationId: null,
            VendorPackingSlip: "FLOW-PROBE-001",
            Carrier: "FedEx",
            TrackingNumber: null,
            ReceiptDate: DateTime.UtcNow,
            WipItemId: Step7WipItemId,
            PartNumber: "BRACKET-001",
            Description: "Return WIP — flow probe Step 7",
            DrawingRevision: "RevA",
            LotNumber: "LOT-2026-001",
            SerialNumber: null,
            QuantityReceived: Step7Received,
            QuantityAccepted: Step7Accepted,
            QuantityRejected: Step7Rejected,
            QuantityScrappedAtVendor: Step7Scrapped,
            QuantityShort: 0m,
            Uom: "EA",
            Scenario: Step7Scenario,
            Disposition: SubcontractReceiptDisposition.ReleaseToNextOp,
            RejectReason: Step7Rejected > 0m ? "OutOfTolerance" : null,
            NcrReference: Step7Rejected > 0m ? "NCR-2026-001" : null,
            CertReceived: true,
            CertReference: "CERT-2026-001",
            InspectionRequired: false,
            CreatedBy: by,
            Notes: "Step 7 — admin probe"), ct);
        if (r.IsSuccess) Step7Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 7: receipt {r.Value!.ReceiptNumber} (id #{r.Value.SubcontractReceiptId}) line #{r.Value.SubcontractReceiptLineId} → {r.Value.ReceiptStatus}, op→{r.Value.OpStatus}, approvalRequired={r.Value.RequiresApproval}, acceptedSoFar={r.Value.QuantityAcceptedSoFar:N4}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 8) STEP 8 — Advance to next op + run readiness
    public async Task<IActionResult> OnPostStep8Async(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _flow.AdvanceToNextOpAsync(Step8OpId, by, ct);
        if (r.IsSuccess) Step8Result = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Step 8: op #{r.Value!.SubcontractOperationId} → {r.Value.OpStatus}. Next op seq={r.Value.NextOperationSequence?.ToString() ?? "—"}, ready={r.Value.NextOpReadinessReady}. {r.Value.NextOpReadinessSummary}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 9) AGGREGATE — Get flow state summary
    public async Task<IActionResult> OnPostFlowStateAsync(CancellationToken ct)
    {
        var r = await _flow.GetFlowStateAsync(FlowStateOpId, ct);
        if (r.IsSuccess) FlowState = r.Value;
        Set(r.IsSuccess, r.IsSuccess
            ? $"Flow state: op #{r.Value!.SubcontractOperationId} is at Step {r.Value.CurrentStep} — {r.Value.CurrentStepName}. Hint: {r.Value.NextActionHint}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }
}
