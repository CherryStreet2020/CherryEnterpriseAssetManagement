// Theme B11 Wave R4-10 — admin probe for the resource load profile + calendar engine
// (OPENS Wave R4). Lock-16 corollary: every button writes (Setup builds the worked
// example: a calendar, 3 work centers, machine resources, a production order with
// planned operations sized to differentiate load, and a downtime exception).
//
//   1) Set up worked example — idempotently builds a self-contained plant: STD-MFG
//      calendar (Mon-Fri 08:00-17:00 ET) + 3 WCs (5-axis mill / turning / deburr) +
//      a ProductionResource per WC (mill bridged to a real Asset) + a PRO with
//      planned ops sized so the mill is over-committed (the drum) + a Tuesday
//      downtime exception on the mill resource.
//   2) Compute WC load    — GetPlantLoadAsync(WorkCenter) over the window → ranked
//      Load% + the drum.
//   3) Compute resource load — GetPlantLoadAsync(Resource) → per-machine Load%; the
//      mill resource's available hours reflect the downtime exception.
//   R) Reload.
//
// All targets resolve their own CompanyId and are tenant-guarded inside the service.
// The probe writes are stamped with the operator's visible company.

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
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B11 R4-10 resource load + calendar engine. " +
    "Builds a self-contained worked example (calendar, work centers, resources, a " +
    "production order with planned ops, a downtime exception) and runs " +
    "IResourceLoadService projected-load + drum detection, all tenant-scoped.")]
