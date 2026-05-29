// Theme B11 Wave R4-11 (2026-05-29) — Finite scheduler impl. Design in the interface file.
//
// Pattern (backward): children processed newest-CreatedAt-first (oldest finishes at the
// parent's ScheduledEnd, each younger child finishes where the next-older started — same
// child ordering the stub used). Within a child, ops walk DESC SequenceNumber; each op's
// PlannedEnd = the running cursor, PlannedStart = cursor minus the op's span consumed in
// WORKING time on that op's Work Center calendar. Finite-capacity contention is checked
// against SimultaneousOperationsMax; an overloaded primary triggers capability-based
// alternate selection (R3-9) then WorkCenterAlternate spill. commit:false = pure what-if.
//
// EF discipline: flat scalar projections, all calendar/interval/contention math in C#.

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

namespace Abs.FixedAssets.Services.Production.Scheduling
{
    public sealed class FiniteSchedulingService : IFiniteSchedulingService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly ICapabilityMatchService _capabilityMatch;
        private readonly ILogger<FiniteSchedulingService> _log;

        private const int HorizonDays = 180;

        private static readonly ProductionOperationStatus[] CommittedStatuses =
        {
            ProductionOperationStatus.Scheduled, ProductionOperationStatus.Released,
            ProductionOperationStatus.InSetup, ProductionOperationStatus.Running,
            ProductionOperationStatus.Paused,
        };

        public FiniteSchedulingService(AppDbContext db, ITenantContext tenant,
            ICapabilityMatchService capabilityMatch, ILogger<FiniteSchedulingService> log)
        {
            _db = db; _tenant = tenant; _capabilityMatch = capabilityMatch; _log = log;
        }

