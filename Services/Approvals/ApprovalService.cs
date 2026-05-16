using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Approvals
{
    // Sprint 2 PR #115 — Approval Hierarchy + SoD.
    //
    // Single-step, N-of-M-from-role enforcement on top of the existing
    // ApprovalWorkflow seed (PO_STD/PO_HIGH/WO_STD/WO_HIGH/DISP/TRANS etc.).
    // Sequential multi-step chain (Manager → Director → CFO) is a Sprint 2.1
    // extension on top of an ApprovalWorkflowSteps child table.
    //
    // RULES this service enforces:
    //   1. Resolve workflow:  pick the highest-threshold matching workflow
    //      for (WorkflowType, amount). If none match, doc is auto-approved
    //      (single signature; rule mirrors existing OnPostApproveAsync).
    //   2. SoD:  the user attempting to approve must NOT be the document's
    //      creator. We accept a creatorUserId parameter so each caller
    //      (PO / VendorInvoice / WO) supplies its own creator pointer.
    //   3. Role gate:  user must have at least one role that appears in
    //      workflow.ApproverRoles (CSV).
    //   4. No double-approve:  same user cannot record two Approve actions
    //      against the same target.
    //   5. N-of-M:  after recording the Approve, the count of unique-user
    //      Approve actions is compared to workflow.RequiredApprovals.
    //      Below threshold => "Pending". At or above => "FullyApproved".
    //   6. Reject:  any single Reject decision immediately produces
    //      "Rejected" state and prevents further approvals.
    //
    // Every decision is recorded as one ApprovalAction row (immutable log)
    // and one AuditLog row (defense in depth).
    public enum ApprovalOutcome
    {
        NoWorkflowApplicable = 0,   // amount below all thresholds — auto-approve
        Pending = 1,                 // decision recorded; more approvals needed
        FullyApproved = 2,           // RequiredApprovals reached
        Rejected = 3,                // someone rejected — terminal
        SodViolation = 4,            // attempted approver is the creator
        DuplicateApprover = 5,       // user already approved this target
        InsufficientRole = 6         // user not in workflow.ApproverRoles
    }

    public sealed record ApprovalDecisionResult(
        ApprovalOutcome Outcome,
        ApprovalWorkflow? Workflow,
        int ApprovalsRecorded,
        int ApprovalsRequired,
        IReadOnlyList<ApprovalAction> History,
        string? ErrorMessage);

    public sealed record PendingApprovalRow(
        string TargetEntityType,
        int TargetEntityId,
        string Title,           // human-readable, e.g. "PO-2026-00042"
        string Subtitle,        // e.g. "Vendor A · $25,000.00 · 2026-01-15"
        decimal Amount,
        int ApprovalsRecorded,
        int ApprovalsRequired,
        string WorkflowName,
        string DetailUrl);

    public interface IApprovalService
    {
        Task<ApprovalWorkflow?> ResolveWorkflowAsync(WorkflowType type, decimal amount);

        Task<ApprovalDecisionResult> RecordDecisionAsync(
            string targetEntityType,
            int targetEntityId,
            WorkflowType workflowType,
            decimal amount,
            ApprovalDecision decision,
            string approverUserId,
            string approverUsername,
            IEnumerable<string> approverRoles,
            string? creatorUserId,
            string? comment,
            int? companyId);

        Task<ApprovalDecisionResult> GetStatusAsync(
            string targetEntityType,
            int targetEntityId,
            WorkflowType workflowType,
            decimal amount);

        Task<IReadOnlyList<PendingApprovalRow>> GetPendingForUserAsync(
            string userId,
            IEnumerable<string> userRoles,
            int? companyIdFilter);
    }

    public class ApprovalService : IApprovalService
    {
        private readonly AppDbContext _db;
        private readonly AuditService _audit;
        private readonly ILogger<ApprovalService> _logger;

        public ApprovalService(AppDbContext db, AuditService audit, ILogger<ApprovalService> logger)
        {
            _db = db;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApprovalWorkflow?> ResolveWorkflowAsync(WorkflowType type, decimal amount)
        {
            // Pick the highest-threshold ACTIVE workflow for this doc-type
            // whose ThresholdAmount <= amount. If no row matches and the
            // existing seed has AutoApproveIfBelowThreshold = true, the
            // caller should treat that as "auto-approve, no chain."
            return await _db.ApprovalWorkflows
                .Where(w => w.IsActive && w.Type == type && w.ThresholdAmount <= amount)
                .OrderByDescending(w => w.ThresholdAmount)
                .ThenByDescending(w => w.RequiredApprovals)
                .FirstOrDefaultAsync();
        }

        public async Task<ApprovalDecisionResult> RecordDecisionAsync(
            string targetEntityType,
            int targetEntityId,
            WorkflowType workflowType,
            decimal amount,
            ApprovalDecision decision,
            string approverUserId,
            string approverUsername,
            IEnumerable<string> approverRoles,
            string? creatorUserId,
            string? comment,
            int? companyId)
        {
            var workflow = await ResolveWorkflowAsync(workflowType, amount);
            var existing = await GetHistoryAsync(targetEntityType, targetEntityId);

            // No applicable workflow — caller can auto-approve. We still
            // record a "no-workflow" Approve action for the audit trail
            // when the caller asks us to (avoid the case-by-case in callers).
            if (workflow == null)
            {
                if (decision == ApprovalDecision.Approved)
                {
                    var act = await PersistActionAsync(
                        targetEntityType, targetEntityId, null, 1,
                        decision, approverUserId, approverUsername,
                        approverRoles.FirstOrDefault(), comment, companyId,
                        existing);
                    return new ApprovalDecisionResult(
                        Outcome: ApprovalOutcome.NoWorkflowApplicable,
                        Workflow: null,
                        ApprovalsRecorded: 1,
                        ApprovalsRequired: 1,
                        History: existing.Append(act).ToList(),
                        ErrorMessage: null);
                }
                // Reject without a workflow — still record it.
                var rejAct = await PersistActionAsync(
                    targetEntityType, targetEntityId, null, 1,
                    decision, approverUserId, approverUsername,
                    approverRoles.FirstOrDefault(), comment, companyId,
                    existing);
                return new ApprovalDecisionResult(
                    Outcome: ApprovalOutcome.Rejected,
                    Workflow: null,
                    ApprovalsRecorded: 0,
                    ApprovalsRequired: 1,
                    History: existing.Append(rejAct).ToList(),
                    ErrorMessage: null);
            }

            // RULE 2: SoD — creator cannot approve their own doc.
            if (!string.IsNullOrEmpty(creatorUserId)
                && string.Equals(creatorUserId, approverUserId, StringComparison.OrdinalIgnoreCase))
            {
                return new ApprovalDecisionResult(
                    Outcome: ApprovalOutcome.SodViolation,
                    Workflow: workflow,
                    ApprovalsRecorded: existing.Count(a => a.Decision == ApprovalDecision.Approved),
                    ApprovalsRequired: workflow.RequiredApprovals,
                    History: existing,
                    ErrorMessage: "Segregation of duties: the document creator cannot approve their own document.");
            }

            // RULE 3: role gate.
            var allowedRoles = SplitCsv(workflow.ApproverRoles);
            if (allowedRoles.Count > 0
                && !allowedRoles.Any(r => approverRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                return new ApprovalDecisionResult(
                    Outcome: ApprovalOutcome.InsufficientRole,
                    Workflow: workflow,
                    ApprovalsRecorded: existing.Count(a => a.Decision == ApprovalDecision.Approved),
                    ApprovalsRequired: workflow.RequiredApprovals,
                    History: existing,
                    ErrorMessage: $"Your role is not in this workflow's approver list. Required: {string.Join(", ", allowedRoles)}.");
            }

            // RULE 6: a prior Reject is terminal.
            if (existing.Any(a => a.Decision == ApprovalDecision.Rejected))
            {
                return new ApprovalDecisionResult(
                    Outcome: ApprovalOutcome.Rejected,
                    Workflow: workflow,
                    ApprovalsRecorded: existing.Count(a => a.Decision == ApprovalDecision.Approved),
                    ApprovalsRequired: workflow.RequiredApprovals,
                    History: existing,
                    ErrorMessage: "This document was already rejected. Re-submit to start a new approval chain.");
            }

            // RULE 4: no double approve.
            if (decision == ApprovalDecision.Approved
                && existing.Any(a => a.Decision == ApprovalDecision.Approved
                                  && string.Equals(a.DecidedByUserId, approverUserId, StringComparison.OrdinalIgnoreCase)))
            {
                return new ApprovalDecisionResult(
                    Outcome: ApprovalOutcome.DuplicateApprover,
                    Workflow: workflow,
                    ApprovalsRecorded: existing.Count(a => a.Decision == ApprovalDecision.Approved),
                    ApprovalsRequired: workflow.RequiredApprovals,
                    History: existing,
                    ErrorMessage: "You have already approved this document. The workflow needs a different approver.");
            }

            // All gates passed — persist the action.
            var matchingRole = allowedRoles
                .FirstOrDefault(r => approverRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
            var action = await PersistActionAsync(
                targetEntityType, targetEntityId, workflow.Id, 1,
                decision, approverUserId, approverUsername,
                matchingRole, comment, companyId, existing);

            var updatedHistory = existing.Append(action).ToList();
            var approvalCount = updatedHistory.Count(a => a.Decision == ApprovalDecision.Approved);

            if (decision == ApprovalDecision.Rejected)
            {
                return new ApprovalDecisionResult(
                    Outcome: ApprovalOutcome.Rejected,
                    Workflow: workflow,
                    ApprovalsRecorded: approvalCount,
                    ApprovalsRequired: workflow.RequiredApprovals,
                    History: updatedHistory,
                    ErrorMessage: null);
            }

            var outcome = approvalCount >= workflow.RequiredApprovals
                ? ApprovalOutcome.FullyApproved
                : ApprovalOutcome.Pending;

            return new ApprovalDecisionResult(
                Outcome: outcome,
                Workflow: workflow,
                ApprovalsRecorded: approvalCount,
                ApprovalsRequired: workflow.RequiredApprovals,
                History: updatedHistory,
                ErrorMessage: null);
        }

        public async Task<ApprovalDecisionResult> GetStatusAsync(
            string targetEntityType,
            int targetEntityId,
            WorkflowType workflowType,
            decimal amount)
        {
            var workflow = await ResolveWorkflowAsync(workflowType, amount);
            var history = await GetHistoryAsync(targetEntityType, targetEntityId);
            var approvalCount = history.Count(a => a.Decision == ApprovalDecision.Approved);
            var anyReject = history.Any(a => a.Decision == ApprovalDecision.Rejected);

            ApprovalOutcome outcome;
            if (workflow == null) outcome = ApprovalOutcome.NoWorkflowApplicable;
            else if (anyReject) outcome = ApprovalOutcome.Rejected;
            else if (approvalCount >= workflow.RequiredApprovals) outcome = ApprovalOutcome.FullyApproved;
            else outcome = ApprovalOutcome.Pending;

            return new ApprovalDecisionResult(
                Outcome: outcome,
                Workflow: workflow,
                ApprovalsRecorded: approvalCount,
                ApprovalsRequired: workflow?.RequiredApprovals ?? 1,
                History: history,
                ErrorMessage: null);
        }

        public async Task<IReadOnlyList<PendingApprovalRow>> GetPendingForUserAsync(
            string userId,
            IEnumerable<string> userRoles,
            int? companyIdFilter)
        {
            var userRolesList = userRoles.ToList();
            var rows = new List<PendingApprovalRow>();

            // Pending POs — Status = PendingApproval.
            var pendingPos = await _db.PurchaseOrders
                .Include(p => p.Vendor)
                .Where(p => p.Status == POStatus.PendingApproval
                         && (companyIdFilter == null || p.CompanyId == companyIdFilter))
                .ToListAsync();

            foreach (var po in pendingPos)
            {
                var workflow = await ResolveWorkflowAsync(WorkflowType.PurchaseOrder, po.Total);
                if (workflow == null) continue;

                var allowedRoles = SplitCsv(workflow.ApproverRoles);
                if (allowedRoles.Count > 0
                    && !allowedRoles.Any(r => userRolesList.Contains(r, StringComparer.OrdinalIgnoreCase)))
                    continue;

                var history = await GetHistoryAsync("PurchaseOrder", po.Id);
                // Hide rows where the user has already approved.
                if (history.Any(a => a.Decision == ApprovalDecision.Approved
                                  && string.Equals(a.DecidedByUserId, userId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var approvalCount = history.Count(a => a.Decision == ApprovalDecision.Approved);
                rows.Add(new PendingApprovalRow(
                    TargetEntityType: "PurchaseOrder",
                    TargetEntityId: po.Id,
                    Title: $"PO {po.PONumber}",
                    Subtitle: $"{po.Vendor?.Name ?? "(no vendor)"} · {po.Total:C} · {po.OrderDate:yyyy-MM-dd}",
                    Amount: po.Total,
                    ApprovalsRecorded: approvalCount,
                    ApprovalsRequired: workflow.RequiredApprovals,
                    WorkflowName: workflow.Name,
                    DetailUrl: $"/Purchasing/Details?id={po.Id}"));
            }

            // Pending Vendor Invoices — Status = PendingApproval.
            var pendingInvs = await _db.VendorInvoices
                .Include(v => v.Vendor)
                .Where(v => v.Status == InvoiceStatus.PendingApproval
                         && (companyIdFilter == null || v.CompanyId == companyIdFilter))
                .ToListAsync();

            foreach (var inv in pendingInvs)
            {
                var workflow = await ResolveWorkflowAsync(WorkflowType.Invoice, inv.Total);
                if (workflow == null) continue;

                var allowedRoles = SplitCsv(workflow.ApproverRoles);
                if (allowedRoles.Count > 0
                    && !allowedRoles.Any(r => userRolesList.Contains(r, StringComparer.OrdinalIgnoreCase)))
                    continue;

                var history = await GetHistoryAsync("VendorInvoice", inv.Id);
                if (history.Any(a => a.Decision == ApprovalDecision.Approved
                                  && string.Equals(a.DecidedByUserId, userId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var approvalCount = history.Count(a => a.Decision == ApprovalDecision.Approved);
                rows.Add(new PendingApprovalRow(
                    TargetEntityType: "VendorInvoice",
                    TargetEntityId: inv.Id,
                    Title: $"Invoice {inv.InvoiceNumber}",
                    Subtitle: $"{inv.Vendor?.Name ?? "(no vendor)"} · {inv.Total:C} · {inv.InvoiceDate:yyyy-MM-dd}",
                    Amount: inv.Total,
                    ApprovalsRecorded: approvalCount,
                    ApprovalsRequired: workflow.RequiredApprovals,
                    WorkflowName: workflow.Name,
                    DetailUrl: $"/AccountsPayable/Details?id={inv.Id}"));
            }

            return rows.OrderByDescending(r => r.Amount).ToList();
        }

        // ---------- Helpers ----------

        private async Task<List<ApprovalAction>> GetHistoryAsync(string targetEntityType, int targetEntityId)
        {
            return await _db.ApprovalActions
                .Where(a => a.TargetEntityType == targetEntityType && a.TargetEntityId == targetEntityId)
                .OrderBy(a => a.DecidedAt)
                .ToListAsync();
        }

        private async Task<ApprovalAction> PersistActionAsync(
            string targetEntityType, int targetEntityId, int? workflowId, int stepNumber,
            ApprovalDecision decision, string userId, string username,
            string? approverRole, string? comment, int? companyId,
            List<ApprovalAction> _existingForLog)
        {
            var action = new ApprovalAction
            {
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId,
                ApprovalWorkflowId = workflowId,
                StepNumber = stepNumber,
                Decision = decision,
                DecidedByUserId = userId,
                DecidedByUsername = username,
                DecidedAt = DateTime.UtcNow,
                Comment = comment,
                ApproverRole = approverRole,
                CompanyId = companyId
            };
            _db.ApprovalActions.Add(action);
            await _db.SaveChangesAsync();

            // Defense-in-depth: also write to AuditLog (flat snapshot to
            // avoid cycle pitfalls per feedback_audit_log_serialization).
            try
            {
                var snapshot = new
                {
                    action.Id,
                    action.TargetEntityType,
                    action.TargetEntityId,
                    action.ApprovalWorkflowId,
                    action.StepNumber,
                    Decision = action.Decision.ToString(),
                    action.DecidedByUserId,
                    action.DecidedByUsername,
                    action.DecidedAt,
                    action.ApproverRole,
                    action.Comment,
                    action.CompanyId
                };
                await _audit.LogAsync<object>(
                    action: $"Approval{action.Decision}",
                    before: null,
                    after: snapshot,
                    username: username,
                    description: $"{action.Decision} {targetEntityType}#{targetEntityId} as {approverRole ?? "(no role match)"}.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AuditLog write failed for ApprovalAction {Id}; ApprovalAction row still persisted.", action.Id);
            }

            return action;
        }

        private static List<string> SplitCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }
}