public sealed class ResourceLoadProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IResourceLoadService _load;
    private readonly ILogger<ResourceLoadProbeModel> _logger;

    public ResourceLoadProbeModel(AppDbContext db, ITenantContext tenant,
        IResourceLoadService load, ILogger<ResourceLoadProbeModel> logger)
    {
        _db = db; _tenant = tenant; _load = load; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int CompanyId { get; private set; }
    public DateTime WindowFromUtc { get; private set; }
    public DateTime WindowToUtc { get; private set; }
    public int WorkCenterCount { get; private set; }
    public int ResourceCount { get; private set; }

    public PlantLoadProfile? PlantResult { get; private set; }

    private const string CalCode = "STD-MFG-WEEK";
    private const string ProNumber = "LOADPROBE-PRO-1";
    private static readonly (string Code, string Name)[] WcDefs =
    {
        ("LOADPROBE-MILL", "Haas UMC-750 5-axis mill cell"),
        ("LOADPROBE-TURN", "Mazak QTN-250 turning cell"),
        ("LOADPROBE-DEBURR", "Deburr / finishing bench"),
    };

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    // Deterministic window: next Monday 00:00 UTC for 5 days (Mon-Fri working).
    private static (DateTime from, DateTime to) Window()
    {
        var today = DateTime.UtcNow.Date;
        int daysUntilMon = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMon == 0) daysUntilMon = 7; // always the NEXT Monday
        var from = today.AddDays(daysUntilMon);
        return (from, from.AddDays(5));
    }

    private int Company() => _tenant.VisibleCompanyIds.FirstOrDefault();

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        CompanyId = Company();
        (WindowFromUtc, WindowToUtc) = Window();
        if (CompanyId <= 0) return;
        var codes = WcDefs.Select(d => d.Code).ToArray();
        WorkCenterCount = await _db.WorkCenters
            .CountAsync(w => w.CompanyId == CompanyId && codes.Contains(w.Code), ct);
        ResourceCount = await _db.ProductionResources
            .CountAsync(r => r.CompanyId == CompanyId && r.Code.StartsWith("LOADPROBE-"), ct);
    }

    // ── 1) Build the worked example ──────────────────────────────────────────────
    public async Task<IActionResult> OnPostSetupAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company in tenant scope."); await LoadStatsAsync(ct); return Page(); }

        // A valid Location to stamp the WCs (no FK enforced, but reference a real one).
        var locationId = await _db.Locations
            .Where(l => l.CompanyId == companyId).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        if (locationId == null) { Set(false, "No Location in this company — seed master data first."); await LoadStatsAsync(ct); return Page(); }

        var (from, to) = Window();

        // Calendar: reuse the company/system default if present, else create STD-MFG-WEEK.
        var calId = await EnsureCalendarAsync(companyId, ct);

        // Three work centers with that calendar.
        var mill = await EnsureWorkCenterAsync(companyId, locationId.Value, calId, WcDefs[0].Code, WcDefs[0].Name, ct);
        var turn = await EnsureWorkCenterAsync(companyId, locationId.Value, calId, WcDefs[1].Code, WcDefs[1].Name, ct);
        var deburr = await EnsureWorkCenterAsync(companyId, locationId.Value, calId, WcDefs[2].Code, WcDefs[2].Name, ct);

        // A real Asset to bridge the mill resource (so per-resource committed load resolves).
        var millAssetId = await _db.Assets
            .Where(a => a.CompanyId == companyId).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);

        // One machine resource per WC; the mill resource bridges to the asset.
        var millRes = await EnsureResourceAsync(companyId, mill.Id, calId, "LOADPROBE-RES-MILL",
            "Haas UMC-750 (cell A)", millAssetId, ct);
        await EnsureResourceAsync(companyId, turn.Id, calId, "LOADPROBE-RES-TURN", "Mazak QTN-250", null, ct);
        await EnsureResourceAsync(companyId, deburr.Id, calId, "LOADPROBE-RES-DEBURR", "Deburr station 1", null, ct);

        // A Tuesday all-day downtime exception on the mill resource (planned PM).
        var tue = from.AddDays(1);
        await EnsureDowntimeAsync(companyId, millRes.Id, tue, tue.AddDays(1),
            "Planned preventive maintenance — spindle service", ct);

        // A production order to hang the planned operations on.
        var pro = await EnsureProductionOrderAsync(companyId, locationId.Value, ct);

        // Planned ops sized so the mill is the drum (over-committed). Available over the
        // 5-day Mon-Fri 8-5 window ≈ 45 h. Mill ≈ 50 h (>100%), turn ≈ 27 h, deburr ≈ 9 h.
        // Mill ops carry the asset so the resource view resolves committed load too.
        await ReplaceOpsAsync(pro.Id, companyId, locationId.Value, mill.Id, millAssetId, from,
            opCount: 5, setupMins: 60m, runMins: 540m, ct);   // 5 × 10 h = 50 h
        await ReplaceOpsAsync(pro.Id, companyId, locationId.Value, turn.Id, null, from,
            opCount: 3, setupMins: 30m, runMins: 510m, ct);   // 3 × 9 h = 27 h
        await ReplaceOpsAsync(pro.Id, companyId, locationId.Value, deburr.Id, null, from,
            opCount: 3, setupMins: 15m, runMins: 165m, ct);   // 3 × 3 h = 9 h

        Set(true, $"Worked example ready for company #{companyId} over {from:yyyy-MM-dd}…{to:yyyy-MM-dd} " +
                  $"(5 working days, calendar #{calId}). 3 work centers + 3 resources; mill carries 50 h of planned ops " +
                  $"(the drum), turning 27 h, deburr 9 h; Tuesday PM downtime on the mill resource. Now Compute WC load.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // ── 2) WC load + drum ────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostComputeWcAsync(CancellationToken ct)
    {
        var (from, to) = Window();
        var result = await _load.GetPlantLoadAsync(Company(), ResourceLoadTargetKind.WorkCenter, from, to, ct);
        await LoadStatsAsync(ct);
        if (result.IsFailure) { Set(false, result.Error); return Page(); }
        PlantResult = result.Value;
        // Restrict the rendered view to the probe's own WCs for a clean worked example.
        FilterToProbeTargets();
        Set(true, $"WC load over {from:yyyy-MM-dd}…{to:yyyy-MM-dd}: drum = {PlantResult!.DrumCode ?? "—"} " +
                  $"at {PlantResult.DrumLoadPct:0.#}% load.");
        return Page();
    }

    // ── 3) Resource load ─────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostComputeResourceAsync(CancellationToken ct)
    {
        var (from, to) = Window();
        var result = await _load.GetPlantLoadAsync(Company(), ResourceLoadTargetKind.Resource, from, to, ct);
        await LoadStatsAsync(ct);
        if (result.IsFailure) { Set(false, result.Error); return Page(); }
        PlantResult = result.Value;
        FilterToProbeTargets();
        Set(true, $"Resource load over {from:yyyy-MM-dd}…{to:yyyy-MM-dd}: drum = {PlantResult!.DrumCode ?? "—"} " +
                  $"at {PlantResult.DrumLoadPct:0.#}% (the mill resource's available hours reflect the Tuesday downtime).");
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }

    // Keep only the probe's own targets in the rendered profile (clean worked example).
    private void FilterToProbeTargets()
    {
        if (PlantResult == null) return;
        var kept = PlantResult.Profiles
            .Where(p => p.Code.StartsWith("LOADPROBE-")).ToList();
        var drum = kept.FirstOrDefault(p => p.CommittedHours > 0m);
        PlantResult = PlantResult with
        {
            Profiles = kept,
            DrumTargetId = drum?.TargetId,
            DrumCode = drum?.Code,
            DrumLoadPct = drum?.LoadPct ?? 0m,
        };
    }

    // ── ensure helpers (idempotent by natural key) ─────────────────────────────────
    private async Task<int> EnsureCalendarAsync(int companyId, CancellationToken ct)
    {
        var existing = await _db.WorkCalendars
            .Where(w => w.Code == CalCode && (w.CompanyId == companyId || w.CompanyId == null))
            .Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
        if (existing != null) return existing.Value;

        var cal = new WorkCalendar
        {
            CompanyId = companyId, Code = CalCode, Name = "Standard manufacturing week",
            Description = "Mon-Fri 08:00-17:00 ET — R4-10 load probe.",
            TimeZone = "America/New_York", WorkDayMask = 62, // Mon-Fri
            WorkDayStart = new TimeSpan(8, 0, 0), WorkDayEnd = new TimeSpan(17, 0, 0),
            IsActive = true, IsDefault = false,
        };
        _db.WorkCalendars.Add(cal);
        await _db.SaveChangesAsync(ct);
        return cal.Id;
    }

    private async Task<WorkCenter> EnsureWorkCenterAsync(
        int companyId, int locationId, int calId, string code, string name, CancellationToken ct)
    {
        var wc = await _db.WorkCenters.FirstOrDefaultAsync(w => w.CompanyId == companyId && w.Code == code, ct);
        if (wc == null)
        {
            wc = new WorkCenter
            {
                CompanyId = companyId, Code = code, Name = name, LocationId = locationId,
                CalendarId = calId, Type = WorkCenterType.Machine, Status = WorkCenterStatus.Active,
                CapacityModel = WorkCenterCapacityModel.SingleResource, IsActive = true,
                UtilizationPct = 100, EfficiencyPct = 100, CreatedBy = "ResourceLoadProbe",
            };
            _db.WorkCenters.Add(wc);
        }
        else { wc.CalendarId = calId; wc.IsActive = true; wc.ModifiedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return wc;
    }

    private async Task<ProductionResource> EnsureResourceAsync(
        int companyId, int wcId, int calId, string code, string name, int? assetId, CancellationToken ct)
    {
        var res = await _db.ProductionResources.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Code == code, ct);
        if (res == null)
        {
            res = new ProductionResource
            {
                CompanyId = companyId, ResourceKind = ResourceKind.Machine, Code = code, Name = name,
                Status = ProductionResourceStatus.Active, WorkCenterId = wcId, CalendarId = calId,
                AssetId = assetId, IsPrimary = true, FiniteCapacityFlag = true,
                UtilizationPct = 100, EfficiencyPct = 100, CreatedBy = "ResourceLoadProbe",
            };
            _db.ProductionResources.Add(res);
        }
        else
        {
            res.WorkCenterId = wcId; res.CalendarId = calId; res.AssetId = assetId;
            res.Status = ProductionResourceStatus.Active; res.ModifiedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return res;
    }

    private async Task EnsureDowntimeAsync(
        int companyId, int resourceId, DateTime startUtc, DateTime endUtc, string reason, CancellationToken ct)
    {
        var ex = await _db.ResourceCalendarExceptions.FirstOrDefaultAsync(
            e => e.ProductionResourceId == resourceId
                 && e.ExceptionType == ResourceCalendarExceptionType.MaintenanceWindow
                 && e.StartUtc == startUtc, ct);
        if (ex == null)
        {
            ex = new ResourceCalendarException
            {
                CompanyId = companyId, ProductionResourceId = resourceId,
                ExceptionType = ResourceCalendarExceptionType.MaintenanceWindow,
                StartUtc = startUtc, EndUtc = endUtc, Reason = reason, CreatedBy = "ResourceLoadProbe",
            };
            _db.ResourceCalendarExceptions.Add(ex);
        }
        else { ex.EndUtc = endUtc; ex.Reason = reason; ex.ModifiedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ProductionOrder> EnsureProductionOrderAsync(int companyId, int locationId, CancellationToken ct)
    {
        var pro = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.OrderNumber == ProNumber, ct);
        if (pro == null)
        {
            pro = new ProductionOrder
            {
                CompanyId = companyId, OrderNumber = ProNumber, LocationId = locationId,
                Title = "R4-10 load-probe order (Inconel 718 bracket lot)",
                Status = ProductionOrderStatus.Released, QuantityOrdered = 50,
            };
            _db.ProductionOrders.Add(pro);
            await _db.SaveChangesAsync(ct);
        }
        return pro;
    }

    // Delete + recreate this PRO's ops on a WC so load is deterministic across re-runs.
    private async Task ReplaceOpsAsync(
        int proId, int companyId, int locationId, int wcId, int? assetId, DateTime windowFrom,
        int opCount, decimal setupMins, decimal runMins, CancellationToken ct)
    {
        var stale = await _db.ProductionOperations
            .Where(o => o.ProductionOrderId == proId && o.WorkCenterId == wcId).ToListAsync(ct);
        if (stale.Count > 0) { _db.ProductionOperations.RemoveRange(stale); await _db.SaveChangesAsync(ct); }

        // Lay ops back-to-back from window start + 6 h so every planned span sits inside
        // the 5-day query window (fraction = 1). Working-hour flooring is R4-11's job;
        // R4-10 measures load against calendar-available hours regardless of where the
        // planned span lands.
        var cursor = windowFrom.AddHours(6);
        for (int i = 0; i < opCount; i++)
        {
            var mins = setupMins + runMins;
            var op = new ProductionOperation
            {
                ProductionOrderId = proId, WorkCenterId = wcId, AssetId = assetId,
                CompanyIdSnapshot = companyId, LocationIdSnapshot = locationId,
                SequenceNumber = (i + 1) * 10, Description = $"Op {(i + 1) * 10}",
                Status = ProductionOperationStatus.Released,
                PlannedSetupMins = setupMins, PlannedRunMins = runMins,
                PlannedStart = cursor, PlannedEnd = cursor.AddMinutes((double)mins),
                PlannedQty = 50, CreatedBy = "ResourceLoadProbe",
            };
            _db.ProductionOperations.Add(op);
            cursor = cursor.AddMinutes((double)mins);
        }
        await _db.SaveChangesAsync(ct);
    }
}
