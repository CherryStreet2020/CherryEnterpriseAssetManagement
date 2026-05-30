// Theme B9 Wave 3 PR-7 (2026-05-30) — IProjectWbsService.
//
// Hardens the self-nesting ProjectPhase tree into a controllable WBS backbone:
//   - weighted roll-up of cost (planned/actual/committed/forecast) + %-complete,
//   - the 100%-rule validator (children weights sum to 100 under every parent
//     and across roots), and
//   - BaselineProjectWbs — the §20 gate: cannot baseline unless every LEAF has a
//     Responsible Owner AND a cost bucket (PlannedCost) AND the 100%-rule holds.
//     The baseline stamp is set-once (re-baseline requires an explicit flag).
//
// ADR-025: PageModels / voice read through THIS service, never AppDbContext.
// Tenant scope flows THROUGH the parent CustomerProject (ProjectPhase has no
// CompanyId of its own).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectWbsService
{
    /// <summary>
    /// The full WBS tree for a project with weighted roll-up of cost and
    /// percent-complete onto parent nodes (the 100%-rule view).
    /// </summary>
    Task<Result<ProjectWbsRollup>> GetWbsRollupAsync(int projectId, CancellationToken ct = default);

    /// <summary>
    /// Set WBS attributes on a single phase (owner, cost bucket, weight,
    /// forecast/actual schedule, status). Null fields are left unchanged.
    /// </summary>
    Task<Result<ProjectPhase>> UpdatePhaseWbsAsync(UpdatePhaseWbsRequest req, CancellationToken ct = default);

    /// <summary>
    /// Validate the 100%-rule: under every parent (and across the project's
    /// roots) the children's WeightPercent must sum to 100 (±0.01).
    /// </summary>
    Task<Result<HundredPercentRuleResult>> ValidateHundredPercentRuleAsync(int projectId, CancellationToken ct = default);

    /// <summary>
    /// Baseline the project's WBS. Gates (B9 §20): the project has at least
    /// one phase; every leaf has a Responsible Owner AND a PlannedCost; the
    /// 100%-rule holds. Set-once — fails if already baselined unless
    /// <c>AllowRebaseline</c> is set. On success stamps IsBaselined +
    /// BaselinedAt/By on every phase and freezes Baseline dates from Forecast.
    /// </summary>
    Task<Result<BaselineWbsOutcome>> BaselineProjectWbsAsync(BaselineProjectWbsRequest req, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

/// <summary>Project-level WBS roll-up with the node tree.</summary>
public sealed record ProjectWbsRollup(
    int ProjectId,
    bool IsBaselined,
    decimal TotalPlannedCost,
    decimal TotalActualCost,
    decimal TotalCommittedCost,
    decimal TotalForecastCost,
    decimal RolledPercentComplete,
    IReadOnlyList<WbsNode> Roots);

/// <summary>One WBS node with roll-up totals folded in.</summary>
public sealed class WbsNode
{
    public int Id { get; init; }
    public int? ParentPhaseId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public WbsType WbsType { get; init; }
    public int WbsLevel { get; init; }
    public ProjectPhaseStatus Status { get; init; }
    public string? ResponsibleOwner { get; init; }
    public string? ControlAccount { get; init; }
    public decimal? WeightPercent { get; init; }
    public bool CustomerVisible { get; init; }
    public bool IsBaselined { get; init; }
    public bool IsLeaf { get; init; }

    // Roll-up: leaves carry their own value; parents carry the sum of children.
    public decimal PlannedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal CommittedCost { get; set; }
    public decimal ForecastCost { get; set; }
    public decimal PercentComplete { get; set; }

    public List<WbsNode> Children { get; } = new();
}

/// <summary>Outcome of the 100%-rule validator.</summary>
public sealed record HundredPercentRuleResult(
    bool Satisfied,
    IReadOnlyList<WeightGroupViolation> Violations);

/// <summary>A parent (or the project root scope) whose children weights ≠ 100.</summary>
public sealed record WeightGroupViolation(
    int? ParentPhaseId,
    string Scope,
    decimal WeightSum);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

/// <summary>Inputs for <see cref="IProjectWbsService.UpdatePhaseWbsAsync"/>. Null = leave unchanged.</summary>
public sealed record UpdatePhaseWbsRequest(
    int PhaseId,
    WbsType? WbsType = null,
    string? ResponsibleOwner = null,
    string? ResponsibleDepartment = null,
    string? ControlAccount = null,
    decimal? PlannedCost = null,
    decimal? ActualCost = null,
    decimal? CommittedCost = null,
    decimal? ForecastCost = null,
    decimal? WeightPercent = null,
    decimal? PercentComplete = null,
    System.DateTime? ForecastStart = null,
    System.DateTime? ForecastFinish = null,
    System.DateTime? ActualStart = null,
    System.DateTime? ActualFinish = null,
    ProjectPhaseStatus? Status = null,
    bool? CustomerVisible = null,
    string? ModifiedBy = null);

/// <summary>Inputs for <see cref="IProjectWbsService.BaselineProjectWbsAsync"/>.</summary>
public sealed record BaselineProjectWbsRequest(
    int CustomerProjectId,
    bool AllowRebaseline = false,
    string? BaselinedBy = null);

/// <summary>Outcome of a successful baseline.</summary>
public sealed record BaselineWbsOutcome(
    int CustomerProjectId,
    int PhasesBaselined,
    int LeafCount,
    decimal TotalPlannedCost,
    bool WasRebaseline);
