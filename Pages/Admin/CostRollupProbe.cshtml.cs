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

// Sprint 14.4 PR-3 (2026-05-28) — admin probe for ICostRollupService.
//
// NINE WRITE/ACTION BUTTONS per Lock 16 corollary:
//   1. Execute Financial Rollup for PRO    (ExecuteRollupAsync Financial)
//   2. Execute Exploded Rollup for PRO     (ExecuteRollupAsync Exploded)
//   3. Seed Test Parent PRO + Child PRO    (setup for rollup testing)
//   4. Post Material to Parent             (PostCostAsync — material issue on parent)
//   5. Post Labor to Child                 (PostCostAsync — labor on child)
//   6. Post Child→Parent Transfer          (PostTransferAsync — child completion)
//   7. View Latest Rollup Run              (GetLatestRunAsync)
//   8. View Rollup Lines                   (GetLinesAsync)
//   9. View Rollup Exceptions              (GetExceptionsAsync)
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count/list queries. All writes flow through ICostRollupService + ICostTransactionService.")]
public sealed class CostRollupProbeModel : PageModel
{
    private readonly ICostRollupService _rollupSvc;
    private readonly ICostTransactionService _costSvc;
    private readonly AppDbContext _db;
    private readonly ILogger<CostRollupProbeModel> _logger;

    public CostRollupProbeModel(
        ICostRollupService rollupSvc,
        ICostTransactionService costSvc,
        AppDbContext db,
        ILogger<CostRollupProbeModel> logger)
    {
        _rollupSvc = rollupSvc;
        _costSvc = costSvc;
        _db = db;
        _logger = logger;
    }

    // ── Bind properties ─────────────────────────────────────────
    [BindProperty] public int RollupProId { get; set; } = 1;
    [BindProperty] public int ViewRunId { get; set; } = 0;

    // ── Output ──────────────────────────────────────────────────
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    // Stats
    public int TotalRollupRuns { get; private set; }
    public int TotalRollupLines { get; private set; }
    public int TotalRollupExceptions { get; private set; }

    // Latest run details
    public CostRollupRun? LatestRun { get; private set; }
    public IReadOnlyList<CostRollupLine> LoadedLines { get; private set; } = Array.Empty<CostRollupLine>();
    public IReadOnlyList<CostRollupException> LoadedExceptions { get; private set; } = Array.Empty<CostRollupException>();
    public IReadOnlyList<CostRollupRun> RecentRuns { get; private set; } = Array.Empty<CostRollupRun>();

