// =============================================================================
// Sprint 12.8 PR #2 — IBackwardSchedulingService (stub)
//
// Backward-schedule a parent ProductionOrder's children + their operations
// from the parent's ScheduledEnd. The ABS Thursday demo for Shadi Mohaisen
// (Rolls-Royce Trent XWB engine bracket assembly — 10 PROs across two
// plants + 1 subcontract paint op) needs visible PlannedStart/End on every
// child PRO and every released ProductionOperation so the Production
// Control Center and the /Production/Walkthrough page can render a real
// timeline.
//
// THIS IS A STUB OF A FUTURE ENGINE. The Sprint 14 backward-scheduler will:
//   - Consult a multi-shift working-calendar service (skip nights / weekends /
//     plant holidays — both ABS sites observe Stat holidays)
//   - Apply capacity constraints (WorkCenter MaxConcurrentOps, Asset
//     MaxConcurrentOps)
//   - Resource-level competing demand from other PROs running at the same
//     WorkCenters in the same window
//   - Walk PredecessorOperationId / IsParallel for non-linear sequences
//   - Choose between alternate routings (Routing.IsDefault + scoring)
//   - Cross plants for inter-site moves (Mississauga → Burlington transit
//     time on the bracket assembly)
//
// None of that ships here. The stub assumes:
//   - All operations are sequential within a child PRO (walk SequenceNumber
//     descending, subtract op time from cursor).
//   - Children of one parent run sequentially in CreatedAt order (each
//     child finishes as the next-younger child starts; the LAST child
//     finishes at the parent's ScheduledEnd).
//   - 24-hour wall-clock arithmetic (no calendar). A 480-min op subtracts
//     exactly 8 hours, even if that lands at 02:00 Saturday.
//
// Trade-off — these simplifications are stated up-front so the demo can
// honestly answer "is this a real scheduler?" with "no, it's the
// foundation; the engine ships in Sprint 14." This matches the discipline
// from PR #346 (chain trace) and PR #347 (voice intent) — the system
// narrates what it doesn't know rather than fabricating.
//
// The interface signature does NOT change when Sprint 14 swaps the stub
// for the real engine. That's intentional — callers (PR #5c seeder, future
// /Production/Schedule UI, voice intent) bind to the interface, never the
// implementation.
//
// ADR: not required for the stub. ADR-028 (PR #349) already locks the
// parent-child semantic separation from MasterProductionOrderId. When the
// real engine lands in Sprint 14, an ADR for calendar + capacity rules is
// expected.
//
// Lock 17 applies — this is a code-only PR. No schema, no data, no
// Republish. The stub is exercised by tests + by the future PR #5c seeder
// call. Dev preview E2E confirms the app boots; no UI walkthrough exists
// for the stub itself (PR #5d's /Production/Walkthrough will surface the
// stamped dates once seeded data exists).
// =============================================================================

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

namespace Abs.FixedAssets.Services.Production.BackwardScheduling;

public interface IBackwardSchedulingService
{
    /// <summary>
    /// Backward-schedule a parent ProductionOrder's children + their operations
    /// from the parent's ScheduledEnd. Children must already have
    /// ProductionOperations rows (released via
    /// <see cref="IProductionOperationService.ReleaseFromRoutingAsync"/>);
    /// children with no ProductionOperations are skipped with a warning and
    /// their ScheduledStart/End are left as-is.
    /// <para>
    /// Walks each child's ProductionOperations in descending SequenceNumber
    /// order, subtracting the sum of PlannedSetupMins + PlannedRunMins +
    /// PlannedQueueMins + PlannedMoveMins + PlannedWaitMins from a cursor that
    /// starts at the child's ScheduledEnd. Each child's ScheduledEnd is set
    /// to the previous (younger) child's ScheduledStart, or the parent's
    /// ScheduledEnd for the LAST (oldest by CreatedAt) child.
    /// </para>
    /// <para>
    /// Stub semantics — see interface XML doc at the top of the file. The
    /// Sprint 14 real engine will swap the implementation without changing
    /// this signature.
    /// </para>
    /// </summary>
    /// <param name="parentProductionOrderId">
    /// Id of the parent ProductionOrder. Must exist, be in the caller's
    /// tenant scope, and have a non-null ScheduledEnd.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}"/> wrapping a <see cref="BackwardScheduleOutcome"/>
    /// on success or a string error on failure.
    /// </returns>
    Task<Result<BackwardScheduleOutcome>> BackwardScheduleAsync(
        int parentProductionOrderId,
        CancellationToken ct);
}