        public async Task<Result<FiniteScheduleResult>> ScheduleAsync(
            int parentProductionOrderId, ScheduleDirection direction, bool commit, CancellationToken ct = default)
        {
            var parent = await _db.ProductionOrders
                .FirstOrDefaultAsync(p => p.Id == parentProductionOrderId, ct);
            if (parent is null)
                return Result.Failure<FiniteScheduleResult>($"ProductionOrder {parentProductionOrderId} not found.");
            if (!_tenant.VisibleCompanyIds.Contains(parent.CompanyId))
                return Result.Failure<FiniteScheduleResult>("ProductionOrder is not in your tenant scope.");

            DateTime anchor;
            if (direction == ScheduleDirection.Backward)
            {
                if (parent.ScheduledEnd is null)
                    return Result.Failure<FiniteScheduleResult>(
                        $"Parent {parentProductionOrderId} has no ScheduledEnd to backward-schedule from.");
                anchor = DateTime.SpecifyKind(parent.ScheduledEnd.Value, DateTimeKind.Utc);
            }
            else
            {
                anchor = DateTime.SpecifyKind(parent.ScheduledStart ?? DateTime.UtcNow, DateTimeKind.Utc);
            }

            var children = await _db.ProductionOrders
                .Where(p => p.ParentProductionOrderId == parentProductionOrderId)
                .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                .ToListAsync(ct);

            // Orders that belong to THIS schedule run — excluded from "competing demand".
            var ownOrderIds = new HashSet<int>(children.Select(c => c.Id)) { parentProductionOrderId };

            var warnings = new List<string>();
            var placements = new List<OperationPlacement>();
            var inRun = new List<(int Wc, DateTime Start, DateTime End)>();
            var wcCache = new Dictionary<int, WcCtx>();
            var horizonFrom = anchor.AddDays(-HorizonDays);
            var horizonTo = anchor.AddDays(HorizonDays);

            if (children.Count == 0)
                return Result.Success(new FiniteScheduleResult(
                    parentProductionOrderId, direction, commit, anchor,
                    Array.Empty<int>(), placements, 0, 0, 0, 0,
                    new[] { "Parent has no child production orders — nothing to schedule." }));

            var processedChildIds = new List<int>(children.Count);
            DateTime earliest = anchor, latest = anchor;
            var orderForChildren = direction == ScheduleDirection.Backward ? children : Enumerable.Reverse(children).ToList();
            var cursor = anchor;

            foreach (var child in orderForChildren)
            {
                var ops = await _db.ProductionOperations
                    .Where(o => o.ProductionOrderId == child.Id)
                    .OrderByDescending(o => o.SequenceNumber).ThenByDescending(o => o.Id)
                    .ToListAsync(ct);
                // Forward consumes ascending sequence.
                if (direction == ScheduleDirection.Forward) ops.Reverse();

                if (ops.Count == 0)
                {
                    if (commit) { child.ScheduledStart = cursor; child.ScheduledEnd = cursor; child.ModifiedAt = DateTime.UtcNow; }
                    processedChildIds.Add(child.Id);
                    warnings.Add($"Child {child.Id} ({child.OrderNumber}) has no operations — release it first. Stamped zero-duration.");
                    continue;
                }

                var childBoundaryStart = cursor;  // backward: child end; forward: child start
                var opCursor = cursor;

                foreach (var op in ops)
                {
                    var spanMins = op.PlannedSetupMins + op.PlannedRunMins + op.PlannedQueueMins + op.PlannedMoveMins + op.PlannedWaitMins;
                    var busyMins = op.PlannedSetupMins + op.PlannedRunMins;

                    var primary = await GetWcAsync(op.WorkCenterId, parent.CompanyId, horizonFrom, horizonTo, wcCache, warnings, ct);

                    // Initial placement on the primary WC, floored into working time.
                    var (start0, end0, ranOut0) = Place(direction, primary, opCursor, spanMins);

                    int chosenWc = op.WorkCenterId;
                    int? chosenAsset = op.AssetId;
                    string chosenCode = primary.Code;
                    var start = start0; var end = end0; var ranOut = ranOut0;
                    bool moved = false; int? originalWc = null; string? altReason = null; bool onOverloaded = false;

                    if (await IsOverloadedAsync(primary, op.WorkCenterId, start, end, busyMins, ownOrderIds, inRun, ct))
                    {
                        // Each candidate is evaluated at its OWN re-floored window on its OWN calendar,
                        // so capacity is checked against the interval the op would actually occupy there.
                        var alt = await FindAlternateAsync(op, direction, opCursor, spanMins, busyMins, parent.CompanyId,
                            ownOrderIds, inRun, horizonFrom, horizonTo, wcCache, warnings, ct);
                        if (alt is not null)
                        {
                            moved = true; originalWc = op.WorkCenterId;
                            chosenWc = alt.WcId; chosenAsset = alt.AssetId; chosenCode = alt.Code; altReason = alt.Reason;
                            start = alt.Start; end = alt.End; ranOut = alt.RanOut;
                        }
                        else
                        {
                            onOverloaded = true;
                            warnings.Add($"Op {op.Id} (seq {op.SequenceNumber}) stays on overloaded WC {primary.Code} — no free capability alternate or spill WC.");
                        }
                    }

                    if (commit)
                    {
                        op.WorkCenterId = chosenWc; op.AssetId = chosenAsset;
                        op.PlannedStart = start; op.PlannedEnd = end; op.ModifiedAt = DateTime.UtcNow;
                    }

                    placements.Add(new OperationPlacement(
                        op.Id, child.Id, op.SequenceNumber, op.Description,
                        chosenWc, chosenCode, chosenAsset, start, end,
                        moved, originalWc, altReason, onOverloaded, ranOut));
                    inRun.Add((chosenWc, start, end));

                    if (start < earliest) earliest = start;
                    if (end > latest) latest = end;

                    // Advance the cursor: backward → to the op's start; forward → to its end.
                    opCursor = direction == ScheduleDirection.Backward ? start : end;
                }

                if (commit)
                {
                    if (direction == ScheduleDirection.Backward)
                    { child.ScheduledEnd = childBoundaryStart; child.ScheduledStart = opCursor; }
                    else
                    { child.ScheduledStart = childBoundaryStart; child.ScheduledEnd = opCursor; }
                    child.ModifiedAt = DateTime.UtcNow;
                }
                cursor = opCursor;
                processedChildIds.Add(child.Id);
            }

            if (commit) await _db.SaveChangesAsync(ct);

            var spannedDays = (int)Math.Floor((latest - earliest).TotalDays);
            var movedCount = placements.Count(p => p.MovedToAlternate);
            var overloadedCount = placements.Count(p => p.OnOverloadedResource);

            _log.LogInformation(
                "FiniteSchedule {Dir} PRO {Pid} commit={Commit}: {Ops} ops across {Children} children, " +
                "{Moved} re-homed to alternates, {Over} on overloaded WCs, span {Days}d.",
                direction, parentProductionOrderId, commit, placements.Count, processedChildIds.Count,
                movedCount, overloadedCount, spannedDays);

            return Result.Success(new FiniteScheduleResult(
                parentProductionOrderId, direction, commit, anchor,
                processedChildIds, placements, placements.Count, movedCount, overloadedCount,
                spannedDays, warnings));
        }

        // Place an op's span ending (backward) / starting (forward) at the cursor, floored to working time.
        private static (DateTime start, DateTime end, bool ranOut) Place(
            ScheduleDirection dir, WcCtx wc, DateTime cursor, decimal spanMins)
        {
            if (dir == ScheduleDirection.Backward)
            {
                var end = cursor;
                var start = WorkingTimeEngine.SubtractWorkingMinutes(wc.Intervals, end, spanMins, out var ranOut);
                return (start, end, ranOut);
            }
            else
            {
                var start = cursor;
                var end = WorkingTimeEngine.AddWorkingMinutes(wc.Intervals, start, spanMins, out var ranOut);
                return (start, end, ranOut);
            }
        }

