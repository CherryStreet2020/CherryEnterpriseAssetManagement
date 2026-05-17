using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.WorkOrders;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.4 — Approval chain CRUD + lookup service.
    //
    // The status engine queries IsStageApprovedAsync as part of the
    // approval-gate check. Chain creation + decision recording happens
    // through this service so audit logging + RowVersion-based optimistic
    // concurrency are centralized.
    //
    // Chain-builder logic (e.g. "create a Tier1 + Tier2 + CFO chain for
    // CIP > $500K") is intentionally NOT in this PR — that lives in a
    // future WorkOrderApprovalChainBuilder service that reads threshold
    // config tables and constructs the right chain shape. For now, chains
    // are created explicitly via AddStageAsync.
    public interface IWorkOrderApprovalService
    {
        // Returns every approval row for a WO, ordered by StageOrder.
        // Renders the approval chain panel in the UI.
        Task<IReadOnlyList<WorkOrderApproval>> GetChainAsync(
            int workOrderId,
            CancellationToken ct = default);

        // The status engine's hot path. Returns true iff the WO has an
        // approval row with Stage == stage AND Decision == Approved.
        // Skipped does NOT count as approved (skipping a stage moves
        // the workflow forward but doesn't satisfy approval gates that
        // explicitly require that stage).
        Task<bool> IsStageApprovedAsync(
            int workOrderId,
            string stage,
            CancellationToken ct = default);

        // Append a new pending stage to a WO's chain. Used by chain
        // builders + the admin "add ad-hoc approval" action.
        Task<WorkOrderApproval> AddStageAsync(
            int workOrderId,
            string stage,
            string roleRequired,
            int stageOrder,
            string? displayLabel = null,
            CancellationToken ct = default);

        // Record a decision (Approved / Rejected / Skipped). Stamps
        // DecisionAt + ApproverUserId. Caller is responsible for
        // SaveChangesAsync.
        Task<DecisionResult> RecordDecisionAsync(
            int approvalRowId,
            WorkOrderApprovalDecision decision,
            int approverUserId,
            string? comments,
            CancellationToken ct = default);
    }

    public sealed record DecisionResult(
        bool Success,
        string Message,
        WorkOrderApproval? Updated);
}