/// <summary>
/// Result envelope for a single BackwardScheduleAsync call. Surfaces the
/// child Ids that were stamped (in the order they were processed, OLDEST
/// CreatedAt first per the stub's sequential-children assumption), how many
/// ProductionOperation rows had PlannedStart/End stamped, and the total
/// span from the earliest stamped ChildScheduledStart to the parent's
/// ScheduledEnd in whole days (floor). Callers display this on the
/// /Production/Walkthrough page + the future Production Control Center
/// timeline view.
/// </summary>
public sealed record BackwardScheduleOutcome(
    int ParentProductionOrderId,
    IReadOnlyList<int> ChildProductionOrderIds,
    int OperationsStamped,
    int TotalSpannedDays,
    IReadOnlyList<string> Warnings);

public sealed class BackwardSchedulingService : IBackwardSchedulingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BackwardSchedulingService> _logger;

    public BackwardSchedulingService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<BackwardSchedulingService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Result<BackwardScheduleOutcome>> BackwardScheduleAsync(
        int parentProductionOrderId,
        CancellationToken ct)
    {
        var parent = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == parentProductionOrderId, ct);
        if (parent is null)
            return Result.Failure<BackwardScheduleOutcome>(
                $"ProductionOrder {parentProductionOrderId} not found.");

        // Tenant scope guard — ProductionOrder.CompanyId is the denormalized
        // tenant column (Sprint 13.5 PR #5c.2). No need to resolve via
        // Location like ProductionOperationService does — the column is
        // backfilled and CHECK >= 0.
        if (!_tenantContext.VisibleCompanyIds.Contains(parent.CompanyId))
            return Result.Failure<BackwardScheduleOutcome>(
                "ProductionOrder is not in your tenant scope.");

        if (parent.ScheduledEnd is null)
            return Result.Failure<BackwardScheduleOutcome>(
                $"Parent ProductionOrder {parentProductionOrderId} has no ScheduledEnd " +
                "to backward-schedule from. Set ScheduledEnd on the parent first.");

        // Load children once. EF will populate the Children collection as
        // a separate query — explicit and predictable. Order by CreatedAt
        // for deterministic processing. The OLDEST child by CreatedAt
        // finishes LAST (at parent.ScheduledEnd); each younger child
        // finishes when the next-older child starts. This matches the
        // intuitive demo flow where the latest sub-assembly added to the
        // plan is the first one started on the shop floor.
        var children = await _db.ProductionOrders
            .Where(p => p.ParentProductionOrderId == parentProductionOrderId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        if (children.Count == 0)
        {
            _logger.LogInformation(
                "BackwardSchedule for ProductionOrder {ParentId}: no children. No-op.",
                parentProductionOrderId);
            return Result.Success(new BackwardScheduleOutcome(
                ParentProductionOrderId: parentProductionOrderId,
                ChildProductionOrderIds: Array.Empty<int>(),
                OperationsStamped: 0,
                TotalSpannedDays: 0,
                Warnings: Array.Empty<string>()));
        }

        // Sequential cursor — starts at the parent's ScheduledEnd, walks
        // down through each child. Each child's END is the cursor's
        // current value; the cursor is then set to the child's START (so
        // the next child finishes when this one began). The OLDEST child
        // (last in the descending order — processed LAST) is the one whose
        // ScheduledEnd lands at the parent's ScheduledEnd.
        //
        // Concretely for the ABS bracket assembly (parent ScheduledEnd =
        // 2026-08-14 17:00, 9 children stacked back from there):
        //   children[0] (newest)       → end = 2026-08-14 17:00 (parent end)
        //   children[1]                → end = children[0].ScheduledStart
        //   ...
        //   children[8] (oldest, first → end = children[7].ScheduledStart
        //                in real-world
        //                process order)
        var cursor = parent.ScheduledEnd.Value;
        var processedChildIds = new List<int>(children.Count);
        var warnings = new List<string>();
        var totalOperationsStamped = 0;
        DateTime earliestStart = parent.ScheduledEnd.Value;

        foreach (var child in children)
        {
            // Load each child's ProductionOperations. We sort by
            // SequenceNumber DESCENDING because we walk backward — the
            // LAST op (highest sequence) finishes at the child's
            // ScheduledEnd, and we subtract op times to derive the
            // PlannedStart of each prior op.
            var ops = await _db.ProductionOperations
                .Where(o => o.ProductionOrderId == child.Id)
                .OrderByDescending(o => o.SequenceNumber)
                .ToListAsync(ct);

            if (ops.Count == 0)
            {
                // Child not yet released — skip with warning. The seeder
                // should release every child before calling this. Stamp
                // the child's ScheduledEnd anyway so the cursor moves and
                // subsequent (older) children get scheduled around it; use
                // zero duration so ScheduledStart == ScheduledEnd. Real
                // engine in Sprint 14 would fall back to MaterialStructure
                // lookup or refuse to schedule unreleased orders.
                child.ScheduledEnd = cursor;
                child.ScheduledStart = cursor;
                child.ModifiedAt = DateTime.UtcNow;
                processedChildIds.Add(child.Id);
                warnings.Add(
                    $"Child ProductionOrder {child.Id} ({child.OrderNumber}) has no " +
                    "ProductionOperations — release it against a Routing first. " +
                    "Schedule stamped as zero-duration placeholder.");
                continue;
            }

            var childEnd = cursor;
            // Sum the five planned time components across all of this
            // child's ops. We need the total to compute the child header
            // ScheduledStart in one shot; the per-op stamping is a second
            // pass to keep both views consistent.
            decimal totalChildMins = ops.Sum(o =>
                o.PlannedSetupMins
                + o.PlannedRunMins
                + o.PlannedQueueMins
                + o.PlannedMoveMins
                + o.PlannedWaitMins);
            var childStart = childEnd.AddMinutes(-(double)totalChildMins);

            child.ScheduledEnd = childEnd;
            child.ScheduledStart = childStart;
            child.ModifiedAt = DateTime.UtcNow;

            // Walk each op backward inside the child window. Cursor for
            // ops is the child's end; each op's PlannedEnd lands at the
            // cursor, PlannedStart = PlannedEnd - opMins, then cursor moves
            // to PlannedStart for the previous op. After the loop the
            // cursor sits at the child's earliest op start, which equals
            // child.ScheduledStart by construction (sanity check below).
            var opCursor = childEnd;
            foreach (var op in ops)
            {
                var opMins = op.PlannedSetupMins
                           + op.PlannedRunMins
                           + op.PlannedQueueMins
                           + op.PlannedMoveMins
                           + op.PlannedWaitMins;
                op.PlannedEnd = opCursor;
                op.PlannedStart = opCursor.AddMinutes(-(double)opMins);
                op.ModifiedAt = DateTime.UtcNow;
                opCursor = op.PlannedStart.Value;
                totalOperationsStamped++;
            }

            // Sanity log — opCursor should equal childStart. If it drifts
            // beyond a millisecond there's a bug in the arithmetic above.
            if (Math.Abs((opCursor - childStart).TotalSeconds) > 1)
            {
                warnings.Add(
                    $"Child {child.Id} arithmetic drift: opCursor={opCursor:o} " +
                    $"childStart={childStart:o}. This is a bug in the stub.");
                _logger.LogWarning(
                    "BackwardSchedule arithmetic drift on Child {ChildId}: " +
                    "opCursor={OpCursor:o} childStart={ChildStart:o}",
                    child.Id, opCursor, childStart);
            }

            cursor = childStart;
            if (childStart < earliestStart) earliestStart = childStart;
            processedChildIds.Add(child.Id);
        }

        await _db.SaveChangesAsync(ct);

        var totalSpan = parent.ScheduledEnd.Value - earliestStart;
        var totalSpannedDays = (int)Math.Floor(totalSpan.TotalDays);

        _logger.LogInformation(
            "BackwardSchedule for ProductionOrder {ParentId}: stamped {ChildCount} " +
            "children + {OpCount} operations spanning {Days} days from {Earliest:o} " +
            "back from parent end {ParentEnd:o}. {WarningCount} warning(s).",
            parentProductionOrderId,
            processedChildIds.Count,
            totalOperationsStamped,
            totalSpannedDays,
            earliestStart,
            parent.ScheduledEnd.Value,
            warnings.Count);

        return Result.Success(new BackwardScheduleOutcome(
            ParentProductionOrderId: parentProductionOrderId,
            ChildProductionOrderIds: processedChildIds,
            OperationsStamped: totalOperationsStamped,
            TotalSpannedDays: totalSpannedDays,
            Warnings: warnings));
    }
}