        // Would placing this op on [start,end] push PEAK concurrency on the WC over its capacity?
        // (Counting total *overlapping* ops would falsely reject a long candidate that merely brushes
        // two non-concurrent jobs on a capacity-2 WC — so we sweep for the true peak instead.)
        private async Task<bool> IsOverloadedAsync(
            WcCtx wc, int wcId, DateTime start, DateTime end, decimal busyMins,
            HashSet<int> ownOrderIds, List<(int Wc, DateTime Start, DateTime End)> inRun,
            CancellationToken ct)
        {
            if (busyMins <= 0m) return false;             // a zero-duration op never contends
            if (wc.CapacityMax >= int.MaxValue) return false; // InfiniteCapacity

            // Competing intervals: OTHER orders' committed ops on this WC overlapping the window…
            var competing = await _db.ProductionOperations
                .Where(o => o.WorkCenterId == wcId
                    && !ownOrderIds.Contains(o.ProductionOrderId)
                    && o.PlannedStart != null && o.PlannedEnd != null
                    && o.PlannedEnd > start && o.PlannedStart < end
                    && (o.PlannedSetupMins + o.PlannedRunMins) > 0m
                    && CommittedStatuses.Contains(o.Status))
                .Select(o => new { S = o.PlannedStart!.Value, E = o.PlannedEnd!.Value })
                .ToListAsync(ct);

            // …plus this run's own placements on the same WC overlapping the window, plus the candidate.
            var intervals = new List<(DateTime S, DateTime E)>(competing.Count + 4);
            foreach (var c in competing) intervals.Add((c.S, c.E));
            foreach (var p in inRun) if (p.Wc == wcId && p.End > start && p.Start < end) intervals.Add((p.Start, p.End));
            intervals.Add((start, end));

            return PeakConcurrency(intervals) > wc.CapacityMax;
        }

        // Max number of intervals overlapping at any instant. Ends are exclusive: an op ending exactly
        // when another starts is NOT concurrent (process -1 before +1 at equal timestamps).
        private static int PeakConcurrency(List<(DateTime S, DateTime E)> intervals)
        {
            var events = new List<(DateTime T, int Delta)>(intervals.Count * 2);
            foreach (var iv in intervals)
            {
                if (iv.E <= iv.S) continue;
                events.Add((iv.S, 1));
                events.Add((iv.E, -1));
            }
            events.Sort((a, b) => a.T != b.T ? a.T.CompareTo(b.T) : a.Delta.CompareTo(b.Delta));
            int cur = 0, peak = 0;
            foreach (var e in events) { cur += e.Delta; if (cur > peak) peak = cur; }
            return peak;
        }

        private async Task<AltChoice?> FindAlternateAsync(
            ProductionOperation op, ScheduleDirection direction, DateTime opCursor, decimal spanMins, decimal busyMins,
            int companyId, HashSet<int> ownOrderIds, List<(int Wc, DateTime Start, DateTime End)> inRun,
            DateTime horizonFrom, DateTime horizonTo, Dictionary<int, WcCtx> wcCache, List<string> warnings,
            CancellationToken ct)
        {
            // Evaluate a candidate WC by FLOORING the op on that WC's own calendar, then checking
            // capacity at that re-floored window (a different shift/TZ/holiday moves the interval).
            async Task<AltChoice?> TryAsync(int altWc, int? assetId, string reasonPrefix)
            {
                if (altWc == op.WorkCenterId) return null;
                var altCtx = await GetWcAsync(altWc, companyId, horizonFrom, horizonTo, wcCache, warnings, ct);
                var (s, e, ranOut) = Place(direction, altCtx, opCursor, spanMins);
                if (await IsOverloadedAsync(altCtx, altWc, s, e, busyMins, ownOrderIds, inRun, ct)) return null;
                return new AltChoice(altWc, assetId, altCtx.Code, $"{reasonPrefix} {altCtx.Code}", s, e, ranOut);
            }

            // (a) Capability-based: R3-9 tells us WHO ELSE can run this op (if released from a routing op).
            if (op.RoutingOperationId is int routingOpId)
            {
                var match = await _capabilityMatch.GetEligibleResourcesAsync(routingOpId, asOfUtc: null, ct);
                if (match.IsSuccess && match.Value.Eligible.Count > 0)
                {
                    var eligibleIds = match.Value.Eligible.Select(e => e.ResourceId).ToList();
                    var resources = await _db.ProductionResources
                        .Where(r => eligibleIds.Contains(r.Id) && r.WorkCenterId != null)
                        .Select(r => new { r.Id, r.WorkCenterId, r.AssetId, r.Code })
                        .ToListAsync(ct);
                    foreach (var resId in eligibleIds) // preserve R3-9 rank order
                    {
                        var r = resources.FirstOrDefault(x => x.Id == resId);
                        if (r is null || r.WorkCenterId is not int altWc) continue;
                        var choice = await TryAsync(altWc, r.AssetId, $"capability alternate → resource {r.Code} on WC");
                        if (choice is not null) return choice;
                    }
                }
            }

            // (b) Spill: the WC's ordered WorkCenterAlternate list.
            var alternates = await _db.Set<WorkCenterAlternate>()
                .Where(a => a.WorkCenterId == op.WorkCenterId && a.IsActive)
                .OrderBy(a => a.Preference)
                .Select(a => a.AlternateWorkCenterId)
                .ToListAsync(ct);
            foreach (var altWc in alternates)
            {
                var choice = await TryAsync(altWc, null, "spill → alternate WC");
                if (choice is not null) return choice;
            }
            return null;
        }

