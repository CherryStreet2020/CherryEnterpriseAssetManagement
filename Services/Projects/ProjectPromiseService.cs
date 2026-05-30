// Theme B9 Wave 1 PR-2 (2026-05-30) — ProjectPromiseService impl.
// READ-ONLY. Composes schedule + linked-job state + readiness + change-order +
// EVM signals into a Green/Yellow/Red/Black promise verdict. Tenant-scoped.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectPromiseService : IProjectPromiseService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IOperationReadinessService _readiness;
    private readonly ILogger<ProjectPromiseService> _log;

    // Slip beyond this many days past target end is Red, not Yellow.
    private const int RedSlipDays = 14;
    // Progress more than this far behind the time-elapsed line is Yellow.
    private const decimal ProgressLagPct = 15m;
    // Cap readiness fan-out so a huge project can't stall the page.
    private const int MaxReadinessJobs = 25;

    public ProjectPromiseService(
        AppDbContext db, ITenantContext tenant,
        IOperationReadinessService readiness, ILogger<ProjectPromiseService> log)
    {
        _db = db; _tenant = tenant; _readiness = readiness; _log = log;
    }

    public async Task<Result<ProjectPromiseAssessment>> EvaluateAsync(
        int customerProjectId, CancellationToken ct = default)
    {
        if (customerProjectId <= 0)
            return Result.Failure<ProjectPromiseAssessment>("CustomerProjectId must be > 0.");

        var p = await _db.CustomerProjects
            .Where(x => x.Id == customerProjectId && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
            .Select(x => new
            {
                x.Id, x.Code, x.Name, x.Status,
                x.TargetStartDate, x.TargetEndDate, x.ProjectedEndDate, x.PercentComplete,
            })
            .FirstOrDefaultAsync(ct);
        if (p == null)
            return Result.Failure<ProjectPromiseAssessment>(
                $"Customer project {customerProjectId} not found in your tenant scope.");

        var reasons = new List<PromiseReason>();
        var today = DateTime.UtcNow.Date;
        bool terminal = p.Status is CustomerProjectStatus.Closed or CustomerProjectStatus.Cancelled;

        // Linked jobs.
        var jobs = await _db.ProductionOrders
            .Where(o => o.CustomerProjectId == customerProjectId
                && _tenant.VisibleCompanyIds.Contains(o.CompanyId))
            .Select(o => new { o.Id, o.OrderNumber, o.Status })
            .ToListAsync(ct);

        bool allJobsDone = jobs.Count > 0 && jobs.All(j => j.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Closed);
        bool complete = terminal || allJobsDone || (p.PercentComplete.HasValue && p.PercentComplete.Value >= 100m);

        // ── 1) Schedule: already past due (Black) vs projected slip (Red/Yellow) ──
        if (!p.TargetEndDate.HasValue)
        {
            reasons.Add(new PromiseReason(PromiseReasonCode.NoScheduleBaselined, PromiseStatus.Yellow,
                "No customer delivery date is baselined, so on-time delivery can't be confirmed."));
        }
        else if (!complete && today > p.TargetEndDate.Value.Date)
        {
            reasons.Add(new PromiseReason(PromiseReasonCode.AlreadyPastDue, PromiseStatus.Black,
                $"The target delivery date ({p.TargetEndDate.Value:MMM d, yyyy}) has already passed and the project is not complete."));
        }
        else if (p.ProjectedEndDate.HasValue && p.ProjectedEndDate.Value.Date > p.TargetEndDate.Value.Date)
        {
            int slip = (int)(p.ProjectedEndDate.Value.Date - p.TargetEndDate.Value.Date).TotalDays;
            reasons.Add(new PromiseReason(PromiseReasonCode.ScheduleSlip,
                slip > RedSlipDays ? PromiseStatus.Red : PromiseStatus.Yellow,
                $"Projected completion is {slip} day(s) past the target delivery date."));
        }

        // ── 2) Progress behind the time-elapsed line (Yellow) ──
        if (!complete && p.PercentComplete.HasValue && p.TargetStartDate.HasValue && p.TargetEndDate.HasValue
            && p.TargetEndDate.Value.Date > p.TargetStartDate.Value.Date)
        {
            var totalDays = (decimal)(p.TargetEndDate.Value.Date - p.TargetStartDate.Value.Date).TotalDays;
            var elapsedDays = (decimal)(today - p.TargetStartDate.Value.Date).TotalDays;
            if (elapsedDays > 0)
            {
                var expectedPct = Math.Clamp(elapsedDays / totalDays * 100m, 0m, 100m);
                if (p.PercentComplete.Value + ProgressLagPct < expectedPct)
                    reasons.Add(new PromiseReason(PromiseReasonCode.BehindOnProgress, PromiseStatus.Yellow,
                        $"Progress is {p.PercentComplete.Value:0.#}% but ~{expectedPct:0.#}% of the schedule has elapsed."));
            }
        }

        // ── 3) Linked-job state: on hold (Red) / not released (Yellow) ──
        if (!terminal)
        {
            int onHold = jobs.Count(j => j.Status == ProductionOrderStatus.OnHold);
            if (onHold > 0)
                reasons.Add(new PromiseReason(PromiseReasonCode.JobOnHold, PromiseStatus.Red,
                    $"{onHold} linked job(s) are on hold."));

            int notReleased = jobs.Count(j => j.Status is ProductionOrderStatus.Planned or ProductionOrderStatus.Firmed);
            if (notReleased > 0)
                reasons.Add(new PromiseReason(PromiseReasonCode.JobNotReleased, PromiseStatus.Yellow,
                    $"{notReleased} linked job(s) are planned but not yet released."));
        }

        // ── 4) Linked-job readiness: blocked / material short (best-effort) ──
        if (!terminal)
        {
            var openJobs = jobs
                .Where(j => j.Status is ProductionOrderStatus.Released or ProductionOrderStatus.InProgress)
                .Take(MaxReadinessJobs)
                .ToList();
            int blocked = 0, atRisk = 0;
            foreach (var j in openJobs)
            {
                try
                {
                    var r = await _readiness.CheckOrderReadinessAsync(j.Id, ct);
                    if (r.IsSuccess && r.Value is { } rd)
                    {
                        if (rd.FailCount > 0) blocked++;
                        else if (rd.WarningCount > 0) atRisk++;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Promise: readiness check failed for PRO {Id}; skipping.", j.Id);
                }
            }
            if (blocked > 0)
                reasons.Add(new PromiseReason(PromiseReasonCode.MaterialShortage, PromiseStatus.Red,
                    $"{blocked} released job(s) have a blocking readiness failure (material shortage or operation not ready)."));
            else if (atRisk > 0)
                reasons.Add(new PromiseReason(PromiseReasonCode.JobBlocked, PromiseStatus.Yellow,
                    $"{atRisk} released job(s) are at risk on a readiness check."));
        }

        // ── 5) Change orders not approved (Yellow) ──
        if (!terminal)
        {
            int openAmend = await _db.ProjectAmendments
                .CountAsync(a => a.CustomerProjectId == customerProjectId
                    && (a.Status == ProjectAmendmentStatus.Draft || a.Status == ProjectAmendmentStatus.Submitted), ct);
            if (openAmend > 0)
                reasons.Add(new PromiseReason(PromiseReasonCode.ChangeOrderNotApproved, PromiseStatus.Yellow,
                    $"{openAmend} change order(s) are pending customer approval."));
        }

        // Verdict = worst severity; Green when nothing flags (and a schedule exists).
        var status = reasons.Count == 0 ? PromiseStatus.Green : reasons.Max(r => r.Severity);
        var ordered = reasons.OrderByDescending(r => r.Severity).ToList();
        var headline = BuildHeadline(p.Code, status, ordered);

        return Result.Success(new ProjectPromiseAssessment(
            p.Id, p.Code, p.Name, status, headline, ordered));
    }

    public async Task<Result<ProjectPromiseAssessment>> EvaluateByRefAsync(
        string projectRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectRef))
            return Result.Failure<ProjectPromiseAssessment>("Which project? Say a project code or id.");

        var raw = projectRef.Trim();
        int? pid = null;
        if (int.TryParse(raw, out var asId) && asId > 0)
            pid = await _db.CustomerProjects
                .Where(x => x.Id == asId && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);

        if (pid is null)
        {
            pid = await _db.CustomerProjects
                .Where(x => x.Code == raw && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            pid ??= await _db.CustomerProjects
                .Where(x => x.Code != null && x.Code.StartsWith(raw) && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        }

        if (pid is null or 0)
            return Result.Failure<ProjectPromiseAssessment>($"I couldn't find a project matching '{raw}' in your scope.");

        return await EvaluateAsync(pid.Value, ct);
    }

    private static string BuildHeadline(string code, PromiseStatus status, IReadOnlyList<PromiseReason> reasons)
    {
        var verdict = status switch
        {
            PromiseStatus.Green => "on track to hit the customer promise",
            PromiseStatus.Yellow => "at risk on the customer promise",
            PromiseStatus.Red => "unlikely to hit the customer promise",
            PromiseStatus.Black => "has already missed the customer promise",
            _ => "status unknown",
        };
        if (reasons.Count == 0)
            return $"Project {code} is {verdict}.";
        var top = reasons.Take(2).Select(r => r.Detail);
        return $"Project {code} is {verdict}: " + string.Join(" ", top);
    }
}
