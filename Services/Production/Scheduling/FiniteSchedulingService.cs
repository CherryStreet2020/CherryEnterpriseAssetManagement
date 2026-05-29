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
                        var alt = await FindAlternateAsync(op, primary, start, end, busyMins, parent.CompanyId,
                            ownOrderIds, inRun, horizonFrom, horizonTo, wcCache, warnings, ct);
                        if (alt is not null)
                        {
                            moved = true; originalWc = op.WorkCenterId;
                            chosenWc = alt.Value.WcId; chosenAsset = alt.Value.AssetId; chosenCode = alt.Value.Code;
                            altReason = alt.Value.Reason;
                            var altCtx = wcCache[chosenWc];
                            (start, end, ranOut) = Place(direction, altCtx, opCursor, spanMins); // re-floor on the alternate calendar
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

        // Would placing one more busy op on this WC over [start,end] exceed its simultaneous capacity?
        private async Task<bool> IsOverloadedAsync(
            WcCtx wc, int wcId, DateTime start, DateTime end, decimal busyMins,
            HashSet<int> ownOrderIds, List<(int Wc, DateTime Start, DateTime End)> inRun,
            CancellationToken ct)
        {
            if (busyMins <= 0m) return false;             // a zero-duration op never contends
            if (wc.CapacityMax >= int.MaxValue) return false; // InfiniteCapacity

            // Competing demand from OTHER orders already persisted on this WC, overlapping the window.
            var dbCount = await _db.ProductionOperations.CountAsync(o =>
                o.WorkCenterId == wcId
                && !ownOrderIds.Contains(o.ProductionOrderId)
                && o.PlannedStart != null && o.PlannedEnd != null
                && o.PlannedEnd > start && o.PlannedStart < end
                && (o.PlannedSetupMins + o.PlannedRunMins) > 0m
                && CommittedStatuses.Contains(o.Status), ct);

            // Plus this run's own placements on the same WC overlapping the window.
            var runCount = inRun.Count(p => p.Wc == wcId && p.End > start && p.Start < end);

            return dbCount + runCount + 1 > wc.CapacityMax; // placing this op would be the (n+1)th
        }

        private async Task<(int WcId, int? AssetId, string Code, string Reason)?> FindAlternateAsync(
            ProductionOperation op, WcCtx primary, DateTime start, DateTime end, decimal busyMins,
            int companyId, HashSet<int> ownOrderIds, List<(int Wc, DateTime Start, DateTime End)> inRun,
            DateTime horizonFrom, DateTime horizonTo, Dictionary<int, WcCtx> wcCache, List<string> warnings,
            CancellationToken ct)
        {
            // (a) Capability-based: R3-9 tells us WHO ELSE can run this op (if released from a routing op).
            if (op.RoutingOperationId is int routingOpId)
            {
                var match = await _capabilityMatch.GetEligibleResourcesAsync(routingOpId, asOfUtc: null, ct);
                if (match.IsSuccess && match.Value.Eligible.Count > 0)
                {
                    var eligibleIds = match.Value.Eligible.Select(e => e.ResourceId).ToList();
                    // Map eligible resources → their WC + asset (need a WC that isn't the loaded primary).
                    var resources = await _db.ProductionResources
                        .Where(r => eligibleIds.Contains(r.Id) && r.WorkCenterId != null)
                        .Select(r => new { r.Id, r.WorkCenterId, r.AssetId, r.Code })
                        .ToListAsync(ct);
                    // Preserve R3-9 rank order.
                    foreach (var resId in eligibleIds)
                    {
                        var r = resources.FirstOrDefault(x => x.Id == resId);
                        if (r is null || r.WorkCenterId is not int altWc || altWc == op.WorkCenterId) continue;
                        var altCtx = await GetWcAsync(altWc, companyId, horizonFrom, horizonTo, wcCache, warnings, ct);
                        if (!await IsOverloadedAsync(altCtx, altWc, start, end, busyMins, ownOrderIds, inRun, ct))
                            return (altWc, r.AssetId, altCtx.Code, $"capability alternate → resource {r.Code} on WC {altCtx.Code}");
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
                var altCtx = await GetWcAsync(altWc, companyId, horizonFrom, horizonTo, wcCache, warnings, ct);
                if (!await IsOverloadedAsync(altCtx, altWc, start, end, busyMins, ownOrderIds, inRun, ct))
                    return (altWc, null, altCtx.Code, $"spill → alternate WC {altCtx.Code}");
            }
            return null;
        }

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
