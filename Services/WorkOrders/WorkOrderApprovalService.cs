using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.4 — Approval service implementation.
    //
    // No caching: approval state changes on user action, so a stale
    // cache risks letting transitions through that should be blocked.
    // Each IsStageApprovedAsync call is a single indexed point lookup
    // (composite index on WorkOrderId + Stage from the migration), so
    // the per-request DB hit is cheap.
    public class WorkOrderApprovalService : IWorkOrderApprovalService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WorkOrderApprovalService> _logger;

        public WorkOrderApprovalService(
            AppDbContext db,
            ILogger<WorkOrderApprovalService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IReadOnlyList<WorkOrderApproval>> GetChainAsync(
            int workOrderId,
            CancellationToken ct = default)
        {
            return await _db.Set<WorkOrderApproval>()
                .AsNoTracking()
                .Where(a => a.WorkOrderId == workOrderId)
                .OrderBy(a => a.StageOrder)
                .ThenBy(a => a.Id)
                .ToListAsync(ct);
        }

        public async Task<bool> IsStageApprovedAsync(
            int workOrderId,
            string stage,
            CancellationToken ct = default)
        {
            return await _db.Set<WorkOrderApproval>()
                .AsNoTracking()
                .AnyAsync(
                    a => a.WorkOrderId == workOrderId
                      && a.Stage == stage
                      && a.Decision == WorkOrderApprovalDecision.Approved,
                    ct);
        }

        public async Task<WorkOrderApproval> AddStageAsync(
            int workOrderId,
            string stage,
            string roleRequired,
            int stageOrder,
            string? displayLabel = null,
            CancellationToken ct = default)
        {
            var row = new WorkOrderApproval
            {
                WorkOrderId = workOrderId,
                Stage = stage,
                RoleRequired = roleRequired,
                StageOrder = stageOrder,
                DisplayLabel = displayLabel,
                Decision = WorkOrderApprovalDecision.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Set<WorkOrderApproval>().Add(row);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "WorkOrderApproval: added pending stage '{Stage}' to WO {WoId} (StageOrder {Order}, Role {Role}).",
                stage, workOrderId, stageOrder, roleRequired);
            return row;
        }

        public async Task<DecisionResult> RecordDecisionAsync(
            int approvalRowId,
            WorkOrderApprovalDecision decision,
            int approverUserId,
            string? comments,
            CancellationToken ct = default)
        {
            var row = await _db.Set<WorkOrderApproval>()
                .FirstOrDefaultAsync(a => a.Id == approvalRowId, ct);
            if (row == null)
            {
                return new DecisionResult(false, $"Approval row {approvalRowId} not found.", null);
            }

            if (row.Decision != WorkOrderApprovalDecision.Pending)
            {
                return new DecisionResult(
                    false,
                    $"Approval row {approvalRowId} is already {row.Decision} (cannot re-decide).",
                    row);
            }

            if (decision == WorkOrderApprovalDecision.Rejected && string.IsNullOrWhiteSpace(comments))
            {
                return new DecisionResult(
                    false,
                    "Rejection requires a comment explaining why.",
                    row);
            }

            row.Decision = decision;
            row.ApproverUserId = approverUserId;
            row.DecisionAt = DateTime.UtcNow;
            row.Comments = comments;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "WorkOrderApproval: row {Id} on WO {WoId} stage '{Stage}' → {Decision} by user {UserId}.",
                row.Id, row.WorkOrderId, row.Stage, decision, approverUserId);
            return new DecisionResult(true, $"Decision recorded: {decision}", row);
        }
    }
}
