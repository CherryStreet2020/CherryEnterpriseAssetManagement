// Theme B11 Wave R4-10 (2026-05-29) — Resource load profile + calendar engine (impl).
//
// Design notes live in IResourceLoadService.cs. Implementation shape:
//   1) Resolve the target (WC or resource) → its CompanyId (tenant guard), the
//      calendar that governs it, its utilization / available-hours cap, and the
//      key the committed ops join on (WorkCenterId, or the resource's AssetId).
//   2) Calendar engine (pure, in-memory): WorkCalendar week mask + work-day window
//      (in the calendar's IANA TZ) → base working INTERVALS in [from,to]; subtract
//      Holidays; for a resource, subtract Downtime/Maintenance/Holiday exceptions,
//      union ExtraShift, scale ReducedCapacity. Sum → raw available hours. Trim by
//      UtilizationPct; floor by AvailableHoursPerDay × working-days if set.
//   3) Committed busy hours = Σ over released ops overlapping the window of
//      (PlannedSetup+PlannedRun) mins, prorated by the overlap fraction of the op's
//      planned span. Load% = committed ÷ available × 100.
//
// EF-translatability: every query projects flat scalars and materializes BEFORE any
// TimeZoneInfo / interval math runs (the calendar engine is pure C#). No method
// calls leak into a server-side .Select (the R3-8 decimal?.ToString() trap).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public sealed class ResourceLoadService : IResourceLoadService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly ILogger<ResourceLoadService> _log;

        // Sentinel Load% when there is committed work but zero available capacity in the
        // window (division would be undefined) — a loud signal, not a real percentage.
        private const decimal OverCommittedNoCapacitySentinel = 9999.9m;

        // Operations that occupy capacity. Completed/Skipped/Scrapped no longer compete.
        private static readonly ProductionOperationStatus[] CommittedStatuses =
        {
            ProductionOperationStatus.Scheduled,
            ProductionOperationStatus.Released,
            ProductionOperationStatus.InSetup,
            ProductionOperationStatus.Running,
            ProductionOperationStatus.Paused,
        };

        public ResourceLoadService(AppDbContext db, ITenantContext tenant, ILogger<ResourceLoadService> log)
        {
            _db = db; _tenant = tenant; _log = log;
        }

        // ── Public: single target ───────────────────────────────────────────────
        public async Task<Result<ResourceLoadProfile>> GetProjectedLoadAsync(
            ResourceLoadTargetKind kind, int targetId,
            DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        {
            if (toUtc <= fromUtc)
                return Result.Failure<ResourceLoadProfile>("Window end must be after window start.");

            var descriptor = await ResolveTargetAsync(kind, targetId, ct);
            if (descriptor == null)
                return Result.Failure<ResourceLoadProfile>($"{kind} #{targetId} not found.");
            if (!_tenant.VisibleCompanyIds.Contains(descriptor.CompanyId))
                return Result.Failure<ResourceLoadProfile>($"{kind} #{targetId} is not in your tenant scope.");

            var cal = await ResolveCalendarAsync(descriptor.CalendarId, descriptor.JoinWorkCenterId, descriptor.CompanyId, ct);
            var holidays = cal == null ? new List<HolidaySpan>() : await LoadHolidaysAsync(cal.Id, fromUtc, toUtc, ct);
            var exceptions = kind == ResourceLoadTargetKind.Resource
                ? await LoadResourceExceptionsAsync(targetId, fromUtc, toUtc, ct)
                : new List<ExceptionSpan>();
            var committed = await LoadCommittedOpsAsync(kind, descriptor, fromUtc, toUtc, ct);

            var profile = BuildProfile(kind, descriptor, cal, holidays, exceptions, committed, fromUtc, toUtc);
            return Result.Success(profile);
        }

        // ── Public: plant-wide + drum ────────────────────────────────────────────
        public async Task<Result<PlantLoadProfile>> GetPlantLoadAsync(
            int companyId, ResourceLoadTargetKind kind,
            DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        {
            if (toUtc <= fromUtc)
                return Result.Failure<PlantLoadProfile>("Window end must be after window start.");
            if (!_tenant.VisibleCompanyIds.Contains(companyId))
                return Result.Failure<PlantLoadProfile>("Company is not in your tenant scope.");

            // Enumerate active targets in the company.
            List<TargetDescriptor> targets;
            if (kind == ResourceLoadTargetKind.WorkCenter)
            {
                targets = await _db.WorkCenters
                    .Where(w => w.CompanyId == companyId && w.IsActive)
                    .Select(w => new TargetDescriptor(
                        w.Id, w.Code, w.Name, w.CompanyId, w.CalendarId,
                        w.UtilizationPct, null, w.Id, null))
                    .ToListAsync(ct);
            }
            else
            {
                targets = await _db.ProductionResources
                    .Where(r => r.CompanyId == companyId && r.Status == ProductionResourceStatus.Active)
                    .Select(r => new TargetDescriptor(
                        r.Id, r.Code, r.Name, r.CompanyId, r.CalendarId,
                        r.UtilizationPct, r.AvailableHoursPerDay, r.WorkCenterId, r.AssetId))
                    .ToListAsync(ct);
            }

            var profiles = new List<ResourceLoadProfile>(targets.Count);
            foreach (var d in targets)
            {
                var cal = await ResolveCalendarAsync(d.CalendarId, d.JoinWorkCenterId, companyId, ct);
                var holidays = cal == null ? new List<HolidaySpan>() : await LoadHolidaysAsync(cal.Id, fromUtc, toUtc, ct);
                var exceptions = kind == ResourceLoadTargetKind.Resource
                    ? await LoadResourceExceptionsAsync(d.Id, fromUtc, toUtc, ct)
                    : new List<ExceptionSpan>();
                var committed = await LoadCommittedOpsAsync(kind, d, fromUtc, toUtc, ct);
                profiles.Add(BuildProfile(kind, d, cal, holidays, exceptions, committed, fromUtc, toUtc));
            }

            var ranked = profiles.OrderByDescending(p => p.LoadPct).ThenBy(p => p.Code).ToList();
            // The drum is the highest-loaded target that actually carries committed work.
            var drum = ranked.FirstOrDefault(p => p.CommittedHours > 0m);

            return Result.Success(new PlantLoadProfile(
                companyId, kind, fromUtc, toUtc, ranked,
                drum?.TargetId, drum?.Code, drum?.LoadPct ?? 0m));
        }

        // ── Target resolution ────────────────────────────────────────────────────
        private async Task<TargetDescriptor?> ResolveTargetAsync(
            ResourceLoadTargetKind kind, int targetId, CancellationToken ct)
        {
            if (kind == ResourceLoadTargetKind.WorkCenter)
            {
                return await _db.WorkCenters
                    .Where(w => w.Id == targetId)
                    .Select(w => new TargetDescriptor(
                        w.Id, w.Code, w.Name, w.CompanyId, w.CalendarId,
                        w.UtilizationPct, null, w.Id, null))
                    .FirstOrDefaultAsync(ct);
            }
            return await _db.ProductionResources
                .Where(r => r.Id == targetId)
                .Select(r => new TargetDescriptor(
                    r.Id, r.Code, r.Name, r.CompanyId, r.CalendarId,
                    r.UtilizationPct, r.AvailableHoursPerDay, r.WorkCenterId, r.AssetId))
                .FirstOrDefaultAsync(ct);
        }

        private async Task<CalendarInfo?> ResolveCalendarAsync(
            int? calendarId, int? workCenterId, int companyId, CancellationToken ct)
        {
            // Resolution chain: explicit calendar (resource override or WC's own) →
            // the assigned WC's calendar (a resource with no CalendarId inherits its WC's,
            // per ProductionResource.CalendarId doc) → company default → system default.
            if (calendarId == null && workCenterId != null)
            {
                calendarId = await _db.WorkCenters
                    .Where(w => w.Id == workCenterId)
                    .Select(w => w.CalendarId)
                    .FirstOrDefaultAsync(ct);
            }
            if (calendarId != null)
            {
                var c = await _db.WorkCalendars
                    .Where(w => w.Id == calendarId)
                    .Select(w => new CalendarInfo(w.Id, w.Code, w.TimeZone, w.WorkDayMask, w.WorkDayStart, w.WorkDayEnd))
                    .FirstOrDefaultAsync(ct);
                if (c != null) return c;
            }
            return await _db.WorkCalendars
                .Where(w => w.IsActive && w.IsDefault && (w.CompanyId == companyId || w.CompanyId == null))
                .OrderByDescending(w => w.CompanyId != null) // prefer the company-specific default over the system one
                .ThenBy(w => w.Id)                            // deterministic if two defaults exist (data-hygiene safety)
                .Select(w => new CalendarInfo(w.Id, w.Code, w.TimeZone, w.WorkDayMask, w.WorkDayStart, w.WorkDayEnd))
                .FirstOrDefaultAsync(ct);
        }

        private async Task<List<HolidaySpan>> LoadHolidaysAsync(int calendarId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            // Pull a generous date band (the work-window TZ shift is < 1 day) and filter precisely in C#.
            var fromDate = fromUtc.Date.AddDays(-1);
            var toDate = toUtc.Date.AddDays(1);
            return await _db.Holidays
                .Where(h => h.WorkCalendarId == calendarId && h.IsActive
                    && h.ObservedDate >= fromDate && h.ObservedDate <= toDate)
                .Select(h => new HolidaySpan(h.ObservedDate, h.IsHalfDay))
                .ToListAsync(ct);
        }

        private async Task<List<ExceptionSpan>> LoadResourceExceptionsAsync(int resourceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await _db.ResourceCalendarExceptions
                .Where(e => e.ProductionResourceId == resourceId && e.EndUtc > fromUtc && e.StartUtc < toUtc)
                .Select(e => new ExceptionSpan(e.ExceptionType, e.StartUtc, e.EndUtc, e.CapacityOverridePct))
                .ToListAsync(ct);
        }

        private async Task<List<CommittedOp>> LoadCommittedOpsAsync(
            ResourceLoadTargetKind kind, TargetDescriptor d, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            // Resource load only resolves for machine resources bridged to an Asset
            // (ProductionOperation carries AssetId, not ProductionResourceId — R4-11 will
            // assign resources to ops). A non-machine resource returns no committed ops.
            if (kind == ResourceLoadTargetKind.Resource && d.AssetId == null)
                return new List<CommittedOp>();

            var q = _db.ProductionOperations.Where(o =>
                o.PlannedStart != null && o.PlannedEnd != null
                && o.PlannedEnd > fromUtc && o.PlannedStart < toUtc
                && CommittedStatuses.Contains(o.Status));

            q = kind == ResourceLoadTargetKind.WorkCenter
                ? q.Where(o => o.WorkCenterId == d.JoinWorkCenterId)
                : q.Where(o => o.AssetId == d.AssetId);

            return await q
                .Select(o => new CommittedOp(
                    o.Id, o.PlannedSetupMins, o.PlannedRunMins, o.PlannedStart!.Value, o.PlannedEnd!.Value))
                .ToListAsync(ct);
        }

        // ── Profile build (pure) ─────────────────────────────────────────────────
        private ResourceLoadProfile BuildProfile(
            ResourceLoadTargetKind kind, TargetDescriptor d, CalendarInfo? cal,
            List<HolidaySpan> holidays, List<ExceptionSpan> exceptions, List<CommittedOp> committed,
            DateTime fromUtc, DateTime toUtc)
        {
            var notes = new List<string>();

            decimal availableHours;
            decimal workingDays = 0m;
            if (cal == null)
            {
                availableHours = 0m;
                notes.Add("No calendar resolved (no resource/WC/company/system default) — available hours = 0.");
            }
            else
            {
                var tz = ResolveTimeZone(cal.TimeZone, notes);
                var baseIntervals = BaseWorkingIntervals(cal, holidays, tz, fromUtc, toUtc, out workingDays);

                // Precedence (documented): (1) subtract Downtime/Maintenance/resource-Holiday
                // from the BASE shift; (2) ReducedCapacity scales the NORMAL shift only;
                // (3) ExtraShift adds availability the base/holiday rules don't claw back.
                foreach (var ex in exceptions)
                {
                    if (ex.Type is ResourceCalendarExceptionType.Downtime
                        or ResourceCalendarExceptionType.MaintenanceWindow
                        or ResourceCalendarExceptionType.Holiday)
                        baseIntervals = Interval.Subtract(baseIntervals, ex.StartUtc, ex.EndUtc);
                }

                decimal reduction = 0m;
                foreach (var ex in exceptions.Where(e => e.Type == ResourceCalendarExceptionType.ReducedCapacity))
                {
                    var overlap = Interval.OverlapHours(baseIntervals, ex.StartUtc, ex.EndUtc);
                    var pct = Math.Clamp(ex.CapacityOverridePct ?? 100m, 0m, 100m);
                    reduction += overlap * (1m - pct / 100m);
                }

                var intervals = baseIntervals;
                foreach (var ex in exceptions)
                {
                    if (ex.Type == ResourceCalendarExceptionType.ExtraShift)
                        intervals = Interval.Union(intervals, Clip(ex.StartUtc, ex.EndUtc, fromUtc, toUtc));
                }

                decimal rawHours = Interval.TotalHours(intervals) - reduction;
                if (reduction > 0m) notes.Add($"Reduced-capacity windows trimmed {reduction:0.##} h.");

                // UtilizationPct trims to expected-available; AvailableHoursPerDay caps per working day.
                var util = d.UtilizationPct <= 0m ? 100m : d.UtilizationPct;
                availableHours = rawHours * (util / 100m);
                if (util != 100m) notes.Add($"Utilization {util:0.#}% applied.");

                if (d.AvailableHoursPerDay is decimal cap && cap > 0m && workingDays > 0m)
                {
                    var capTotal = cap * workingDays;
                    if (capTotal < availableHours)
                    {
                        availableHours = capTotal;
                        notes.Add($"Capped at {cap:0.##} h/day × {workingDays:0.#} working day(s).");
                    }
                }
            }

            // Committed busy hours = (setup+run) prorated by the overlap fraction of the op's planned span.
            decimal committedHours = 0m;
            foreach (var op in committed)
            {
                var busyMins = op.SetupMins + op.RunMins;
                if (busyMins <= 0m) continue;
                var span = (decimal)(op.PlannedEnd - op.PlannedStart).TotalMinutes;
                decimal fraction = 1m;
                if (span > 0m)
                {
                    var os = op.PlannedStart > fromUtc ? op.PlannedStart : fromUtc;
                    var oe = op.PlannedEnd < toUtc ? op.PlannedEnd : toUtc;
                    var overlapMins = (decimal)(oe - os).TotalMinutes;
                    fraction = Math.Clamp(overlapMins / span, 0m, 1m);
                }
                committedHours += busyMins / 60m * fraction;
            }

            decimal loadPct = availableHours > 0m
                ? Math.Round(committedHours / availableHours * 100m, 1)
                : (committedHours > 0m ? OverCommittedNoCapacitySentinel : 0m); // don't divide by zero
            if (availableHours <= 0m && committedHours > 0m)
                notes.Add("Committed work but zero available capacity in the window.");
            if (kind == ResourceLoadTargetKind.Resource && d.AssetId == null)
                notes.Add("Resource has no Asset bridge — op-level resource assignment lands in R4-11; committed load shown as 0.");

            return new ResourceLoadProfile(
                kind, d.Id, d.Code, d.Name, fromUtc, toUtc,
                Math.Round(availableHours, 2), Math.Round(committedHours, 2), loadPct,
                committed.Count, cal?.Code, notes.Count == 0 ? null : string.Join(" ", notes));
        }

        // ── Calendar engine ──────────────────────────────────────────────────────
        private static TimeZoneInfo ResolveTimeZone(string ianaId, List<string> notes)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
            catch
            {
                notes.Add($"Time zone '{ianaId}' not found — using UTC.");
                return TimeZoneInfo.Utc;
            }
        }

        /// <summary>
        /// The base working INTERVALS (UTC) inside [from,to] for a calendar: each
        /// working weekday (per WorkDayMask, bit 0 = Sunday) contributes its
        /// WorkDayStart..WorkDayEnd window in the calendar's local time (converted to
        /// UTC, clipped to the query window). Full holidays drop the day; half-day
        /// holidays halve it. <paramref name="workingDays"/> counts days that contributed.
        /// </summary>
        private static List<Interval> BaseWorkingIntervals(
            CalendarInfo cal, List<HolidaySpan> holidays, TimeZoneInfo tz,
            DateTime fromUtc, DateTime toUtc, out decimal workingDays)
        {
            workingDays = 0m;
            var result = new List<Interval>();
            var halfDays = holidays.Where(h => h.IsHalfDay).Select(h => h.Date.Date).ToHashSet();
            var fullDays = holidays.Where(h => !h.IsHalfDay).Select(h => h.Date.Date).ToHashSet();

            // Iterate local calendar days spanning the (TZ-shifted) window, with a day of slack each side.
            var localFrom = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, tz).Date.AddDays(-1);
            var localTo = TimeZoneInfo.ConvertTimeFromUtc(toUtc, tz).Date.AddDays(1);

            for (var day = localFrom; day <= localTo; day = day.AddDays(1))
            {
                var bit = 1 << (int)day.DayOfWeek; // Sunday=0 → bit 0, matches WorkDayMask doc
                if ((cal.WorkDayMask & bit) == 0) continue;     // not a working weekday
                if (fullDays.Contains(day.Date)) continue;       // full holiday

                var startLocal = day + cal.WorkDayStart;
                var endLocal = day + cal.WorkDayEnd;
                if (endLocal <= startLocal) continue;
                var isHalf = halfDays.Contains(day.Date);
                if (isHalf)
                    endLocal = startLocal + TimeSpan.FromTicks((endLocal - startLocal).Ticks / 2);

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), tz);

                var clipped = Clip(startUtc, endUtc, fromUtc, toUtc);
                if (clipped != null)
                {
                    result.Add(clipped.Value);
                    // Half-day holidays contribute half a cap-day to the AvailableHoursPerDay cap.
                    workingDays += isHalf ? 0.5m : 1m;
                }
            }
            return result;
        }

        private static Interval? Clip(DateTime s, DateTime e, DateTime fromUtc, DateTime toUtc)
        {
            var cs = s > fromUtc ? s : fromUtc;
            var ce = e < toUtc ? e : toUtc;
            return ce > cs ? new Interval(cs, ce) : (Interval?)null;
        }

        // ── Private value types / projections ────────────────────────────────────
        private readonly record struct Interval(DateTime Start, DateTime End)
        {
            public static decimal TotalHours(List<Interval> xs) =>
                xs.Aggregate(0m, (acc, i) => acc + (decimal)(i.End - i.Start).TotalHours);

            public static List<Interval> Union(List<Interval> xs, Interval? add)
            {
                if (add == null) return xs;
                var all = new List<Interval>(xs) { add.Value };
                all.Sort((a, b) => a.Start.CompareTo(b.Start));
                var merged = new List<Interval>();
                foreach (var i in all)
                {
                    if (merged.Count > 0 && i.Start <= merged[^1].End)
                    {
                        var last = merged[^1];
                        if (i.End > last.End) merged[^1] = last with { End = i.End };
                    }
                    else merged.Add(i);
                }
                return merged;
            }

            public static List<Interval> Subtract(List<Interval> xs, DateTime s, DateTime e)
            {
                var result = new List<Interval>();
                foreach (var i in xs)
                {
                    if (e <= i.Start || s >= i.End) { result.Add(i); continue; }   // no overlap
                    if (s > i.Start) result.Add(new Interval(i.Start, s));         // left remainder
                    if (e < i.End) result.Add(new Interval(e, i.End));             // right remainder
                }
                return result;
            }

            public static decimal OverlapHours(List<Interval> xs, DateTime s, DateTime e)
            {
                decimal h = 0m;
                foreach (var i in xs)
                {
                    var os = i.Start > s ? i.Start : s;
                    var oe = i.End < e ? i.End : e;
                    if (oe > os) h += (decimal)(oe - os).TotalHours;
                }
                return h;
            }
        }

        private sealed record TargetDescriptor(
            int Id, string Code, string Name, int CompanyId, int? CalendarId,
            decimal UtilizationPct, decimal? AvailableHoursPerDay, int? JoinWorkCenterId, int? AssetId);

        private sealed record CalendarInfo(
            int Id, string Code, string TimeZone, short WorkDayMask, TimeSpan WorkDayStart, TimeSpan WorkDayEnd);

        private sealed record HolidaySpan(DateTime Date, bool IsHalfDay);

        private sealed record ExceptionSpan(
            ResourceCalendarExceptionType Type, DateTime StartUtc, DateTime EndUtc, decimal? CapacityOverridePct);

        private sealed record CommittedOp(
            int Id, decimal SetupMins, decimal RunMins, DateTime PlannedStart, DateTime PlannedEnd);
    }
}
