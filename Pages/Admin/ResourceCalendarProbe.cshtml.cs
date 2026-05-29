// Theme B11 Wave R2-6 — admin probe for per-resource calendars + finite-capacity.
// Lock-16 corollary: every button writes.
//
//   1) Assign calendar         — set the latest resource's CalendarId → a WorkCalendar.
//   2) Add downtime window      — a ResourceCalendarException (Downtime).
//   3) Add maintenance window   — a MaintenanceWindow exception bridged to an EAM WorkOrder.
//   4) Set capacity envelope    — batch / job-size / concurrency caps on the latest resource.
//   R) Reload
//
// Company-scoped pickers (R1-2 Codex lesson). ControlPlaneExempt (writes via AppDbContext).

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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B11 R2-6 resource calendars + finite-capacity. " +
    "Reads/writes ProductionResource + ResourceCalendarException via AppDbContext, " +
    "tenant-scoped through ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class ResourceCalendarProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ResourceCalendarProbeModel> _logger;

    public ResourceCalendarProbeModel(AppDbContext db, ITenantContext tenant, ILogger<ResourceCalendarProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int ResourceCount { get; private set; }
    public int CalendarAssignedCount { get; private set; }
    public int ExceptionCount { get; private set; }
    public int MaintenanceBridgedCount { get; private set; }
    public IReadOnlyList<ResRow> ResourceSample { get; private set; } = Array.Empty<ResRow>();
    public IReadOnlyList<ExcRow> ExceptionSample { get; private set; } = Array.Empty<ExcRow>();

    public sealed record ResRow(int Id, string Code, ResourceKind Kind, int? CalendarId,
        decimal? MaxBatchSize, decimal? MaxJobQuantity, int? MaxConcurrentJobs, decimal? AvailableHoursPerDay);
    public sealed record ExcRow(int Id, int ResourceId, ResourceCalendarExceptionType Type,
        DateTime StartUtc, DateTime EndUtc, int? SourceWorkOrderId, string? Reason);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private int CompanyId() => _tenant.VisibleCompanyIds.FirstOrDefault();

    private IQueryable<ProductionResource> ScopedRes() =>
        _db.ProductionResources.Where(r => _tenant.VisibleCompanyIds.Contains(r.CompanyId));

    private IQueryable<ResourceCalendarException> ScopedExc() =>
        _db.ResourceCalendarExceptions.Where(x => _tenant.VisibleCompanyIds.Contains(x.CompanyId));

    private Task<ProductionResource?> LatestResAsync(int companyId, CancellationToken ct) =>
        _db.ProductionResources.Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        ResourceCount = await ScopedRes().CountAsync(ct);
        CalendarAssignedCount = await ScopedRes().CountAsync(r => r.CalendarId != null, ct);
        ExceptionCount = await ScopedExc().CountAsync(ct);
        MaintenanceBridgedCount = await ScopedExc().CountAsync(x => x.SourceWorkOrderId != null, ct);

        ResourceSample = await ScopedRes()
            .OrderByDescending(r => r.Id).Take(10)
            .Select(r => new ResRow(r.Id, r.Code, r.ResourceKind, r.CalendarId,
                r.MaxBatchSize, r.MaxJobQuantity, r.MaxConcurrentJobs, r.AvailableHoursPerDay))
            .ToListAsync(ct);

        ExceptionSample = await ScopedExc()
            .OrderByDescending(x => x.Id).Take(10)
            .Select(x => new ExcRow(x.Id, x.ProductionResourceId, x.ExceptionType,
                x.StartUtc, x.EndUtc, x.SourceWorkOrderId, x.Reason))
            .ToListAsync(ct);
    }

    // 1) ASSIGN a calendar to the latest resource
    public async Task<IActionResult> OnPostAssignCalendarAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var res = await LatestResAsync(companyId, ct);
        if (res == null) { Set(false, "No ProductionResource yet — run the R2-4/R2-5 probes first."); await LoadStatsAsync(ct); return Page(); }

        // Prefer a company-owned calendar; fall back to a system calendar (CompanyId null).
        var cal = await _db.WorkCalendars
            .Where(c => c.CompanyId == companyId)
            .OrderByDescending(c => c.IsDefault).ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct)
            ?? await _db.WorkCalendars.Where(c => c.CompanyId == null)
                .OrderByDescending(c => c.IsDefault).ThenByDescending(c => c.Id)
                .FirstOrDefaultAsync(ct);
        if (cal == null) { Set(false, "No WorkCalendar available (company or system)."); await LoadStatsAsync(ct); return Page(); }

        res.CalendarId = cal.Id;
        res.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var scope = cal.CompanyId == null ? "system" : "company";
        Set(true, $"Resource #{res.Id} ({res.Code}) now overrides to calendar #{cal.Id} ({cal.Code}, {scope}). " +
                  "It schedules on its own week instead of inheriting the work-center calendar.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) ADD a downtime window on the latest resource
    public async Task<IActionResult> OnPostAddDowntimeAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var res = await LatestResAsync(companyId, ct);
        if (res == null) { Set(false, "No ProductionResource yet — run the R2-4/R2-5 probes first."); await LoadStatsAsync(ct); return Page(); }

        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(14);  // tomorrow 2pm UTC
        var exc = new ResourceCalendarException
        {
            CompanyId = companyId,
            SiteId = res.SiteId,
            ProductionResourceId = res.Id,
            ExceptionType = ResourceCalendarExceptionType.Downtime,
            StartUtc = start,
            EndUtc = start.AddHours(4),
            Reason = "Spindle bearing replacement — unplanned downtime.",
        };
        _db.ResourceCalendarExceptions.Add(exc);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Added downtime window #{exc.Id} on resource #{res.Id} ({res.Code}): {exc.StartUtc:u} → {exc.EndUtc:u}. " +
                  "The R4 scheduler will subtract this from the resource's availability.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) ADD a maintenance window bridged to an EAM WorkOrder
    public async Task<IActionResult> OnPostAddMaintenanceAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var res = await LatestResAsync(companyId, ct);
        if (res == null) { Set(false, "No ProductionResource yet — run the R2-4/R2-5 probes first."); await LoadStatsAsync(ct); return Page(); }

        // Prefer a WO against the same asset the resource bridges to; else any company WO.
        WorkOrder? wo = null;
        if (res.AssetId != null)
            wo = await _db.WorkOrders.Where(w => w.CompanyId == companyId && w.AssetId == res.AssetId)
                .OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);
        wo ??= await _db.WorkOrders.Where(w => w.CompanyId == companyId)
                .OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);

        var start = DateTime.UtcNow.Date.AddDays(7).AddHours(13);  // next week 1pm UTC
        var exc = new ResourceCalendarException
        {
            CompanyId = companyId,
            SiteId = res.SiteId,
            ProductionResourceId = res.Id,
            ExceptionType = ResourceCalendarExceptionType.MaintenanceWindow,
            StartUtc = start,
            EndUtc = start.AddHours(3),
            SourceWorkOrderId = wo?.Id,
            Reason = wo != null
                ? $"Planned PM — driven by EAM WorkOrder #{wo.Id}."
                : "Planned PM (no EAM WorkOrder in company to bridge).",
        };
        _db.ResourceCalendarExceptions.Add(exc);
        await _db.SaveChangesAsync(ct);

        var bridge = wo != null ? $"bridged to EAM WorkOrder #{wo.Id}" : "no EAM WorkOrder in company (bridge null)";
        Set(true, $"Added maintenance window #{exc.Id} on resource #{res.Id} ({res.Code}), {bridge}. " +
                  "Production and maintenance share the resource — the scheduler sees the PM without re-keying.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) SET the finite-capacity envelope on the latest resource
    public async Task<IActionResult> OnPostSetCapacityAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var res = await LatestResAsync(companyId, ct);
        if (res == null) { Set(false, "No ProductionResource yet — run the R2-4/R2-5 probes first."); await LoadStatsAsync(ct); return Page(); }

        res.AvailableHoursPerDay = 22.5m;   // a 3-shift machine with a maintenance gap
        res.MaxConcurrentJobs = res.ExclusiveUse ? 1 : 3;
        res.MinBatchSize = 1m;
        res.MaxBatchSize = 24m;             // a furnace-load ceiling
        res.MinJobQuantity = 1m;
        res.MaxJobQuantity = 500m;
        res.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        Set(true, $"Set capacity envelope on resource #{res.Id} ({res.Code}): {res.AvailableHoursPerDay}h/day, " +
                  $"batch {res.MinBatchSize}–{res.MaxBatchSize}, job {res.MinJobQuantity}–{res.MaxJobQuantity}, " +
                  $"max {res.MaxConcurrentJobs} concurrent. These bound the R4 finite scheduler.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Stats reloaded.");
        return Page();
    }
}