        private sealed record AltChoice(
            int WcId, int? AssetId, string Code, string Reason, DateTime Start, DateTime End, bool RanOut);

        // Resolve + cache a WC's scheduling context (capacity + working intervals over the horizon).
        private async Task<WcCtx> GetWcAsync(
            int wcId, int companyId, DateTime horizonFrom, DateTime horizonTo,
            Dictionary<int, WcCtx> cache, List<string> warnings, CancellationToken ct)
        {
            if (cache.TryGetValue(wcId, out var hit)) return hit;

            var wc = await _db.WorkCenters
                .Where(w => w.Id == wcId)
                .Select(w => new { w.Id, w.Code, w.CalendarId, w.CapacityModel, w.SimultaneousOperationsMax, w.ParallelMachineCount })
                .FirstOrDefaultAsync(ct);

            int capacityMax;
            if (wc == null) capacityMax = 1;
            else if (wc.CapacityModel == WorkCenterCapacityModel.InfiniteCapacity) capacityMax = int.MaxValue;
            else capacityMax = Math.Max(1, wc.SimultaneousOperationsMax ?? wc.ParallelMachineCount ?? 1);

            var cal = await ResolveCalendarAsync(wc?.CalendarId, companyId, ct);
            List<WorkingTimeEngine.Interval> intervals;
            if (cal == null)
            {
                intervals = new List<WorkingTimeEngine.Interval>();
                warnings.Add($"WC {wc?.Code ?? wcId.ToString()} has no resolvable calendar — working-time flooring degraded.");
            }
            else
            {
                var tz = WorkingTimeEngine.ResolveTimeZone(cal.TimeZone, out var tzNote);
                if (tzNote != null && !warnings.Contains(tzNote)) warnings.Add(tzNote);
                var holidays = await _db.Holidays
                    .Where(h => h.WorkCalendarId == cal.Id && h.IsActive
                        && h.ObservedDate >= horizonFrom.Date.AddDays(-1) && h.ObservedDate <= horizonTo.Date.AddDays(1))
                    .Select(h => new WorkingHoliday(h.ObservedDate, h.IsHalfDay))
                    .ToListAsync(ct);
                intervals = WorkingTimeEngine.BuildWorkingIntervals(cal.Spec, holidays, tz, horizonFrom, horizonTo);
            }

            var ctx = new WcCtx(wcId, wc?.Code ?? $"WC#{wcId}", capacityMax, intervals);
            cache[wcId] = ctx;
            return ctx;
        }

        private async Task<CalRes?> ResolveCalendarAsync(int? calendarId, int companyId, CancellationToken ct)
        {
            if (calendarId != null)
            {
                var c = await _db.WorkCalendars.Where(w => w.Id == calendarId)
                    .Select(w => new CalRes(w.Id, new WorkingCalendarSpec(w.Code, w.TimeZone, w.WorkDayMask, w.WorkDayStart, w.WorkDayEnd)))
                    .FirstOrDefaultAsync(ct);
                if (c != null) return c;
            }
            return await _db.WorkCalendars
                .Where(w => w.IsActive && w.IsDefault && (w.CompanyId == companyId || w.CompanyId == null))
                .OrderByDescending(w => w.CompanyId != null).ThenBy(w => w.Id)
                .Select(w => new CalRes(w.Id, new WorkingCalendarSpec(w.Code, w.TimeZone, w.WorkDayMask, w.WorkDayStart, w.WorkDayEnd)))
                .FirstOrDefaultAsync(ct);
        }

        private sealed record WcCtx(int WcId, string Code, int CapacityMax, List<WorkingTimeEngine.Interval> Intervals);
        private sealed record CalRes(int Id, WorkingCalendarSpec Spec)
        {
            public string TimeZone => Spec.TimeZone;
        }
    }
}
