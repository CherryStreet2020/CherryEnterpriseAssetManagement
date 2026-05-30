// Theme B9 Wave 1 PR-2 (2026-05-30) — IProjectPromiseService.
//
// THE "Can we still hit the customer promise?" indicator (spec §22.3) — the
// signal no incumbent surfaces from execution data. Reads schedule, linked-job
// state + readiness, open change orders, and EVM progress to return a single
// Green / Yellow / Red / Black verdict with the concrete reason codes the spec
// calls for (long-lead PO late, drawing not approved, job not released, operation
// behind, customer approval pending, material shortage, open blocking NCR, change
// order not approved). Read-only; tenant-scoped. Powers the Command Center badge
// and the Cherry Bar `ProjectPromiseStatus` voice intent.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Projects;

/// <summary>The promise verdict. Ordinal == severity so the worst signal wins.</summary>
public enum PromiseStatus
{
    Green = 0,   // On track
    Yellow = 1,  // At risk
    Red = 2,     // Promise unlikely
    Black = 3,   // Already missed
}

/// <summary>Why the promise is at risk — mapped to the spec §22.3 reason examples.</summary>
public enum PromiseReasonCode
{
    NoScheduleBaselined,
    AlreadyPastDue,
    ScheduleSlip,
    JobOnHold,
    JobNotReleased,
    JobBlocked,
    MaterialShortage,
    ChangeOrderNotApproved,
    BehindOnProgress,
}

public sealed record PromiseReason(PromiseReasonCode Code, PromiseStatus Severity, string Detail);

public sealed record ProjectPromiseAssessment(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    PromiseStatus Status,
    string Headline,                       // one-line spoken/printed summary
    IReadOnlyList<PromiseReason> Reasons);  // worst-first

public interface IProjectPromiseService
{
    /// <summary>Evaluate the customer-promise health for a project (tenant-scoped).</summary>
    Task<Result<ProjectPromiseAssessment>> EvaluateAsync(
        int customerProjectId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a project by free-text ref (numeric Id OR project Code, exact→prefix,
    /// tenant-scoped) then evaluate. For the `ProjectPromiseStatus` voice intent.
    /// </summary>
    Task<Result<ProjectPromiseAssessment>> EvaluateByRefAsync(
        string projectRef, CancellationToken ct = default);
}
