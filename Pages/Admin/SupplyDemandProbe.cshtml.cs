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

// Sprint 15.1 PR-2 (2026-05-28) — admin probe for IProductionSupplyDemandService.
//
// EIGHT WRITE/ACTION BUTTONS per Lock 16 corollary:
//   1. Generate Demands from PRO   (GenerateDemandsFromProAsync)
//   2. Refresh Supply Status        (RefreshSupplyStatusAsync — one demand)
//   3. Refresh All Demands for PRO  (RefreshSupplyStatusForProAsync)
//   4. Allocate Supply              (AllocateSupplyAsync)
//   5. Release Allocation           (ReleaseAllocationAsync)
//   6. Load Demands for PRO         (read — GetDemandsForProAsync)
//   7. Load Unresolved Demands      (read — GetUnresolvedDemandsAsync)
//   8. Load Allocations for Demand  (read — GetAllocationsForDemandAsync)
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count/list queries. All writes flow through IProductionSupplyDemandService.")]
public sealed class SupplyDemandProbeModel : PageModel
{
    private readonly IProductionSupplyDemandService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<SupplyDemandProbeModel> _logger;

    public SupplyDemandProbeModel(
        IProductionSupplyDemandService svc,
        AppDbContext db,
        ILogger<SupplyDemandProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Bind properties ────────────────────────────────────────
    [BindProperty] public int GenerateProId { get; set; } = 1;

    [BindProperty] public int RefreshDemandId { get; set; } = 1;

    [BindProperty] public int RefreshAllProId { get; set; } = 1;

    [BindProperty] public int AllocateDemandId { get; set; } = 1;
    [BindProperty] public AllocationSupplyType AllocateSupplyType { get; set; } = AllocationSupplyType.PurchaseOrderLine;
    [BindProperty] public int AllocateRecordId { get; set; } = 1;
    [BindProperty] public int? AllocateLineId { get; set; }
    [BindProperty] public decimal AllocateQuantity { get; set; } = 10m;

    [BindProperty] public int ReleaseAllocationId { get; set; } = 1;

    [BindProperty] public int LoadProId { get; set; } = 1;

    [BindProperty] public int LoadAllocationsDemandId { get; set; } = 1;

    // ── Output ──────────────────────────────────────────────────
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    // Summary stats
    public int TotalDemands { get; private set; }
    public int DemandsNotSupplied { get; private set; }
    public int DemandsCommitted { get; private set; }
    public int DemandsFullyFulfilled { get; private set; }
    public int DemandsShort { get; private set; }
    public int TotalAllocations { get; private set; }
    public int AllocationsActive { get; private set; }

    public IReadOnlyList<ProductionSupplyDemand> LoadedDemands { get; private set; } = Array.Empty<ProductionSupplyDemand>();
    public IReadOnlyList<ProductionSupplyAllocation> LoadedAllocations { get; private set; } = Array.Empty<ProductionSupplyAllocation>();

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalDemands = await _db.Set<ProductionSupplyDemand>().CountAsync(ct);
        DemandsNotSupplied = await _db.Set<ProductionSupplyDemand>()
            .CountAsync(d => d.SupplyStatus == DemandSupplyStatus.NotSupplied, ct);
        DemandsCommitted = await _db.Set<ProductionSupplyDemand>()
            .CountAsync(d => d.SupplyStatus == DemandSupplyStatus.Committed, ct);
        DemandsFullyFulfilled = await _db.Set<ProductionSupplyDemand>()
            .CountAsync(d => d.SupplyStatus == DemandSupplyStatus.FullyFulfilled, ct);
        DemandsShort = await _db.Set<ProductionSupplyDemand>()
            .CountAsync(d => d.ShortageStatus == DemandShortageStatus.Short ||
                              d.ShortageStatus == DemandShortageStatus.Late, ct);
        TotalAllocations = await _db.Set<ProductionSupplyAllocation>().CountAsync(ct);
        AllocationsActive = await _db.Set<ProductionSupplyAllocation>()
            .CountAsync(a => a.Status == AllocationStatus.Active ||
                              a.Status == AllocationStatus.PartiallyConsumed, ct);
    }

    // 1. Generate Demands from PRO
    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.GenerateDemandsFromProAsync(GenerateProId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"PRO {r.Value!.ProductionOrderId}: created {r.Value.DemandsCreated}, skipped {r.Value.DemandsSkipped}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2. Refresh Single Demand
    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken ct)
    {
        var r = await _svc.RefreshSupplyStatusAsync(RefreshDemandId, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Demand #{r.Value!.DemandId} refreshed. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3. Refresh All for PRO
    public async Task<IActionResult> OnPostRefreshAllAsync(CancellationToken ct)
    {
        var r = await _svc.RefreshSupplyStatusForProAsync(RefreshAllProId, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Refreshed {r.Value} demands for PRO {RefreshAllProId}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4. Allocate Supply
    public async Task<IActionResult> OnPostAllocateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.AllocateSupplyAsync(
            new AllocateSupplyRequest(
                AllocateDemandId,
                AllocateSupplyType,
                AllocateRecordId,
                AllocateLineId,
                AllocateQuantity,
                null,
                "Admin probe allocation",
                by),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Allocation #{r.Value!.AllocationId}: {r.Value.AllocatedQuantity:N4} allocated to demand #{r.Value.DemandId}. Remaining demand: {r.Value.RemainingDemand:N4}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5. Release Allocation
    public async Task<IActionResult> OnPostReleaseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReleaseAllocationAsync(ReleaseAllocationId, "Admin probe release", by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Released {r.Value!.QuantityReleased:N4} from allocation #{r.Value.AllocationId} on demand #{r.Value.DemandId}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6. Load Demands for PRO
    public async Task<IActionResult> OnPostLoadDemandsAsync(CancellationToken ct)
    {
        LoadedDemands = await _svc.GetDemandsForProAsync(LoadProId, ct);
        Set(true, $"Loaded {LoadedDemands.Count} demands for PRO {LoadProId}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 7. Load Unresolved
    public async Task<IActionResult> OnPostLoadUnresolvedAsync(CancellationToken ct)
    {
        LoadedDemands = await _svc.GetUnresolvedDemandsAsync(LoadProId, ct);
        Set(true, $"Loaded {LoadedDemands.Count} UNRESOLVED demands for PRO {LoadProId}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 8. Load Allocations for Demand
    public async Task<IActionResult> OnPostLoadAllocationsAsync(CancellationToken ct)
    {
        LoadedAllocations = await _svc.GetAllocationsForDemandAsync(LoadAllocationsDemandId, ct);
        Set(true, $"Loaded {LoadedAllocations.Count} allocations for demand #{LoadAllocationsDemandId}.");
        await LoadStatsAsync(ct);
        return Page();
    }
}
