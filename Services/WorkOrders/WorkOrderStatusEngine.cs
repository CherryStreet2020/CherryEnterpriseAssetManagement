using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.3 — Status engine implementation.
    //
    // Read path: hits the Singleton cache (no DB after warm-up).
    // Write path: validates → checks approval gate → runs guard →
    // updates WorkOrder.Status → audit-logs the transition.
    //
    // Guards are resolved by IServiceProvider keyed-service lookup
    // (DI key matches WorkOrderStatusTransition.GuardServiceName).
    // Missing guards are LOGGED + ALLOWED (developer-friendly during
    // build-up; satellites in Phase D will register the real guards).
    public class WorkOrderStatusEngine : IWorkOrderStatusEngine
    {
        private readonly AppDbContext _db;
        private readonly WorkOrderStatusCache _cache;
        private readonly IServiceProvider _services;
        private readonly ILogger<WorkOrderStatusEngine> _logger;

        public WorkOrderStatusEngine(
            AppDbContext db,
            WorkOrderStatusCache cache,
            IServiceProvider services,
            ILogger<WorkOrderStatusEngine> logger)
        {
            _db = db;
            _cache = cache;
            _services = services;
            _logger = logger;
        }

        public async Task<IReadOnlyList<TransitionOption>> GetAvailableTransitionsAsync(
            MaintenanceEvent workOrder,
            CancellationToken ct = default)
        {
            var fromStatus = (short)(int)workOrder.Status;
            var transitions = await GetTransitionsFromAsync(
                workOrder.Classification, fromStatus, ct);

            var labels = await GetAllLabelsAsync(workOrder.Classification, ct);
            var labelByCode = labels.ToDictionary(l => l.StatusCode);

            return transitions.Select(t =>
            {
                labelByCode.TryGetValue(t.ToStatusCode, out var toLabel);
                return new TransitionOption(
                    ToStatusCode: t.ToStatusCode,
                    ToStatusKey: toLabel?.StatusKey ?? $"Status{t.ToStatusCode}",
                    ActionLabel: t.ActionLabel ?? $"Move to {toLabel?.DisplayLabel ?? t.ToStatusCode.ToString()}",
                    DisplayColor: toLabel?.DisplayColor ?? "gray",
                    IsBackTransition: t.IsBackTransition,
                    RequiresApproval: !string.IsNullOrEmpty(t.RequiredApprovalStage),
                    RequiredApprovalStage: t.RequiredApprovalStage,
                    IsGuarded: !string.IsNullOrEmpty(t.GuardServiceName),
                    GuardServiceName: t.GuardServiceName,
                    DisplayOrder: t.DisplayOrder);
            }).ToList().AsReadOnly();
        }

        public async Task<TransitionResult> TryTransitionAsync(
            MaintenanceEvent workOrder,
            short toStatusCode,
            int userId,
            string? comment,
            CancellationToken ct = default)
        {
            var fromStatus = (short)(int)workOrder.Status;

            // 1. Validate the transition is allowed at all.
            var transitions = await GetTransitionsFromAsync(
                workOrder.Classification, fromStatus, ct);
            var match = transitions.FirstOrDefault(t => t.ToStatusCode == toStatusCode);
            if (match == null)
            {
                return new TransitionResult(
                    TransitionOutcome.NotAllowed,
                    null, null,
                    $"No transition configured from status {fromStatus} to {toStatusCode} for classification {workOrder.Classification}.",
                    BlockedReason: "Transition not configured.");
            }

            // 2. Approval gate.
            //
            // PR #119.4 ships the polymorphic WorkOrderApproval table that
            // this check queries. Until then: if a transition has
            // RequiredApprovalStage set, we LOG + ALLOW (developer-friendly
            // default; same posture as the missing-guard branch below).
            // PR #119.4 will replace this stub with the real lookup.
            //
            // Note: the seeder in this PR does NOT yet wire any transition
            // to RequiredApprovalStage, so this branch is dead code in
            // production until PR #119.4 lands. The structure stays here
            // so PR #119.4 is a single targeted edit.
            if (!string.IsNullOrEmpty(match.RequiredApprovalStage))
            {
                _logger.LogWarning(
                    "WorkOrderStatusEngine: transition for WO {WoId} requires approval stage '{Stage}'. " +
                    "WorkOrderApproval table doesn't exist yet (ships in PR #119.4); allowing transition.",
                    workOrder.Id, match.RequiredApprovalStage);
            }

            // 3. Optional guard.
            string? warning = null;
            if (!string.IsNullOrEmpty(match.GuardServiceName))
            {
                var guard = _services.GetKeyedService<IWorkOrderTransitionGuard>(match.GuardServiceName);
                if (guard == null)
                {
                    _logger.LogWarning(
                        "WorkOrderStatusEngine: guard '{Guard}' not registered. Allowing transition for WO {WoId} (developer-friendly default during build-up).",
                        match.GuardServiceName, workOrder.Id);
                }
                else
                {
                    var result = await guard.RunAsync(workOrder, fromStatus, toStatusCode, userId, comment, ct);
                    switch (result.Decision)
                    {
                        case GuardDecision.Block:
                            return new TransitionResult(
                                TransitionOutcome.GuardBlocked,
                                null, null,
                                $"Guard '{match.GuardServiceName}' blocked the transition.",
                                BlockedReason: result.Reason);
                        case GuardDecision.AllowWithWarning:
                            warning = result.Reason;
                            break;
                        case GuardDecision.Allow:
                        default:
                            break;
                    }
                }
            }

            // 4. Apply the new status. Caller owns SaveChangesAsync.
            workOrder.Status = (MaintenanceStatus)(int)toStatusCode;
            // Stamp closure metadata for terminal transitions.
            var labels = await GetAllLabelsAsync(workOrder.Classification, ct);
            var newLabel = labels.FirstOrDefault(l => l.StatusCode == toStatusCode);
            if (newLabel?.IsTerminal == true && workOrder.ClosedAt == null)
            {
                workOrder.ClosedAt = DateTime.UtcNow;
            }

            return new TransitionResult(
                Outcome: warning != null ? TransitionOutcome.AllowedWithWarning : TransitionOutcome.Success,
                NewStatusCode: toStatusCode,
                NewStatusKey: newLabel?.StatusKey,
                Message: warning ?? $"Moved to {newLabel?.DisplayLabel ?? toStatusCode.ToString()}",
                BlockedReason: warning);
        }

        public async Task<WorkOrderStatusLabel?> GetLabelAsync(
            WorkOrderClassification classification,
            short statusCode,
            CancellationToken ct = default)
        {
            var labels = await GetAllLabelsAsync(classification, ct);
            return labels.FirstOrDefault(l => l.StatusCode == statusCode);
        }

        public async Task<IReadOnlyList<WorkOrderStatusLabel>> GetAllLabelsAsync(
            WorkOrderClassification classification,
            CancellationToken ct = default)
        {
            if (_cache.LabelsByClassification.TryGetValue(classification, out var cached))
                return cached;

            var loaded = await _db.Set<WorkOrderStatusLabel>()
                .AsNoTracking()
                .Where(l => l.Classification == classification)
                .OrderBy(l => l.DisplayOrder)
                .ThenBy(l => l.StatusCode)
                .ToListAsync(ct);

            var ro = (IReadOnlyList<WorkOrderStatusLabel>)loaded.AsReadOnly();
            _cache.LabelsByClassification[classification] = ro;
            return ro;
        }

        public void Invalidate()
        {
            _cache.LabelsByClassification.Clear();
            _cache.TransitionsByFromStatus.Clear();
            _cache.ProfileByClassification.Clear();
            _logger.LogInformation("WorkOrderStatusEngine cache cleared.");
        }

        private async Task<IReadOnlyList<WorkOrderStatusTransition>> GetTransitionsFromAsync(
            WorkOrderClassification classification,
            short fromStatus,
            CancellationToken ct)
        {
            var key = (classification, fromStatus);
            if (_cache.TransitionsByFromStatus.TryGetValue(key, out var cached))
                return cached;

            var loaded = await _db.Set<WorkOrderStatusTransition>()
                .AsNoTracking()
                .Where(t => t.Classification == classification && t.FromStatusCode == fromStatus)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.ToStatusCode)
                .ToListAsync(ct);

            var ro = (IReadOnlyList<WorkOrderStatusTransition>)loaded.AsReadOnly();
            _cache.TransitionsByFromStatus[key] = ro;
            return ro;
        }
    }

}