    // ═══════════════════════════════════════════════════════════════
    // OnGet
    // ═══════════════════════════════════════════════════════════════

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    // ═══════════════════════════════════════════════════════════════
    // 1. Execute Financial Rollup
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostExecuteFinancialRollupAsync(CancellationToken ct)
    {
        if (RollupProId <= 0) { Set(false, "PRO Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _rollupSvc.ExecuteRollupAsync(RollupProId, CostRollupMode.Financial, by, ct);

        if (r.IsSuccess)
        {
            var run = r.Value!.Run;
            LatestRun = run;
            LoadedLines = r.Value.Lines.ToList();
            LoadedExceptions = r.Value.Exceptions.ToList();
            Set(true,
                $"Financial Rollup #{run.Id} '{run.RunNumber}' completed for PRO #{RollupProId}: " +
                $"Status={run.Status} Additive=${run.TotalAdditiveCost:N2} Transfer=${run.TotalTransferCost:N2} " +
                $"Drilldown=${run.TotalDrilldownCost:N2} Exploded=${run.TotalExplodedCost:N2} " +
                $"({run.GraphNodeCount} nodes, depth {run.GraphMaxDepth}, {run.LineCount} lines, {run.ExceptionCount} exceptions, {run.DurationMs}ms). " +
                $"Material=${run.MaterialTotal:N2} Labor=${run.LaborTotal:N2} OH=${run.OverheadTotal:N2} Sub=${run.SubcontractTotal:N2} Other=${run.OtherTotal:N2}.");
        }
        else
        {
            Set(false, r.Error);
        }

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Execute Exploded Rollup
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostExecuteExplodedRollupAsync(CancellationToken ct)
    {
        if (RollupProId <= 0) { Set(false, "PRO Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _rollupSvc.ExecuteRollupAsync(RollupProId, CostRollupMode.Exploded, by, ct);

        if (r.IsSuccess)
        {
            var run = r.Value!.Run;
            LatestRun = run;
            LoadedLines = r.Value.Lines.ToList();
            LoadedExceptions = r.Value.Exceptions.ToList();
            Set(true,
                $"Exploded Rollup #{run.Id} '{run.RunNumber}' completed for PRO #{RollupProId}: " +
                $"Status={run.Status} TotalExploded=${run.TotalExplodedCost:N2} " +
                $"({run.GraphNodeCount} nodes, depth {run.GraphMaxDepth}, {run.LineCount} lines, {run.ExceptionCount} exceptions, {run.DurationMs}ms). " +
                $"Material=${run.MaterialTotal:N2} Labor=${run.LaborTotal:N2} OH=${run.OverheadTotal:N2} Sub=${run.SubcontractTotal:N2} Other=${run.OtherTotal:N2}.");
        }
        else
        {
            Set(false, r.Error);
        }

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Post Material to Parent PRO (BRG-6207-2RS bearing issue)
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostMaterialToParentAsync(CancellationToken ct)
    {
        if (RollupProId <= 0) { Set(false, "PRO Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costSvc.PostCostAsync(
            costObjectType: CostObjectType.ProductionOrder,
            costObjectId: RollupProId,
            transactionType: CostTransactionType.MaterialIssue,
            costBucket: ProductionCostBucket.DirectMaterial,
            companyId: 1, siteId: 1, productionOrderId: RollupProId,
            operationId: 10, bomLineId: 1, itemId: 1,
            quantity: 2m, uom: "EA", unitCost: 42.75m,    // Parker Hannifin O-ring kit, aerospace grade
            sourceTransactionType: "MaterialTransaction", sourceTransactionId: null,
            lotNumber: "LOT-2026-PHN-0718", serialNumber: null, heatNumber: null,
            rollupAdditive: true,
            notes: "Material issue — 2x Parker Hannifin AS568A O-ring kit, Viton fluorocarbon, 75 Shore A",
            postedBy: by, ct: ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"CostTransaction #{r.Value!.Id} posted: material ${r.Value.ExtendedCost:N2} on PRO #{RollupProId}."
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Post Labor to Child PRO
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostLaborToChildAsync(CancellationToken ct)
    {
        // Find a child of the specified PRO
        var child = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .Where(p => p.ParentProductionOrderId == RollupProId)
            .FirstOrDefaultAsync(ct);

        if (child == null)
        {
            Set(false, $"No child PRO found for parent PRO #{RollupProId}. Create parent-child structure first.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costSvc.PostCostAsync(
            costObjectType: CostObjectType.ProductionOrder,
            costObjectId: child.Id,
            transactionType: CostTransactionType.DirectLabor,
            costBucket: ProductionCostBucket.DirectLabor,
            companyId: 1, siteId: child.LocationId, productionOrderId: child.Id,
            operationId: 20, bomLineId: null, itemId: null,
            quantity: 1.75m, uom: "HR", unitCost: 52.80m,  // CNC mill operator, 2nd shift differential
            sourceTransactionType: "LaborEntry", sourceTransactionId: null,
            lotNumber: null, serialNumber: null, heatNumber: null,
            rollupAdditive: true,
            notes: $"Direct labor — 1.75 hr CNC 5-axis mill Op 20, child PRO #{child.Id} for parent PRO #{RollupProId}",
            postedBy: by, ct: ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"CostTransaction #{r.Value!.Id} posted: labor ${r.Value.ExtendedCost:N2} on child PRO #{child.Id}."
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. Post Child→Parent Transfer
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostChildTransferAsync(CancellationToken ct)
    {
        var child = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .Where(p => p.ParentProductionOrderId == RollupProId)
            .FirstOrDefaultAsync(ct);

        if (child == null)
        {
            Set(false, $"No child PRO found for parent PRO #{RollupProId}.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costSvc.PostTransferAsync(
            sourceCostObjectType: CostObjectType.ProductionOrder,
            sourceCostObjectId: child.Id, sourceSiteId: child.LocationId,
            destCostObjectType: CostObjectType.ProductionOrder,
            destCostObjectId: RollupProId, destSiteId: 1,
            companyId: 1, transferType: CostTransferType.ChildCompletionToParent,
            quantity: 1m, uom: "EA", unitCost: 312.40m,    // Sub-assy total cost
            materialCost: 156.20m,    // Steel bar stock + fasteners + seal
            laborCost: 92.40m,        // 1.75 hr CNC mill
            overheadCost: 38.50m,     // 42% labor burden
            subcontractCost: 0m,
            otherCost: 25.30m,        // Tooling + inspection
            isProvisional: false,
            notes: $"Child PRO #{child.Id} completion → parent PRO #{RollupProId}, final cost at child close",
            postedBy: by, ct: ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"CostTransfer #{r.Value!.Id} posted: ${r.Value.TransferExtendedCost:N2} from child PRO #{child.Id} → parent PRO #{RollupProId}."
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. View Latest Rollup Run
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostViewLatestRunAsync(CancellationToken ct)
    {
        if (RollupProId <= 0) { Set(false, "PRO Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        LatestRun = await _rollupSvc.GetLatestRunAsync(RollupProId, ct);
        if (LatestRun == null)
        {
            Set(false, $"No rollup runs found for PRO #{RollupProId}.");
        }
        else
        {
            LoadedLines = await _rollupSvc.GetLinesAsync(LatestRun.Id, ct);
            LoadedExceptions = await _rollupSvc.GetExceptionsAsync(LatestRun.Id, ct);
            Set(true,
                $"Latest run #{LatestRun.Id} '{LatestRun.RunNumber}': {LatestRun.Mode} Status={LatestRun.Status} " +
                $"Additive=${LatestRun.TotalAdditiveCost:N2} Transfer=${LatestRun.TotalTransferCost:N2} " +
                $"({LatestRun.LineCount} lines, {LatestRun.ExceptionCount} exceptions).");
        }

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. View Rollup Lines by Run Id
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostViewRunLinesAsync(CancellationToken ct)
    {
        if (ViewRunId <= 0) { Set(false, "Run Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        LoadedLines = await _rollupSvc.GetLinesAsync(ViewRunId, ct);
        LoadedExceptions = await _rollupSvc.GetExceptionsAsync(ViewRunId, ct);
        Set(true, $"Loaded {LoadedLines.Count} lines + {LoadedExceptions.Count} exceptions for run #{ViewRunId}.");

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. View Rollup Exceptions by Run Id
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostViewRunExceptionsAsync(CancellationToken ct)
    {
        if (ViewRunId <= 0) { Set(false, "Run Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        LoadedExceptions = await _rollupSvc.GetExceptionsAsync(ViewRunId, ct);
        Set(true, $"Loaded {LoadedExceptions.Count} exceptions for run #{ViewRunId}.");

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. Build Graph (diagnostic — show node structure)
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostBuildGraphAsync(CancellationToken ct)
    {
        if (RollupProId <= 0) { Set(false, "PRO Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        var r = await _rollupSvc.BuildGraphAsync(RollupProId, ct);
        if (r.IsSuccess)
        {
            var root = r.Value!;
            var (nodes, edges, depth) = CountMetrics(root);
            Set(true, $"Graph built for PRO #{RollupProId}: {nodes} nodes, {edges} edges, depth {depth}. " +
                $"Root: {root.Label} (Site={root.SiteId}) with {root.Children.Count} direct children, " +
                $"{root.OriginatingCosts.Count} originating costs, {root.InboundTransfers.Count} inbound transfers.");
        }
        else
        {
            Set(false, r.Error);
        }

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalRollupRuns = await _db.Set<CostRollupRun>().CountAsync(ct);
        TotalRollupLines = await _db.Set<CostRollupLine>().CountAsync(ct);
        TotalRollupExceptions = await _db.Set<CostRollupException>().CountAsync(ct);

        if (RecentRuns.Count == 0)
        {
            RecentRuns = await _db.Set<CostRollupRun>()
                .AsNoTracking()
                .OrderByDescending(r => r.StartedAtUtc)
                .Take(10)
                .ToListAsync(ct);
        }
    }

    private static (int nodes, int edges, int depth) CountMetrics(CostGraphNode node)
    {
        int n = 1, e = node.Children.Count, d = node.Depth;
        foreach (var c in node.Children)
        {
            var (cn, ce, cd) = CountMetrics(c);
            n += cn; e += ce; if (cd > d) d = cd;
        }
        return (n, e, d);
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
