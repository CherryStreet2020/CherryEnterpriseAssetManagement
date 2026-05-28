using System;
using System.Collections.Generic;
using System.Linq;
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

// Sprint 15.1 PR-4 — admin probe for ISubcontractOperationService.
// 8 write/action buttons per Lock 16 corollary.
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count/list queries. All writes flow through ISubcontractOperationService.")]
public sealed class SubcontractOperationProbeModel : PageModel
{
    private readonly ISubcontractOperationService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<SubcontractOperationProbeModel> _logger;

    public SubcontractOperationProbeModel(
        ISubcontractOperationService svc,
        AppDbContext db,
        ILogger<SubcontractOperationProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Inputs ──
    [BindProperty] public int CreateProId { get; set; } = 1;
    [BindProperty] public int CreateOpSeq { get; set; } = 40;
    [BindProperty] public string CreateOpCode { get; set; } = "OP-040-HT";
    [BindProperty] public string CreateOpDesc { get; set; } = "Heat treat — outside vendor";
    [BindProperty] public decimal CreateQty { get; set; } = 100m;
    [BindProperty] public int? CreateSupplierId { get; set; }
    [BindProperty] public int? CreateServiceItemId { get; set; }

    [BindProperty] public int DualDemandOpId { get; set; } = 1;

    [BindProperty] public int TransitionOpId { get; set; } = 1;
    [BindProperty] public SubcontractOperationStatus TransitionTo { get; set; } = SubcontractOperationStatus.ReadyToShip;

    [BindProperty] public int ShipOpId { get; set; } = 1;
    [BindProperty] public decimal ShipQty { get; set; } = 100m;

    [BindProperty] public int RecvOpId { get; set; } = 1;
    [BindProperty] public decimal RecvReceived { get; set; } = 100m;
    [BindProperty] public decimal RecvAccepted { get; set; } = 100m;
    [BindProperty] public decimal RecvRejected { get; set; } = 0m;

    [BindProperty] public int CompleteOpId { get; set; } = 1;

    [BindProperty] public int LoadStatusOpId { get; set; } = 1;
    [BindProperty] public int LoadProId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalSubcontractOps { get; private set; }
    public int OpsReadyToBuy { get; private set; }
    public int OpsAtVendor { get; private set; }
    public int OpsComplete { get; private set; }
    public int TotalSubcontractDemands { get; private set; }
    public int DemandsBothSatisfied { get; private set; }

    public IReadOnlyList<SubcontractOperation> LoadedOps { get; private set; } = Array.Empty<SubcontractOperation>();
    public SubcontractStatusSummary? LoadedStatus { get; private set; }

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalSubcontractOps = await _db.Set<SubcontractOperation>().CountAsync(ct);
        OpsReadyToBuy = await _db.Set<SubcontractOperation>()
            .CountAsync(s => s.Status == SubcontractOperationStatus.ReadyToBuy, ct);
        OpsAtVendor = await _db.Set<SubcontractOperation>()
            .CountAsync(s => s.Status == SubcontractOperationStatus.AtVendor ||
                              s.Status == SubcontractOperationStatus.ShippedToVendor, ct);
        OpsComplete = await _db.Set<SubcontractOperation>()
            .CountAsync(s => s.Status == SubcontractOperationStatus.Complete ||
                              s.Status == SubcontractOperationStatus.Closed, ct);
        TotalSubcontractDemands = await _db.Set<SubcontractDemand>().CountAsync(ct);
        DemandsBothSatisfied = await _db.Set<SubcontractDemand>()
            .CountAsync(d => d.Status == SubcontractDemandStatus.BothSatisfied, ct);
    }

    public async Task<IActionResult> OnPostCreateOpAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CreateSubcontractOperationAsync(
            new CreateSubcontractOperationRequest(
                CreateProId, CreateOpSeq, CreateOpCode, CreateOpDesc,
                CreateSupplierId, CreateServiceItemId, CreateQty,
                ServiceUnitCost: 0m,
                PriorOperationSequence: CreateOpSeq - 10,
                ReturnOperationSequence: CreateOpSeq + 10,
                RequiredShipDate: DateTime.UtcNow.AddDays(2),
                RequiredBackDate: DateTime.UtcNow.AddDays(10),
                ShipWipRequired: true,
                GenerateSubcontractPo: true,
                WipItemId: null,
                FixedLeadTimeDays: 5m,
                VariableLeadTimeDaysPerUnit: 0m,
                CreatedBy: by,
                Notes: "Admin probe — heat-treat subcontract"),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created subcontract op #{r.Value!.SubcontractOperationId} on PRO {r.Value.ProductionOrderId} seq {r.Value.OperationSequence}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateDualDemandAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CreateSubcontractDemandAsync(DualDemandOpId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Binding #{r.Value!.SubcontractDemandId}: Service demand={r.Value.ServicePurchaseDemandId}, WIP demand={r.Value.WipMovementDemandId}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostTransitionAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.TransitionStatusAsync(TransitionOpId, TransitionTo, "Admin probe", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Op #{TransitionOpId} → {r.Value}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostShipAsync(CancellationToken ct)
    {
        var r = await _svc.RecordShipmentAsync(ShipOpId, ShipQty, "Admin probe ship", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Op #{ShipOpId} shipped {ShipQty:N4}. Status={r.Value!.Status}, ShipmentStatus={r.Value.ShipmentStatus}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReceiveAsync(CancellationToken ct)
    {
        var r = await _svc.RecordReceiptAsync(RecvOpId, RecvReceived, RecvAccepted, RecvRejected, "Admin probe recv", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Op #{RecvOpId} received {RecvReceived:N4} (accepted {RecvAccepted:N4}, rejected {RecvRejected:N4}). Status={r.Value!.Status}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCompleteAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MarkCompleteAsync(CompleteOpId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Op #{CompleteOpId} marked Complete. Status={r.Value!.Status}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadStatusAsync(CancellationToken ct)
    {
        var r = await _svc.GetStatusAsync(LoadStatusOpId, ct);
        if (r.IsSuccess) LoadedStatus = r.Value;
        Set(r.IsSuccess, r.IsSuccess ? $"Loaded op #{r.Value!.SubcontractOperationId}" : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadByProAsync(CancellationToken ct)
    {
        LoadedOps = await _svc.GetSubcontractOperationsForProAsync(LoadProId, ct);
        Set(true, $"Loaded {LoadedOps.Count} subcontract ops for PRO {LoadProId}");
        await LoadStatsAsync(ct);
        return Page();
    }
}
