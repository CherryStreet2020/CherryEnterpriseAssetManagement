// Theme B11 Wave R4-12 — admin probe for the dispatch board (CLOSES Wave R4).
// Lock-16 corollary: Setup writes (3 WCs with different dispatch rules, 3 orders with
// different due/priority/work, 9 released ops); "Dispatch next" writes (top op → InSetup).
//
//   1) Set up worked example — three jobs A/B/C (A due soonest + low priority + long;
//      B due latest + high priority + short; C in the middle) each placed on three WCs
//      whose rules differ (EarliestDueDate / HighestPriority / ShortestProcessingTime),
//      so the SAME three jobs sort into THREE different run orders.
//   2) Show dispatch board — renders every WC column ordered by its own rule.
//   3) Dispatch next (EDD WC) — supervisor releases the top-ranked queued op into setup.
//   R) Reload.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B11 R4-12 dispatch board. Seeds three work centers with " +
    "different dispatch rules and three jobs with different due/priority/work, then renders the " +
    "board and supports supervisor dispatch-next, all tenant-scoped to the operator's company.")]
public sealed class DispatchBoardProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IDispatchBoardService _board;
    private readonly ILogger<DispatchBoardProbeModel> _logger;

    public DispatchBoardProbeModel(AppDbContext db, ITenantContext tenant,
        IDispatchBoardService board, ILogger<DispatchBoardProbeModel> logger)
    {
        _db = db; _tenant = tenant; _board = board; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public int CompanyId { get; private set; }
    public DispatchBoard? Board { get; private set; }

    // (orderNo, title, priority, dueDaysOut, setupMins, runMins)
    private static readonly (string No, string Title, int Priority, int DueDays, decimal Setup, decimal Run)[] Jobs =
    {
        ("DISPATCH-A", "Lot A — Inconel 718 (due soon, low priority, long run)", 10, 2, 60m, 540m),
        ("DISPATCH-B", "Lot B — Ti-6Al-4V (due late, high priority, short run)", 90, 10, 30m, 90m),
        ("DISPATCH-C", "Lot C — 17-4PH (mid due, mid priority, mid run)",        50, 6, 60m, 240m),
    };
    private static readonly (string Code, string Name, WorkCenterDispatchRule Rule)[] Wcs =
    {
        ("DISPATCH-EDD", "Mill cell — earliest-due dispatch", WorkCenterDispatchRule.EarliestDueDate),
        ("DISPATCH-PRIO", "Turn cell — highest-priority dispatch", WorkCenterDispatchRule.HighestPriority),
        ("DISPATCH-SPT", "Deburr — shortest-processing-time dispatch", WorkCenterDispatchRule.ShortestProcessingTime),
    };

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
    private int Company() => _tenant.VisibleCompanyIds.FirstOrDefault();

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);
    private Task LoadStatsAsync(CancellationToken ct) { CompanyId = Company(); return Task.CompletedTask; }

    // ── 1) Setup ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostSetupAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company."); await LoadStatsAsync(ct); return Page(); }
        var locationId = await _db.Locations.Where(l => l.CompanyId == companyId)
            .Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        if (locationId == null) { Set(false, "No Location in this company — seed master data first."); await LoadStatsAsync(ct); return Page(); }

        var now = DateTime.UtcNow;

        // Three work centers, each with a different dispatch rule.
        var wcIds = new Dictionary<string, int>();
        foreach (var (code, name, rule) in Wcs)
            wcIds[code] = (await EnsureWcAsync(companyId, locationId.Value, code, name, rule, ct)).Id;

        // Three orders with different due / priority; one op per order on EACH WC.
        foreach (var job in Jobs)
        {
            var order = await EnsureOrderAsync(companyId, locationId.Value, job.No, job.Title, job.Priority,
                now.AddDays(job.DueDays), ct);
            var stale = await _db.ProductionOperations.Where(o => o.ProductionOrderId == order.Id).ToListAsync(ct);
            if (stale.Count > 0) { _db.ProductionOperations.RemoveRange(stale); await _db.SaveChangesAsync(ct); }
            int seq = 10;
            foreach (var (code, _, _) in Wcs)
            {
                _db.ProductionOperations.Add(new ProductionOperation
                {
                    ProductionOrderId = order.Id, WorkCenterId = wcIds[code], CompanyIdSnapshot = companyId,
                    LocationIdSnapshot = locationId.Value, SequenceNumber = seq, Description = $"{job.No} @ {code}",
                    Status = ProductionOperationStatus.Released,
                    PlannedSetupMins = job.Setup, PlannedRunMins = job.Run, PlannedQty = 20, CreatedBy = "DispatchBoardProbe",
                });
                seq += 10;
            }
            await _db.SaveChangesAsync(ct);
        }

        Set(true, $"Worked example ready (company #{companyId}). 3 jobs (A due+2d/pri 10/10h · B due+10d/pri 90/2h · " +
                  $"C due+6d/pri 50/5h) queued on 3 WCs with different rules — the SAME jobs sort differently per column. " +
                  $"Now Show dispatch board.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // ── 2) Show board ───────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostShowAsync(CancellationToken ct)
    {
        var r = await _board.GetBoardAsync(Company(), ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        Board = r.Value;
        var probeCols = Board!.Columns.Count(c => c.WorkCenterCode.StartsWith("DISPATCH-"));
        Set(true, $"Dispatch board: {Board.Columns.Count} WC column(s) with queued work ({probeCols} from this probe), " +
                  $"each ordered by its own dispatch rule.");
        return Page();
    }

    // ── 3) Dispatch next on the EDD work center ──────────────────────────────────────
    public async Task<IActionResult> OnPostDispatchNextAsync(CancellationToken ct)
    {
        var companyId = Company();
        var eddWcId = await _db.WorkCenters.Where(w => w.CompanyId == companyId && w.Code == "DISPATCH-EDD")
            .Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
        if (eddWcId == null) { Set(false, "Run Set up worked example first."); await LoadStatsAsync(ct); return Page(); }

        var r = await _board.DispatchNextAsync(eddWcId.Value, ct);
        if (r.IsFailure) { Set(false, r.Error); await ShowAfterAsync(companyId, ct); return Page(); }
        Set(true, $"Dispatched on DISPATCH-EDD: {r.Value.OrderNumber} (op #{r.Value.ProductionOperationId}) → InSetup. " +
                  $"It drops off the queue and the next-due job moves to rank 1.");
        await ShowAfterAsync(companyId, ct);
        return Page();
    }

    private async Task ShowAfterAsync(int companyId, CancellationToken ct)
    {
        var b = await _board.GetBoardAsync(companyId, ct);
        if (b.IsSuccess) Board = b.Value;
        await LoadStatsAsync(ct);
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }

    // ── ensure helpers ─────────────────────────────────────────────────────────────
    private async Task<WorkCenter> EnsureWcAsync(int companyId, int locationId, string code, string name,
        WorkCenterDispatchRule rule, CancellationToken ct)
    {
        var wc = await _db.WorkCenters.FirstOrDefaultAsync(w => w.CompanyId == companyId && w.Code == code, ct);
        if (wc == null)
        {
            wc = new WorkCenter
            {
                CompanyId = companyId, Code = code, Name = name, LocationId = locationId,
                Type = WorkCenterType.Machine, Status = WorkCenterStatus.Active,
                CapacityModel = WorkCenterCapacityModel.SingleResource, DispatchRule = rule,
                IsActive = true, UtilizationPct = 100, EfficiencyPct = 100, CreatedBy = "DispatchBoardProbe",
            };
            _db.WorkCenters.Add(wc);
        }
        else { wc.DispatchRule = rule; wc.IsActive = true; wc.ModifiedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return wc;
    }

    private async Task<ProductionOrder> EnsureOrderAsync(int companyId, int locationId, string orderNo, string title,
        int priority, DateTime due, CancellationToken ct)
    {
        var po = await _db.ProductionOrders.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.OrderNumber == orderNo, ct);
        if (po == null)
        {
            po = new ProductionOrder
            {
                CompanyId = companyId, OrderNumber = orderNo, LocationId = locationId, Title = title,
                Status = ProductionOrderStatus.Released, QuantityOrdered = 20, Priority = priority, ScheduledEnd = due,
            };
            _db.ProductionOrders.Add(po);
        }
        else { po.Priority = priority; po.ScheduledEnd = due; po.ModifiedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return po;
    }
}
