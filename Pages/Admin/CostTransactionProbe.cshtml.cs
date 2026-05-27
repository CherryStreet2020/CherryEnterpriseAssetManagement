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

// Sprint 14.4 (2026-05-27) — admin probe for ICostTransactionService.
//
// FIVE WRITE/ACTION BUTTONS per Lock 16 corollary:
//   1. Insert Test Cost Transaction  (PostCostAsync — material issue)
//   2. Insert Test Cost Transaction  (PostCostAsync — labor posting)
//   3. Insert Test Transfer           (PostTransferAsync — child→parent)
//   4. Refresh Summary for PRO        (RefreshSummaryAsync)
//   5. Load Transactions for PRO      (read — GetForProductionOrderAsync)
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count/list queries. All writes flow through ICostTransactionService.")]
public sealed class CostTransactionProbeModel : PageModel
{
    private readonly ICostTransactionService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<CostTransactionProbeModel> _logger;

    public CostTransactionProbeModel(
        ICostTransactionService svc,
        AppDbContext db,
        ILogger<CostTransactionProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Bind properties ─────────────────────────────────────────

    // Refresh / Load PRO
    [BindProperty] public int RefreshProId { get; set; } = 1;
    [BindProperty] public int LoadProId { get; set; } = 1;

    // ── Output ──────────────────────────────────────────────────

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    // Summary stats
    public int TotalCostTransactions { get; private set; }
    public int TotalCostTransfers { get; private set; }
    public int TotalCostSummaries { get; private set; }
    public Dictionary<string, int> BucketCounts { get; private set; } = new();

    // Recent transactions
    public IReadOnlyList<CostTransaction> RecentTransactions { get; private set; } = Array.Empty<CostTransaction>();
    public IReadOnlyList<CostTransfer> RecentTransfers { get; private set; } = Array.Empty<CostTransfer>();
    public ProductionOrderCostSummary? LoadedSummary { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    // OnGet — load summary stats + recent rows
    // ═══════════════════════════════════════════════════════════════

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. Insert Test Cost Transaction — material issue (BRG-6207-2RS)
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostInsertTestTransactionAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.PostCostAsync(
            costObjectType: CostObjectType.ProductionOrder,
            costObjectId: 1,
            transactionType: CostTransactionType.MaterialIssue,
            costBucket: ProductionCostBucket.DirectMaterial,
            companyId: 1,
            siteId: 1,
            productionOrderId: 1,
            operationId: 10,
            bomLineId: 1,
            itemId: 1,
            quantity: 4m,
            uom: "EA",
            unitCost: 18.90m,         // BRG-6207-2RS deep groove ball bearing
            sourceTransactionType: "MaterialTransaction",
            sourceTransactionId: null,
            lotNumber: "LOT-2026-SKF-0412",
            serialNumber: null,
            heatNumber: "HT-4140-0891",
            rollupAdditive: true,
            notes: "Material issue — 4x BRG-6207-2RS bearing, SKF lot 0412, Op 10 CNC lathe spindle sub-assy",
            postedBy: by,
            ct: ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"CostTransaction #{r.Value!.Id} '{r.Value.TransactionNumber}' posted: {r.Value.TransactionType} {r.Value.CostBucket} {r.Value.Quantity:N4} x ${r.Value.UnitCost:N4} = ${r.Value.ExtendedCost:N2}."
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Insert Test Cost Transaction — direct labor posting
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostInsertTestLaborAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.PostCostAsync(
            costObjectType: CostObjectType.ProductionOrder,
            costObjectId: 1,
            transactionType: CostTransactionType.DirectLabor,
            costBucket: ProductionCostBucket.DirectLabor,
            companyId: 1,
            siteId: 1,
            productionOrderId: 1,
            operationId: 10,
            bomLineId: null,
            itemId: null,
            quantity: 2.5m,           // 2.5 hours
            uom: "HR",
            unitCost: 47.25m,         // Journeyman machinist rate
            sourceTransactionType: "LaborEntry",
            sourceTransactionId: null,
            lotNumber: null,
            serialNumber: null,
            heatNumber: null,
            rollupAdditive: true,
            notes: "Direct labor — 2.5 hr CNC lathe Op 10, spindle bore + face, journeyman rate",
            postedBy: by,
            ct: ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"CostTransaction #{r.Value!.Id} '{r.Value.TransactionNumber}' posted: {r.Value.TransactionType} {r.Value.CostBucket} {r.Value.Quantity:N4} x ${r.Value.UnitCost:N4} = ${r.Value.ExtendedCost:N2}."
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Insert Test Transfer — child WO → parent PRO
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostInsertTestTransferAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.PostTransferAsync(
            sourceCostObjectType: CostObjectType.ChildWorkOrder,
            sourceCostObjectId: 2,
            sourceSiteId: 1,
            destCostObjectType: CostObjectType.ProductionOrder,
            destCostObjectId: 1,
            destSiteId: 1,
            companyId: 1,
            transferType: CostTransferType.ChildCompletionToParent,
            quantity: 1m,
            uom: "EA",
            unitCost: 284.60m,        // Sub-assy total rolled-up cost
            materialCost: 142.30m,    // BRG-6207-2RS + SFT-4140-25MM + SEAL-CR-17386
            laborCost: 71.10m,        // 1.5 hr CNC + 0.25 hr assembly
            overheadCost: 42.50m,     // 60% labor burden
            subcontractCost: 0m,
            otherCost: 28.70m,        // Tooling amortization + inspection
            isProvisional: false,
            notes: "Child WO-1002 completion → parent PRO-1001, spindle sub-assy, final cost at completion",
            postedBy: by,
            ct: ct);

        Set(r.IsSuccess, r.IsSuccess
            ? $"CostTransfer #{r.Value!.Id} '{r.Value.TransferNumber}' posted: {r.Value.TransferType} ${r.Value.TransferExtendedCost:N2} from {r.Value.SourceCostObjectType}#{r.Value.SourceCostObjectId} → {r.Value.DestinationCostObjectType}#{r.Value.DestinationCostObjectId}."
            : r.Error);

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Refresh Summary for PRO
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostRefreshSummaryAsync(CancellationToken ct)
    {
        if (RefreshProId <= 0)
        {
            Set(false, "Production Order Id must be > 0.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RefreshSummaryAsync(RefreshProId, by, ct);

        if (r.IsSuccess)
        {
            var s = r.Value!;
            Set(true,
                $"Summary refreshed for PRO #{RefreshProId}: Actual=${s.ActualTotalCost:N2} WIP=${s.WipBalance:N2} Variance=${s.CostVariance:N2} Status={s.RollupStatus}. " +
                $"Material=${s.ActualMaterialCost:N2} Labor=${s.ActualLaborCost:N2} Burden=${s.ActualBurdenCost:N2}.");
            LoadedSummary = s;
        }
        else
        {
            Set(false, r.Error);
        }

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. Load Transactions for PRO (read)
    // ═══════════════════════════════════════════════════════════════

    public async Task<IActionResult> OnPostLoadTransactionsAsync(CancellationToken ct)
    {
        if (LoadProId <= 0)
        {
            Set(false, "Production Order Id must be > 0.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var txns = await _svc.GetForProductionOrderAsync(LoadProId, ct);
        var xfers = await _svc.GetTransfersForObjectAsync(CostObjectType.ProductionOrder, LoadProId, ct);
        LoadedSummary = await _svc.GetSummaryAsync(LoadProId, ct);

        RecentTransactions = txns.OrderByDescending(t => t.EffectiveCostDate).Take(20).ToList();
        RecentTransfers = xfers.OrderByDescending(t => t.CreatedAtUtc).Take(20).ToList();

        Set(true, $"Loaded {txns.Count} transactions + {xfers.Count} transfers for PRO #{LoadProId}." +
            (LoadedSummary != null ? $" Summary: Actual=${LoadedSummary.ActualTotalCost:N2} WIP=${LoadedSummary.WipBalance:N2}." : " No summary row yet."));

        await LoadStatsAsync(ct);
        return Page();
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalCostTransactions = await _db.Set<CostTransaction>().CountAsync(ct);
        TotalCostTransfers = await _db.Set<CostTransfer>().CountAsync(ct);
        TotalCostSummaries = await _db.Set<ProductionOrderCostSummary>().CountAsync(ct);

        // Per-bucket breakdown
        BucketCounts = await _db.Set<CostTransaction>()
            .GroupBy(t => t.CostBucket)
            .Select(g => new { Bucket = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Bucket.ToString(), x => x.Count, ct);

        // Load recent if not already loaded by a handler
        if (RecentTransactions.Count == 0)
        {
            RecentTransactions = await _db.Set<CostTransaction>()
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAtUtc)
                .Take(20)
                .ToListAsync(ct);
        }

        if (RecentTransfers.Count == 0)
        {
            RecentTransfers = await _db.Set<CostTransfer>()
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAtUtc)
                .Take(20)
                .ToListAsync(ct);
        }
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
