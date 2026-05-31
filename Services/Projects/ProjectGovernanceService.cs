// Theme B9 Wave 6 PR-16 (2026-05-31) — ProjectGovernanceService impl.
//
// RAID (Risks/Issues/Actions/Decisions) + meetings for a CustomerProject.
// Tenant-scoped through the parent project; every incoming FK on a write is
// scoped to the project (phase / change-request / meeting pegs). Per-project
// monotonic numbers via plain LINQ MAX+1 (unique index backstops a race —
// InMemory-testable, no raw-SQL lock). Set-once close/complete stamps; status
// legal-maps with terminal states rejecting re-entry.

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

public sealed class ProjectGovernanceService : IProjectGovernanceService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectGovernanceService> _log;

    public ProjectGovernanceService(AppDbContext db, ITenantContext tenant, ILogger<ProjectGovernanceService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<(bool ok, string? err, string currency)> ProjectInfoAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.", "USD");
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.Currency })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? (false, $"Customer project {projectId} not found in your tenant scope.", "USD")
            : (true, null, string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency!);
    }

    // Validate the optional WBS phase + change-request pegs belong to THIS project.
    private async Task<string?> ValidatePegsAsync(int projectId, int? phaseId, int? changeRequestId, CancellationToken ct)
    {
        if (phaseId.HasValue && !await _db.ProjectPhases.AnyAsync(p => p.Id == phaseId.Value && p.CustomerProjectId == projectId, ct))
            return $"Phase {phaseId} is not in this project.";
        if (changeRequestId.HasValue && !await _db.ProjectChangeRequests.AnyAsync(c => c.Id == changeRequestId.Value && c.CustomerProjectId == projectId, ct))
            return $"Change request {changeRequestId} is not in this project.";
        return null;
    }

    private static int ExposureOf(ProjectRiskRating p, ProjectRiskRating i)
        => (p == ProjectRiskRating.NotSet || i == ProjectRiskRating.NotSet) ? 0 : (int)p * (int)i;

    private async Task<T?> LoadScopedAsync<T>(DbSet<T> set, int id, Func<T, int> projectIdOf, CancellationToken ct) where T : class
    {
        if (id <= 0) return null;
        var row = await set.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct);
        if (row is null) return null;
        var pid = projectIdOf(row);
        var visible = await _db.CustomerProjects.AnyAsync(p => p.Id == pid && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0), ct);
        return visible ? row : null;
    }

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectGovernanceView>> GetGovernanceAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err, currency) = await ProjectInfoAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectGovernanceView>(err!);

        var risks = await _db.ProjectRisks.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.RiskNumber).ToListAsync(ct);
        var issues = await _db.ProjectIssues.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.IssueNumber).ToListAsync(ct);
        var actions = await _db.ProjectActionItems.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.ActionNumber).ToListAsync(ct);
        var decisions = await _db.ProjectDecisions.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.DecisionNumber).ToListAsync(ct);
        var meetings = await _db.ProjectMeetings.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.MeetingNumber).ToListAsync(ct);

        var actionCountByMeeting = actions.Where(a => a.ProjectMeetingId.HasValue)
            .GroupBy(a => a.ProjectMeetingId!.Value).ToDictionary(g => g.Key, g => g.Count());

        static bool RiskOpen(ProjectRisk r) => r.Status is ProjectRiskStatus.Open or ProjectRiskStatus.Mitigating or ProjectRiskStatus.Escalated;
        static bool IssueOpen(ProjectIssue i) => i.Status is ProjectIssueStatus.Open or ProjectIssueStatus.InProgress or ProjectIssueStatus.Escalated;
        static bool ActionOpen(ProjectActionItem a) => a.Status is ProjectActionStatus.Open or ProjectActionStatus.InProgress;
        var today = DateTime.UtcNow.Date;

        var riskRows = risks.Select(r => new RiskRow(
            r.Id, r.RiskNumber, r.Title, r.Category, r.Probability, r.Impact, r.Exposure, r.Status, r.Owner,
            r.DueDate, r.CostExposure, r.ScheduleExposureDays, r.CustomerImpact, r.SupplierImpact,
            r.AffectedPhaseId, r.LinkedChangeRequestId, RiskOpen(r))).ToList();
        var issueRows = issues.Select(i => new IssueRow(
            i.Id, i.IssueNumber, i.Title, i.Severity, i.Priority, i.Status, i.Owner, i.OpenDate, i.DueDate,
            i.ClosedDate, i.CostImpact, i.ScheduleImpactDays, i.CustomerImpact, i.AffectedPhaseId,
            i.LinkedChangeRequestId, IssueOpen(i))).ToList();
        var actionRows = actions.Select(a => new ActionRow(
            a.Id, a.ActionNumber, a.Description, a.Owner, a.DueDate, a.Priority, a.Status, a.CompletionDate,
            a.ProjectMeetingId, a.Source, a.SourceId, ActionOpen(a),
            ActionOpen(a) && a.DueDate.HasValue && a.DueDate.Value.Date < today)).ToList();
        var decisionRows = decisions.Select(d => new DecisionRow(
            d.Id, d.DecisionNumber, d.Title, d.DecisionDate, d.DecisionMaker, d.Status, d.AffectedPhaseId, d.LinkedChangeRequestId)).ToList();
        var meetingRows = meetings.Select(m => new MeetingRow(
            m.Id, m.MeetingNumber, m.Title, m.MeetingType, m.MeetingDate, m.Status,
            actionCountByMeeting.TryGetValue(m.Id, out var c) ? c : 0)).ToList();

        return Result.Success(new ProjectGovernanceView(
            projectId, currency,
            riskRows.Count(r => r.IsOpen),
            riskRows.Where(r => r.IsOpen).Select(r => r.Exposure).DefaultIfEmpty(0).Max(),
            riskRows.Where(r => r.IsOpen).Sum(r => r.CostExposure),
            issueRows.Count(i => i.IsOpen),
            issueRows.Count(i => i.IsOpen && i.Severity == ProjectIssueSeverity.Critical),
            actionRows.Count(a => a.IsOpen),
            actionRows.Count(a => a.IsOverdue),
            decisionRows.Count,
            meetingRows.Count,
            riskRows, issueRows, actionRows, decisionRows, meetingRows));
    }

    // ------------------------------------------------------------------
    // Risks
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateRiskAsync(CreateRiskRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err, projCurrency) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pegErr = await ValidatePegsAsync(req.CustomerProjectId, req.AffectedPhaseId, req.LinkedChangeRequestId, ct);
        if (pegErr != null) return Result.Failure<int>(pegErr);

        var n = (await _db.ProjectRisks.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.RiskNumber, ct) ?? 0) + 1;
        var risk = new ProjectRisk
        {
            CustomerProjectId = req.CustomerProjectId, RiskNumber = n, Title = req.Title, Description = req.Description,
            Category = req.Category, Probability = req.Probability, Impact = req.Impact,
            Exposure = ExposureOf(req.Probability, req.Impact), Owner = req.Owner, MitigationPlan = req.MitigationPlan,
            ContingencyPlan = req.ContingencyPlan, Trigger = req.Trigger, Status = ProjectRiskStatus.Open,
            DueDate = req.DueDate, CostExposure = req.CostExposure, ScheduleExposureDays = req.ScheduleExposureDays,
            CustomerImpact = req.CustomerImpact, SupplierImpact = req.SupplierImpact, AffectedPhaseId = req.AffectedPhaseId,
            LinkedChangeRequestId = req.LinkedChangeRequestId,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? projCurrency : req.Currency!,
            Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectRisks.Add(risk);
        await _db.SaveChangesAsync(ct);
        return Result.Success(risk.Id);
    }

    public async Task<Result<ProjectRisk>> UpdateRiskAssessmentAsync(UpdateRiskAssessmentRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectRisk>("Request is required.");
        var risk = await LoadScopedAsync(_db.ProjectRisks, req.ProjectRiskId, r => r.CustomerProjectId, ct);
        if (risk is null) return Result.Failure<ProjectRisk>($"Risk {req.ProjectRiskId} not found in your tenant scope.");
        if (risk.Status is ProjectRiskStatus.Closed or ProjectRiskStatus.Accepted)
            return Result.Failure<ProjectRisk>($"Cannot re-assess a {risk.Status} risk.");

        risk.Probability = req.Probability;
        risk.Impact = req.Impact;
        risk.Exposure = ExposureOf(req.Probability, req.Impact);
        if (req.MitigationPlan != null) risk.MitigationPlan = req.MitigationPlan;
        if (req.ContingencyPlan != null) risk.ContingencyPlan = req.ContingencyPlan;
        if (req.Trigger != null) risk.Trigger = req.Trigger;
        if (req.CostExposure.HasValue) risk.CostExposure = req.CostExposure.Value;
        if (req.ScheduleExposureDays.HasValue) risk.ScheduleExposureDays = req.ScheduleExposureDays;
        risk.ModifiedAt = DateTime.UtcNow; risk.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(risk);
    }

    public async Task<Result<ProjectRisk>> TransitionRiskAsync(int riskId, ProjectRiskStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default)
    {
        var risk = await LoadScopedAsync(_db.ProjectRisks, riskId, r => r.CustomerProjectId, ct);
        if (risk is null) return Result.Failure<ProjectRisk>($"Risk {riskId} not found in your tenant scope.");
        bool terminal = risk.Status is ProjectRiskStatus.Closed or ProjectRiskStatus.Accepted;
        if (terminal) return Result.Failure<ProjectRisk>($"Risk is {risk.Status} (terminal) and cannot transition.");
        if (newStatus == risk.Status) return Result.Failure<ProjectRisk>($"Risk is already {newStatus}.");

        if (newStatus is ProjectRiskStatus.Closed or ProjectRiskStatus.Accepted)
        {
            risk.ClosedAt = DateTime.UtcNow;
            risk.ClosedBy = actor;
        }
        risk.Status = newStatus;
        risk.ModifiedAt = DateTime.UtcNow; risk.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(risk);
    }

    // ------------------------------------------------------------------
    // Issues
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateIssueAsync(CreateIssueRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err, projCurrency) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pegErr = await ValidatePegsAsync(req.CustomerProjectId, req.AffectedPhaseId, req.LinkedChangeRequestId, ct);
        if (pegErr != null) return Result.Failure<int>(pegErr);

        var n = (await _db.ProjectIssues.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.IssueNumber, ct) ?? 0) + 1;
        var issue = new ProjectIssue
        {
            CustomerProjectId = req.CustomerProjectId, IssueNumber = n, Title = req.Title, Description = req.Description,
            Severity = req.Severity, Priority = req.Priority, Owner = req.Owner,
            OpenDate = req.OpenDate ?? DateTime.UtcNow, DueDate = req.DueDate, Status = ProjectIssueStatus.Open,
            RootCause = req.RootCause, CorrectiveAction = req.CorrectiveAction, CustomerImpact = req.CustomerImpact,
            CostImpact = req.CostImpact, ScheduleImpactDays = req.ScheduleImpactDays, AffectedPhaseId = req.AffectedPhaseId,
            LinkedChangeRequestId = req.LinkedChangeRequestId,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? projCurrency : req.Currency!,
            Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectIssues.Add(issue);
        await _db.SaveChangesAsync(ct);
        return Result.Success(issue.Id);
    }

    public async Task<Result<ProjectIssue>> TransitionIssueAsync(int issueId, ProjectIssueStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default)
    {
        var issue = await LoadScopedAsync(_db.ProjectIssues, issueId, i => i.CustomerProjectId, ct);
        if (issue is null) return Result.Failure<ProjectIssue>($"Issue {issueId} not found in your tenant scope.");
        if (issue.Status == ProjectIssueStatus.Closed)
            return Result.Failure<ProjectIssue>("Issue is Closed (terminal) and cannot transition.");
        if (newStatus == issue.Status) return Result.Failure<ProjectIssue>($"Issue is already {newStatus}.");

        if (newStatus == ProjectIssueStatus.Closed)
        {
            issue.ClosedAt = DateTime.UtcNow;
            issue.ClosedBy = actor;
            issue.ClosedDate ??= DateTime.UtcNow.Date;
        }
        issue.Status = newStatus;
        issue.ModifiedAt = DateTime.UtcNow; issue.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(issue);
    }

    // ------------------------------------------------------------------
    // Meetings
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateMeetingAsync(CreateMeetingRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var n = (await _db.ProjectMeetings.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.MeetingNumber, ct) ?? 0) + 1;
        var m = new ProjectMeeting
        {
            CustomerProjectId = req.CustomerProjectId, MeetingNumber = n, Title = req.Title, MeetingType = req.MeetingType,
            MeetingDate = req.MeetingDate ?? DateTime.UtcNow, Location = req.Location, Attendees = req.Attendees,
            Agenda = req.Agenda, Minutes = req.Minutes, Status = ProjectMeetingStatus.Scheduled, Notes = req.Notes,
            CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectMeetings.Add(m);
        await _db.SaveChangesAsync(ct);
        return Result.Success(m.Id);
    }

    public async Task<Result<ProjectMeeting>> TransitionMeetingAsync(int meetingId, ProjectMeetingStatus newStatus, string? modifiedBy = null, CancellationToken ct = default)
    {
        var m = await LoadScopedAsync(_db.ProjectMeetings, meetingId, x => x.CustomerProjectId, ct);
        if (m is null) return Result.Failure<ProjectMeeting>($"Meeting {meetingId} not found in your tenant scope.");
        if (m.Status == ProjectMeetingStatus.Cancelled)
            return Result.Failure<ProjectMeeting>("Meeting is Cancelled (terminal) and cannot transition.");
        if (newStatus == m.Status) return Result.Failure<ProjectMeeting>($"Meeting is already {newStatus}.");
        m.Status = newStatus;
        m.ModifiedAt = DateTime.UtcNow; m.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(m);
    }

    // ------------------------------------------------------------------
    // Action items
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateActionItemAsync(CreateActionItemRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        // Tenant-scope the meeting peg to THIS project.
        if (req.ProjectMeetingId.HasValue && !await _db.ProjectMeetings.AnyAsync(
                m => m.Id == req.ProjectMeetingId.Value && m.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Meeting {req.ProjectMeetingId} is not in this project.");

        var n = (await _db.ProjectActionItems.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.ActionNumber, ct) ?? 0) + 1;
        var a = new ProjectActionItem
        {
            CustomerProjectId = req.CustomerProjectId, ActionNumber = n, ProjectMeetingId = req.ProjectMeetingId,
            Owner = req.Owner, Description = req.Description, DueDate = req.DueDate, Priority = req.Priority,
            Status = ProjectActionStatus.Open, Source = req.Source, SourceId = req.SourceId, Notes = req.Notes,
            CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectActionItems.Add(a);
        await _db.SaveChangesAsync(ct);
        return Result.Success(a.Id);
    }

    public async Task<Result<ProjectActionItem>> TransitionActionItemAsync(int actionId, ProjectActionStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default)
    {
        var a = await LoadScopedAsync(_db.ProjectActionItems, actionId, x => x.CustomerProjectId, ct);
        if (a is null) return Result.Failure<ProjectActionItem>($"Action item {actionId} not found in your tenant scope.");
        if (a.Status is ProjectActionStatus.Done or ProjectActionStatus.Cancelled)
            return Result.Failure<ProjectActionItem>($"Action item is {a.Status} (terminal) and cannot transition.");
        if (newStatus == a.Status) return Result.Failure<ProjectActionItem>($"Action item is already {newStatus}.");

        if (newStatus == ProjectActionStatus.Done)
        {
            a.CompletedAt = DateTime.UtcNow;
            a.CompletedBy = actor;
            a.CompletionDate ??= DateTime.UtcNow.Date;
        }
        a.Status = newStatus;
        a.ModifiedAt = DateTime.UtcNow; a.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(a);
    }

    // ------------------------------------------------------------------
    // Decisions
    // ------------------------------------------------------------------
    public async Task<Result<int>> RecordDecisionAsync(RecordDecisionRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pegErr = await ValidatePegsAsync(req.CustomerProjectId, req.AffectedPhaseId, req.LinkedChangeRequestId, ct);
        if (pegErr != null) return Result.Failure<int>(pegErr);

        var n = (await _db.ProjectDecisions.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.DecisionNumber, ct) ?? 0) + 1;
        var d = new ProjectDecision
        {
            CustomerProjectId = req.CustomerProjectId, DecisionNumber = n, DecisionDate = req.DecisionDate ?? DateTime.UtcNow,
            Title = req.Title, Description = req.Description, DecisionMaker = req.DecisionMaker,
            AlternativesConsidered = req.AlternativesConsidered, Impact = req.Impact, Status = req.Status,
            AffectedPhaseId = req.AffectedPhaseId, LinkedChangeRequestId = req.LinkedChangeRequestId,
            Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectDecisions.Add(d);
        await _db.SaveChangesAsync(ct);
        return Result.Success(d.Id);
    }

    public async Task<Result<ProjectDecision>> TransitionDecisionAsync(int decisionId, ProjectDecisionStatus newStatus, string? modifiedBy = null, CancellationToken ct = default)
    {
        var d = await LoadScopedAsync(_db.ProjectDecisions, decisionId, x => x.CustomerProjectId, ct);
        if (d is null) return Result.Failure<ProjectDecision>($"Decision {decisionId} not found in your tenant scope.");
        if (d.Status is ProjectDecisionStatus.Rejected or ProjectDecisionStatus.Superseded)
            return Result.Failure<ProjectDecision>($"Decision is {d.Status} (terminal) and cannot transition.");
        if (newStatus == d.Status) return Result.Failure<ProjectDecision>($"Decision is already {newStatus}.");
        d.Status = newStatus;
        d.ModifiedAt = DateTime.UtcNow; d.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(d);
    }
}
