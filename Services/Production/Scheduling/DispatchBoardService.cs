// Theme B11 Wave R4-12 (2026-05-29) — Dispatch board impl. Design in the interface file.
//
// Reads queued/active operations (joined to their order for due-date/priority context),
// groups by Work Center, and orders each group by that WC's dispatch rule — all ranking
// in C# after a flat EF projection. DispatchNext marks the top queued op InSetup.

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
    public sealed class DispatchBoardService : IDispatchBoardService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly ILogger<DispatchBoardService> _log;

        // What shows on a dispatch board: queued + in-progress. Completed/Skipped/Scrapped are done.
        private static readonly ProductionOperationStatus[] BoardStatuses =
        {
            ProductionOperationStatus.Scheduled, ProductionOperationStatus.Released,
            ProductionOperationStatus.InSetup, ProductionOperationStatus.Running,
            ProductionOperationStatus.Paused,
        };

        // Only these can be "dispatched next" (not already in setup/running).
        private static readonly ProductionOperationStatus[] QueuedStatuses =
        {
            ProductionOperationStatus.Scheduled, ProductionOperationStatus.Released,
        };

        public DispatchBoardService(AppDbContext db, ITenantContext tenant, ILogger<DispatchBoardService> log)
        {
            _db = db; _tenant = tenant; _log = log;
        }

        public async Task<Result<DispatchBoard>> GetBoardAsync(int companyId, CancellationToken ct = default)
        {
            if (!_tenant.VisibleCompanyIds.Contains(companyId))
                return Result.Failure<DispatchBoard>("Company is not in your tenant scope.");

            var now = DateTime.UtcNow;
            var rows = await QueryEntriesAsync(companyId, ct);
            if (rows.Count == 0)
                return Result.Success(new DispatchBoard(companyId, now, Array.Empty<DispatchColumn>()));

            // Resolve the dispatch rule + names for the WCs that actually have work.
            var wcIds = rows.Select(r => r.WorkCenterId).Distinct().ToList();
            var wcs = await _db.WorkCenters
                .Where(w => wcIds.Contains(w.Id))
                .Select(w => new { w.Id, w.Code, w.Name, w.DispatchRule })
                .ToListAsync(ct);
            var wcById = wcs.ToDictionary(w => w.Id);

            var columns = new List<DispatchColumn>(wcs.Count);
            foreach (var grp in rows.GroupBy(r => r.WorkCenterId))
            {
                var rule = wcById.TryGetValue(grp.Key, out var w) ? w.DispatchRule : WorkCenterDispatchRule.FirstInFirstOut;
                var ordered = OrderByRule(grp.ToList(), rule, now);
                var entries = ordered.Select((e, i) => ToEntry(e, i + 1)).ToList();
                columns.Add(new DispatchColumn(
                    grp.Key,
                    w?.Code ?? $"WC#{grp.Key}",
                    w?.Name ?? "",
                    rule,
                    entries));
            }

            // Stable, readable board order: drum-ish first by queue depth, then code.
            columns = columns.OrderByDescending(c => c.Entries.Count).ThenBy(c => c.WorkCenterCode).ToList();
            return Result.Success(new DispatchBoard(companyId, now, columns));
        }

        public async Task<Result<DispatchEntry>> DispatchNextAsync(int workCenterId, CancellationToken ct = default)
        {
            var wc = await _db.WorkCenters
                .Where(w => w.Id == workCenterId)
                .Select(w => new { w.Id, w.CompanyId, w.DispatchRule })
                .FirstOrDefaultAsync(ct);
            if (wc == null) return Result.Failure<DispatchEntry>($"Work center #{workCenterId} not found.");
            if (!_tenant.VisibleCompanyIds.Contains(wc.CompanyId))
                return Result.Failure<DispatchEntry>("Work center is not in your tenant scope.");

            var now = DateTime.UtcNow;
            var rows = (await QueryEntriesAsync(wc.CompanyId, ct))
                .Where(r => r.WorkCenterId == workCenterId && QueuedStatuses.Contains(r.Status))
                .ToList();
            if (rows.Count == 0)
                return Result.Failure<DispatchEntry>("No queued (Scheduled/Released) operation on this work center.");

            var top = OrderByRule(rows, wc.DispatchRule, now).First();

            var op = await _db.ProductionOperations.FirstOrDefaultAsync(o => o.Id == top.OpId, ct);
            if (op == null) return Result.Failure<DispatchEntry>("Operation vanished before dispatch.");
            op.Status = ProductionOperationStatus.InSetup;
            op.ModifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("DispatchNext WC {Wc}: op {Op} ({Order} seq {Seq}) → InSetup.",
                workCenterId, op.Id, top.OrderNumber, top.SequenceNumber);
            return Result.Success(ToEntry(top with { Status = ProductionOperationStatus.InSetup }, 1));
        }

        // Flat projection of every board-eligible op + its order context, tenant-scoped.
        private async Task<List<Row>> QueryEntriesAsync(int companyId, CancellationToken ct)
        {
            return await (
                from o in _db.ProductionOperations
                join po in _db.ProductionOrders on o.ProductionOrderId equals po.Id
                where o.CompanyIdSnapshot == companyId
                      && BoardStatuses.Contains(o.Status)
                select new Row(
                    o.Id, o.WorkCenterId, po.OrderNumber, o.SequenceNumber, o.Description, o.Status,
                    po.ScheduledEnd, po.PromiseDate, po.Priority,
                    o.PlannedSetupMins + o.PlannedRunMins, o.PlannedStart, o.PlannedEnd))
                .ToListAsync(ct);
        }

        // Order one work center's rows by its dispatch rule. Lower sort key = runs sooner.
        private static List<Row> OrderByRule(List<Row> rows, WorkCenterDispatchRule rule, DateTime now)
        {
            // Common tiebreak: planned start, then sequence, then op id — deterministic.
            static IOrderedEnumerable<Row> Tiebreak(IOrderedEnumerable<Row> q) =>
                q.ThenBy(r => r.PlannedStart ?? DateTime.MaxValue).ThenBy(r => r.SequenceNumber).ThenBy(r => r.OpId);

            IOrderedEnumerable<Row> primary = rule switch
            {
                WorkCenterDispatchRule.EarliestDueDate =>
                    rows.OrderBy(r => Due(r) ?? DateTime.MaxValue),
                WorkCenterDispatchRule.ShortestProcessingTime =>
                    rows.OrderBy(r => r.WorkMins),
                WorkCenterDispatchRule.HighestPriority =>
                    rows.OrderByDescending(r => r.Priority),
                WorkCenterDispatchRule.CriticalRatio =>
                    rows.OrderBy(r => CriticalRatio(r, now)),
                WorkCenterDispatchRule.MinimumSlack =>
                    rows.OrderBy(r => SlackMins(r, now)),
                _ => // FirstInFirstOut — earliest queued (planned start, else creation order via id)
                    rows.OrderBy(r => r.PlannedStart ?? DateTime.MaxValue),
            };
            return Tiebreak(primary).ToList();
        }

        private static DateTime? Due(Row r) => r.ScheduledEnd ?? r.PromiseDate;

        // Critical ratio = time remaining ÷ work remaining. <1 = behind; sort ascending (most urgent first).
        // No due date → least urgent (large ratio). No work → treat as tiny so it isn't falsely urgent.
        private static double CriticalRatio(Row r, DateTime now)
        {
            var due = Due(r);
            if (due == null) return double.MaxValue;
            var workHours = Math.Max((double)r.WorkMins / 60.0, 0.01);
            var remainingHours = (due.Value - now).TotalHours;
            return remainingHours / workHours;
        }

        // Slack = time until due − work remaining (in minutes). Least slack first.
        private static double SlackMins(Row r, DateTime now)
        {
            var due = Due(r);
            if (due == null) return double.MaxValue;
            return (due.Value - now).TotalMinutes - (double)r.WorkMins;
        }

        private static DispatchEntry ToEntry(Row r, int rank) => new(
            rank, r.OpId, r.OrderNumber, r.SequenceNumber, r.Description, r.Status,
            Due(r), r.Priority, r.WorkMins, r.PlannedStart, r.PlannedEnd);

        private sealed record Row(
            int OpId, int WorkCenterId, string OrderNumber, int SequenceNumber, string Description,
            ProductionOperationStatus Status, DateTime? ScheduledEnd, DateTime? PromiseDate, int Priority,
            decimal WorkMins, DateTime? PlannedStart, DateTime? PlannedEnd);
    }
}
