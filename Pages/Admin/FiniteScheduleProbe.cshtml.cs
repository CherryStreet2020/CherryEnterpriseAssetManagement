// Theme B11 Wave R4-11 — admin probe for the finite scheduler (OPENS the engine).
// Lock-16 corollary: Setup writes (calendar, 2 WCs, alternate link, a parent PRO +
// child + ops, and a COMPETING order whose committed op blocks the primary WC on
// Friday). "Run finite schedule" commits (stamps PlannedStart/End + re-homes ops).
//
//   1) Set up worked example — primary MILL-A (SingleResource, cap 1) + alt MILL-B,
//      linked as a WorkCenterAlternate. Parent PRO due next Friday 17:00; one child
//      with three 6 h ops on MILL-A. A separate order occupies MILL-A all of Friday.
//   2) Run finite schedule (backward, commit) — ops floored into Mon-Fri 8-5 working
//      windows; ops that land on the (loaded) Friday SPILL to MILL-B, earlier-week ops
//      stay on MILL-A. Renders every placement + the decision (read the table).
//   3) What-if (forward, no commit) — forward projection, nothing persisted.
//   R) Reload.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
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
    "Admin diagnostic probe for Theme B11 R4-11 finite scheduler. Builds a self-contained " +
    "worked example (calendar, primary + alternate work centers, a production order with a " +
    "child + operations, and a competing order that loads the primary) and runs " +
    "IFiniteSchedulingService backward/forward, all tenant-scoped to the operator's company.")]
