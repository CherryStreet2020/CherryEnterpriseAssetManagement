// Theme B9 Wave 6 PR-15 (2026-05-30) — ProjectChangeService impl. OPENS Wave 6.
//
// Tenant-scoped through the parent CustomerProject. Owns the ProjectChangeRequest
// intake/impact/approval workflow and the conversion into a ProjectAmendment
// (the change order). Hosts the §20 gate: a customer scope change cannot be
// applied (converted to a contract-moving change order) before its required
// approval(s) clear. Every incoming FK on a write is scoped to the project.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectChangeService : IProjectChangeService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectChangeService> _log;

    public ProjectChangeService(AppDbContext db, ITenantContext tenant, ILogger<ProjectChangeService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<(bool ok, string? err, decimal contract, string currency)> ProjectInfoAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.", 0m, "USD");
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.ContractValue, p.Currency, p.Status })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? (false, $"Customer project {projectId} not found in your tenant scope.", 0m, "USD")
            : (true, null, row.ContractValue ?? 0m, string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency!);
    }

    private async Task<ProjectChangeRequest?> LoadScopedAsync(int crId, CancellationToken ct)
    {
        if (crId <= 0) return null;
        return await _db.ProjectChangeRequests
            .Where(c => c.Id == crId)
            .Join(_db.CustomerProjects.Where(p => _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0)),
                  c => c.CustomerProjectId, p => p.Id, (c, p) => c)
            .FirstOrDefaultAsync(ct);
    }

    private static bool IsTerminal(ProjectChangeRequestStatus s)
        => s is ProjectChangeRequestStatus.Rejected
             or ProjectChangeRequestStatus.Cancelled
             or ProjectChangeRequestStatus.Converted;

    // A change request is approved-enough to become a change order when its
    // required approvals have cleared (the §20 gate predicate).
    private static bool IsApprovedForConversion(ProjectChangeRequest c)
    {
        if (c.RequiresCustomerApproval)
            return c.Status == ProjectChangeRequestStatus.CustomerApproved;
        if (c.RequiresInternalApproval)
            return c.Status is ProjectChangeRequestStatus.InternalApproved
                            or ProjectChangeRequestStatus.CustomerApproved;
        // No approval required: any estimated, non-terminal request qualifies.
        return c.Status is ProjectChangeRequestStatus.Estimated
                        or ProjectChangeRequestStatus.InternalApproved
                        or ProjectChangeRequestStatus.SubmittedToCustomer
                        or ProjectChangeRequestStatus.CustomerApproved;
    }

    private static bool CanConvert(ProjectChangeRequest c)
        => !IsTerminal(c.Status)
           && c.ResultingProjectAmendmentId is null
           && IsApprovedForConversion(c);

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectChangeView>> GetChangesAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err, contract, currency) = await ProjectInfoAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectChangeView>(err!);

        var requests = await _db.ProjectChangeRequests
            .Where(c => c.CustomerProjectId == projectId)
            .OrderBy(c => c.ChangeRequestNumber)
            .ToListAsync(ct);

        var approvedChangeValue = await _db.ProjectAmendments
            .Where(a => a.CustomerProjectId == projectId && a.Status == ProjectAmendmentStatus.Approved)
            .SumAsync(a => (decimal?)a.ValueDelta, ct) ?? 0m;

        var changeOrderCount = await _db.ProjectAmendments
            .CountAsync(a => a.CustomerProjectId == projectId, ct);

        var pendingExposure = requests
            .Where(c => !IsTerminal(c.Status))
            .Sum(c => c.RevenueImpact);

        var openCount = requests.Count(c => !IsTerminal(c.Status));

        var rows = requests.Select(c => new ChangeRequestRow(
            c.Id, c.ChangeRequestNumber, c.Title, c.Source, c.Category, c.Status,
            c.RequestedByName, c.RequestDate, c.CostImpact, c.RevenueImpact, c.MarginImpactPct,
            c.ScheduleImpactDays, c.RiskImpact, c.Currency, c.AffectedPhaseId,
            c.RequiresInternalApproval, c.RequiresCustomerApproval,
            c.InternalApprovedAt.HasValue, c.CustomerApprovedAt.HasValue,
            c.ResultingProjectAmendmentId,
            c.Status == ProjectChangeRequestStatus.Converted,
            CanConvert(c))).ToList();

        return Result.Success(new ProjectChangeView(
            projectId, currency, contract, approvedChangeValue, contract + approvedChangeValue,
            pendingExposure, openCount, changeOrderCount, rows));
    }

    // ------------------------------------------------------------------
    // Create change request
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateChangeRequestAsync(CreateChangeRequestRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err, _, projCurrency) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        // Tenant-scope the optional WBS phase peg to THIS project.
        if (req.AffectedPhaseId.HasValue && !await _db.ProjectPhases.AnyAsync(
                p => p.Id == req.AffectedPhaseId.Value && p.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.AffectedPhaseId} is not in this project.");

        // Per-project monotonic number. The unique index backstops a race.
        var nextNumber = (await _db.ProjectChangeRequests
            .Where(c => c.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(c => (int?)c.ChangeRequestNumber, ct) ?? 0) + 1;

        var cr = new ProjectChangeRequest
        {
            CustomerProjectId = req.CustomerProjectId,
            ChangeRequestNumber = nextNumber,
            Title = req.Title,
            Source = req.Source,
            Category = req.Category,
            Status = ProjectChangeRequestStatus.Draft,
            RequestedByName = req.RequestedByName,
            RequestDate = req.RequestDate ?? DateTime.UtcNow,
            Description = req.Description,
            RiskImpact = req.RiskImpact,
            RequiresInternalApproval = req.RequiresInternalApproval,
            RequiresCustomerApproval = req.RequiresCustomerApproval,
            AffectedPhaseId = req.AffectedPhaseId,
            CustomerReference = req.CustomerReference,
            CustomerPoRevision = req.CustomerPoRevision,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? projCurrency : req.Currency!,
            Notes = req.Notes,
            CreatedBy = req.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectChangeRequests.Add(cr);
        await _db.SaveChangesAsync(ct);
        return Result.Success(cr.Id);
    }

    // ------------------------------------------------------------------
    // Update impact analysis (the "Estimate change" action)
    // ------------------------------------------------------------------
    public async Task<Result<ProjectChangeRequest>> UpdateImpactAsync(UpdateChangeImpactRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectChangeRequest>("Request is required.");
        var cr = await LoadScopedAsync(req.ProjectChangeRequestId, ct);
        if (cr is null) return Result.Failure<ProjectChangeRequest>($"Change request {req.ProjectChangeRequestId} not found in your tenant scope.");

        if (cr.Status is not (ProjectChangeRequestStatus.Draft
                           or ProjectChangeRequestStatus.UnderReview
                           or ProjectChangeRequestStatus.Estimated))
            return Result.Failure<ProjectChangeRequest>(
                $"Impact can only be (re)estimated while Draft, UnderReview, or Estimated — current status is {cr.Status}.");

        cr.CostImpact = req.CostImpact;
        cr.RevenueImpact = req.RevenueImpact;
        cr.MarginImpactPct = req.MarginImpactPct;
        cr.ScheduleImpactDays = req.ScheduleImpactDays;
        cr.RiskImpact = req.RiskImpact;
        cr.ImpactNarrative = req.ImpactNarrative;
        cr.BillingTreatment = req.BillingTreatment;
        cr.CostTreatment = req.CostTreatment;
        // Recording impact advances the request to Estimated.
        if (cr.Status is ProjectChangeRequestStatus.Draft or ProjectChangeRequestStatus.UnderReview)
            cr.Status = ProjectChangeRequestStatus.Estimated;
        cr.ModifiedAt = DateTime.UtcNow;
        cr.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(cr);
    }

    // ------------------------------------------------------------------
    // Workflow transition (legal-map + set-once stamps)
    // ------------------------------------------------------------------
    public async Task<Result<ProjectChangeRequest>> TransitionAsync(TransitionChangeRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectChangeRequest>("Request is required.");
        var cr = await LoadScopedAsync(req.ProjectChangeRequestId, ct);
        if (cr is null) return Result.Failure<ProjectChangeRequest>($"Change request {req.ProjectChangeRequestId} not found in your tenant scope.");

        if (cr.Status == ProjectChangeRequestStatus.Converted)
            return Result.Failure<ProjectChangeRequest>("This change request has already been converted to a change order; it can no longer transition.");

        if (!IsLegalTransition(cr, req.NewStatus, out var why))
            return Result.Failure<ProjectChangeRequest>(why!);

        switch (req.NewStatus)
        {
            case ProjectChangeRequestStatus.InternalApproved:
                cr.InternalApprovedAt = DateTime.UtcNow;
                cr.InternalApprovedBy = req.ActorName;
                break;
            case ProjectChangeRequestStatus.SubmittedToCustomer:
                cr.SubmittedToCustomerAt = DateTime.UtcNow;
                break;
            case ProjectChangeRequestStatus.CustomerApproved:
                cr.CustomerApprovedAt = DateTime.UtcNow;
                cr.CustomerApprovedBy = req.ActorName;
                break;
            case ProjectChangeRequestStatus.Rejected:
                cr.RejectedAt = DateTime.UtcNow;
                cr.RejectedBy = req.ActorName;
                break;
        }

        cr.Status = req.NewStatus;
        cr.ModifiedAt = DateTime.UtcNow;
        cr.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(cr);
    }

    // Legal-map. Terminal states (Rejected/Cancelled/Converted) have no
    // outgoing edge. Approval-skip paths honor the Requires* flags.
    private static bool IsLegalTransition(ProjectChangeRequest c, ProjectChangeRequestStatus to, out string? why)
    {
        why = null;
        if (to == c.Status) { why = $"Change request is already {to}."; return false; }

        // Converted is reached only via ConvertToChangeOrderAsync, never here.
        if (to == ProjectChangeRequestStatus.Converted)
        { why = "Use ConvertToChangeOrder to convert an approved change request into a change order."; return false; }

        // Cancel is allowed from any non-terminal state.
        if (to == ProjectChangeRequestStatus.Cancelled)
        {
            if (IsTerminal(c.Status)) { why = $"Cannot cancel a {c.Status} change request."; return false; }
            return true;
        }
        // Reject is allowed from any non-terminal review/approval state.
        if (to == ProjectChangeRequestStatus.Rejected)
        {
            if (IsTerminal(c.Status) || c.Status == ProjectChangeRequestStatus.Draft)
            { why = $"Cannot reject a {c.Status} change request."; return false; }
            return true;
        }

        bool legal = c.Status switch
        {
            ProjectChangeRequestStatus.Draft => to == ProjectChangeRequestStatus.UnderReview,
            ProjectChangeRequestStatus.UnderReview => to == ProjectChangeRequestStatus.Estimated,
            ProjectChangeRequestStatus.Estimated =>
                (c.RequiresInternalApproval && to == ProjectChangeRequestStatus.InternalApproved)
                || (!c.RequiresInternalApproval && c.RequiresCustomerApproval && to == ProjectChangeRequestStatus.SubmittedToCustomer)
                || (!c.RequiresInternalApproval && !c.RequiresCustomerApproval && to == ProjectChangeRequestStatus.CustomerApproved),
            ProjectChangeRequestStatus.InternalApproved =>
                (c.RequiresCustomerApproval && to == ProjectChangeRequestStatus.SubmittedToCustomer)
                || (!c.RequiresCustomerApproval && to == ProjectChangeRequestStatus.CustomerApproved),
            ProjectChangeRequestStatus.SubmittedToCustomer => to == ProjectChangeRequestStatus.CustomerApproved,
            _ => false
        };
        if (!legal) why = $"Illegal transition {c.Status} → {to} (check the required-approval flags).";
        return legal;
    }

    // ------------------------------------------------------------------
    // Convert to change order (the §20 gate)
    // ------------------------------------------------------------------
    public async Task<Result<ConvertToChangeOrderResult>> ConvertToChangeOrderAsync(ConvertToChangeOrderRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ConvertToChangeOrderResult>("Request is required.");
        var cr = await LoadScopedAsync(req.ProjectChangeRequestId, ct);
        if (cr is null) return Result.Failure<ConvertToChangeOrderResult>($"Change request {req.ProjectChangeRequestId} not found in your tenant scope.");

        if (cr.Status == ProjectChangeRequestStatus.Converted || cr.ResultingProjectAmendmentId is not null)
            return Result.Failure<ConvertToChangeOrderResult>(
                $"Change request CR #{cr.ChangeRequestNumber} has already been converted to change order #{cr.ResultingProjectAmendmentId}.");

        // §20 GATE — cannot apply a customer scope change before approval.
        if (!IsApprovedForConversion(cr))
        {
            var needed = cr.RequiresCustomerApproval ? "customer approval"
                       : cr.RequiresInternalApproval ? "internal approval"
                       : "impact estimation";
            return Result.Failure<ConvertToChangeOrderResult>(
                $"Cannot convert CR #{cr.ChangeRequestNumber} to a change order before {needed} — current status is {cr.Status}.");
        }

        // The change order's contract-value delta is the customer revenue impact;
        // a schedule slip flows into the end-date delta. ScopeNarrative carries
        // the impact prose; Reason carries the headline.
        var valueDelta = cr.RevenueImpact;

        // Sanity guard mirrors CreateAmendmentAsync: a single change order cannot
        // drive the baseline contract value below zero.
        var (_, _, baseline, _) = await ProjectInfoAsync(cr.CustomerProjectId, ct);
        if (baseline + valueDelta < 0)
            return Result.Failure<ConvertToChangeOrderResult>(
                "Change order value would drive the baseline contract value below zero.");

        var nextNumber = (await _db.ProjectAmendments
            .Where(a => a.CustomerProjectId == cr.CustomerProjectId)
            .MaxAsync(a => (int?)a.AmendmentNumber, ct) ?? 0) + 1;

        bool approveNow = req.ApproveImmediately;
        var amendment = new ProjectAmendment
        {
            CustomerProjectId = cr.CustomerProjectId,
            AmendmentNumber = nextNumber,
            EffectiveDate = req.EffectiveDate,
            ChangeType = req.ChangeType,
            Reason = cr.Title,
            ScopeNarrative = cr.ImpactNarrative ?? cr.Description,
            ValueDelta = valueDelta,
            TargetEndDateDelta = cr.ScheduleImpactDays,
            CustomerReference = req.CustomerReference ?? cr.CustomerReference,
            SourceChangeRequestId = cr.Id,
            Notes = req.Notes,
            Status = approveNow ? ProjectAmendmentStatus.Approved : ProjectAmendmentStatus.Draft,
            ApprovedByName = approveNow ? req.ApprovedByName : null,
            ApprovedAt = approveNow ? DateTime.UtcNow : (DateTime?)null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectAmendments.Add(amendment);

        // Cross-link + close the request out as Converted IN THE SAME UNIT OF
        // WORK so the amendment insert and the request update commit (or roll
        // back) together (Codex P1). Setting the NAVIGATION lets EF populate
        // ResultingProjectAmendmentId from the generated key after the insert;
        // and because ProjectChangeRequest carries an xmin token, a concurrent
        // conversion that already linked the request makes THIS SaveChanges
        // throw DbUpdateConcurrencyException — which aborts the whole
        // transaction, including the amendment insert, so no orphan/duplicate
        // approved amendment can contribute to the effective contract value.
        cr.ResultingProjectAmendment = amendment;
        cr.Status = ProjectChangeRequestStatus.Converted;
        cr.ModifiedAt = DateTime.UtcNow;
        cr.ModifiedBy = req.CreatedBy;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Converted CR #{Num} (project {Pid}) → amendment {AId} (status {St}, delta {Delta}).",
            cr.ChangeRequestNumber, cr.CustomerProjectId, amendment.Id, amendment.Status, valueDelta);

        return Result.Success(new ConvertToChangeOrderResult(
            cr.Id, amendment.Id, amendment.AmendmentNumber, amendment.Status, valueDelta));
    }
}