public sealed class FiniteScheduleProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IFiniteSchedulingService _scheduler;
    private readonly ILogger<FiniteScheduleProbeModel> _logger;

    public FiniteScheduleProbeModel(AppDbContext db, ITenantContext tenant,
        IFiniteSchedulingService scheduler, ILogger<FiniteScheduleProbeModel> logger)
    {
        _db = db; _tenant = tenant; _scheduler = scheduler; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int CompanyId { get; private set; }
    public DateTime DueUtc { get; private set; }
    public int? ParentProId { get; private set; }
    public FiniteScheduleResult? Result { get; private set; }

    private const string CalCode = "FINSCHED-CAL";
    private const string WcAName = "FINSCHED-MILL-A";
    private const string WcBName = "FINSCHED-MILL-B";
    private const string ParentNo = "FINSCHED-PRO";
    private const string ChildNo = "FINSCHED-PRO-CHILD";
    private const string CompeteNo = "FINSCHED-COMPETE";

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
    private int Company() => _tenant.VisibleCompanyIds.FirstOrDefault();

    // Next Friday 17:00 UTC (a working-window instant in a Mon-Fri 8-5 calendar).
    private static DateTime NextFridayDue()
    {
        var today = DateTime.UtcNow.Date;
        int d = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
        if (d == 0) d = 7;
        return today.AddDays(d).AddHours(17);
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        CompanyId = Company();
        DueUtc = NextFridayDue();
        if (CompanyId <= 0) return;
        ParentProId = await _db.ProductionOrders
            .Where(p => p.CompanyId == CompanyId && p.OrderNumber == ParentNo)
            .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
    }

    // ── 1) Setup ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostSetupAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company."); await LoadStatsAsync(ct); return Page(); }
        var locationId = await _db.Locations.Where(l => l.CompanyId == companyId)
            .Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        if (locationId == null) { Set(false, "No Location in this company — seed master data first."); await LoadStatsAsync(ct); return Page(); }

        var due = NextFridayDue();
        var calId = await EnsureCalendarAsync(companyId, ct);
        var millA = await EnsureWcAsync(companyId, locationId.Value, calId, WcAName, "Haas UMC-750 (cell A)", ct);
        var millB = await EnsureWcAsync(companyId, locationId.Value, calId, WcBName, "Haas UMC-750 (cell B)", ct);
        await EnsureAlternateAsync(companyId, millA.Id, millB.Id, ct);

        // Competing order: a committed op holding MILL-A for the WHOLE of Friday (00:00→24:00
        // UTC) so it blocks every Friday working-window placement regardless of the calendar's
        // resolved time zone — any op the backward pass lands on Friday will collide and must
        // re-home; ops that land earlier in the week stay on MILL-A.
        var compete = await EnsureOrderAsync(companyId, locationId.Value, CompeteNo, "Competing lot (Ti-6Al-4V brackets)", null, null, ct);
        var friBlockStart = due.Date;            // Friday 00:00 UTC
        var friBlockEnd = due.Date.AddDays(1);   // Saturday 00:00 UTC
        await ReplaceOpsAsync(compete.Id, companyId, locationId.Value, millA.Id,
            new[] { (10, 60m, 480m, (DateTime?)friBlockStart, (DateTime?)friBlockEnd) }, ct);

        // Our order: due next Friday 17:00; one child with three 6 h ops (setup 60 + run 300) on MILL-A.
        var parent = await EnsureOrderAsync(companyId, locationId.Value, ParentNo,
            "Inconel 718 manifold lot (finite-schedule demo)", ProductionOrderStatus.Released, due, ct);
        var child = await EnsureOrderAsync(companyId, locationId.Value, ChildNo,
            "Manifold sub-assembly", ProductionOrderStatus.Released, due, ct, parentId: parent.Id);
        await ReplaceOpsAsync(child.Id, companyId, locationId.Value, millA.Id, new[]
        {
            (10, 60m, 300m, (DateTime?)null, (DateTime?)null),
            (20, 60m, 300m, (DateTime?)null, (DateTime?)null),
            (30, 60m, 300m, (DateTime?)null, (DateTime?)null),
        }, ct);

        Set(true, $"Worked example ready (company #{companyId}). Parent {ParentNo} due {due:yyyy-MM-dd HH:mm}Z; " +
                  $"child has three 6 h ops on {WcAName} (cap 1). {WcBName} is its alternate. A competing order holds " +
                  $"{WcAName} for all of Friday — so any op the backward pass lands on Friday re-homes to {WcBName}, " +
                  $"while ops that fall earlier in the week stay on {WcAName}. Now Run finite schedule (see the table).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // ── 2) Run finite (backward, commit) ───────────────────────────────────────────
    public async Task<IActionResult> OnPostRunBackwardAsync(CancellationToken ct)
    {
        var parentId = await ParentIdAsync(ct);
        if (parentId == null) { Set(false, "No parent order — click Set up worked example first."); await LoadStatsAsync(ct); return Page(); }
        var r = await _scheduler.ScheduleAsync(parentId.Value, ScheduleDirection.Backward, commit: true, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        Result = r.Value;
        Set(true, $"Backward finite schedule committed: {Result!.OperationsPlaced} ops, " +
                  $"{Result.OperationsMovedToAlternate} re-homed to an alternate WC, " +
                  $"{Result.OperationsOnOverloaded} left on an overloaded WC; span {Result.TotalSpannedDays} day(s).");
        return Page();
    }

    // ── 3) What-if (forward, no commit) ─────────────────────────────────────────────
    public async Task<IActionResult> OnPostWhatIfForwardAsync(CancellationToken ct)
    {
        var parentId = await ParentIdAsync(ct);
        if (parentId == null) { Set(false, "No parent order — click Set up worked example first."); await LoadStatsAsync(ct); return Page(); }
        var r = await _scheduler.ScheduleAsync(parentId.Value, ScheduleDirection.Forward, commit: false, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        Result = r.Value;
        Set(true, $"Forward what-if (not persisted): {Result!.OperationsPlaced} ops projected, " +
                  $"{Result.OperationsMovedToAlternate} re-homed; span {Result.TotalSpannedDays} day(s).");
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }

    private Task<int?> ParentIdAsync(CancellationToken ct) => _db.ProductionOrders
        .Where(p => p.CompanyId == Company() && p.OrderNumber == ParentNo)
        .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);

    // ── ensure helpers ─────────────────────────────────────────────────────────────
    private async Task<int> EnsureCalendarAsync(int companyId, CancellationToken ct)
    {
        var id = await _db.WorkCalendars.Where(w => w.Code == CalCode && (w.CompanyId == companyId || w.CompanyId == null))
            .Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
        if (id != null) return id.Value;
        var cal = new WorkCalendar
        {
            CompanyId = companyId, Code = CalCode, Name = "Finite-schedule standard week",
            TimeZone = "America/New_York", WorkDayMask = 62,
            WorkDayStart = new TimeSpan(8, 0, 0), WorkDayEnd = new TimeSpan(17, 0, 0),
            IsActive = true, IsDefault = false,
        };
        _db.WorkCalendars.Add(cal); await _db.SaveChangesAsync(ct);
        return cal.Id;
    }

    private async Task<WorkCenter> EnsureWcAsync(int companyId, int locationId, int calId, string code, string name, CancellationToken ct)
    {
        var wc = await _db.WorkCenters.FirstOrDefaultAsync(w => w.CompanyId == companyId && w.Code == code, ct);
        if (wc == null)
        {
            wc = new WorkCenter
            {
                CompanyId = companyId, Code = code, Name = name, LocationId = locationId, CalendarId = calId,
                Type = WorkCenterType.Machine, Status = WorkCenterStatus.Active,
                CapacityModel = WorkCenterCapacityModel.SingleResource, SimultaneousOperationsMax = 1,
                IsActive = true, UtilizationPct = 100, EfficiencyPct = 100, CreatedBy = "FiniteScheduleProbe",
            };
            _db.WorkCenters.Add(wc);
        }
        else { wc.CalendarId = calId; wc.SimultaneousOperationsMax = 1; wc.IsActive = true; wc.ModifiedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return wc;
    }

    private async Task EnsureAlternateAsync(int companyId, int wcId, int altWcId, CancellationToken ct)
    {
        var ex = await _db.Set<WorkCenterAlternate>()
            .FirstOrDefaultAsync(a => a.WorkCenterId == wcId && a.AlternateWorkCenterId == altWcId, ct);
        if (ex == null)
        {
            _db.Set<WorkCenterAlternate>().Add(new WorkCenterAlternate
            {
                CompanyId = companyId, WorkCenterId = wcId, AlternateWorkCenterId = altWcId,
                Preference = 10, IsActive = true, Notes = "FiniteScheduleProbe spill target",
            });
        }
        else { ex.IsActive = true; ex.Preference = 10; }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ProductionOrder> EnsureOrderAsync(
        int companyId, int locationId, string orderNo, string title,
        ProductionOrderStatus? status, DateTime? scheduledEnd, CancellationToken ct, int? parentId = null)
    {
        var po = await _db.ProductionOrders.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.OrderNumber == orderNo, ct);
        if (po == null)
        {
            po = new ProductionOrder
            {
                CompanyId = companyId, OrderNumber = orderNo, LocationId = locationId, Title = title,
                Status = status ?? ProductionOrderStatus.Released, QuantityOrdered = 25,
                ScheduledEnd = scheduledEnd, ParentProductionOrderId = parentId,
            };
            _db.ProductionOrders.Add(po);
        }
        else { po.ScheduledEnd = scheduledEnd ?? po.ScheduledEnd; po.ParentProductionOrderId = parentId ?? po.ParentProductionOrderId; po.ModifiedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return po;
    }

    // (seq, setupMins, runMins, plannedStart, plannedEnd)
    private async Task ReplaceOpsAsync(int orderId, int companyId, int locationId, int wcId,
        IEnumerable<(int seq, decimal setup, decimal run, DateTime? start, DateTime? end)> ops, CancellationToken ct)
    {
        var stale = await _db.ProductionOperations.Where(o => o.ProductionOrderId == orderId).ToListAsync(ct);
        if (stale.Count > 0) { _db.ProductionOperations.RemoveRange(stale); await _db.SaveChangesAsync(ct); }
        foreach (var (seq, setup, run, start, end) in ops)
        {
            _db.ProductionOperations.Add(new ProductionOperation
            {
                ProductionOrderId = orderId, WorkCenterId = wcId, CompanyIdSnapshot = companyId,
                LocationIdSnapshot = locationId, SequenceNumber = seq, Description = $"Op {seq}",
                Status = ProductionOperationStatus.Released,
                PlannedSetupMins = setup, PlannedRunMins = run,
                PlannedStart = start, PlannedEnd = end, PlannedQty = 25, CreatedBy = "FiniteScheduleProbe",
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
