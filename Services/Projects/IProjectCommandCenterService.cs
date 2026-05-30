// Theme B9 Wave 1 PR-1 (2026-05-30) — IProjectCommandCenterService.
//
// THE BIC MONEY-SHOT (read side). The Project Command Center answers, on ONE
// screen, the questions a project manager / exec actually asks: what did the
// customer buy, what changed, what are we building, what's late, what will margin
// be, who owns the next action (spec §22.4). Read-only aggregation over the
// CustomerProject substrate that exists today — linked ProductionOrders (via
// ProductionOrder.CustomerProjectId), phases, amendments (the change log), and the
// EVM rollup fields — with honest typed empty-states for the areas that land in
// later B9 waves (quote, procurement, billing, quality blockers).
//
// Separation of concerns mirrors IProductionCockpitService vs the domain mutation
// services: THIS service only reads/aggregates; all CustomerProject mutations stay
// in ICustomerProjectService. ADR-025 — the AppDbContext reads live here, the page
// only maps the DTO.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

// ── Rollups ──────────────────────────────────────────────────────────

/// <summary>Linked-job execution rollup (from ProductionOrder.CustomerProjectId).</summary>
public sealed record ProjectJobRollup(
    int Total,
    int Open,        // not Completed/Closed/Cancelled
    int Completed,   // Completed or Closed
    int OnHold,
    IReadOnlyList<ProjectJobStatusCount> ByStatus);

public sealed record ProjectJobStatusCount(string Status, int Count);

/// <summary>Contract-change exposure rollup (from ProjectAmendment).</summary>
public sealed record ProjectAmendmentRollup(
    int Total,
    int Open,                 // Draft + Submitted (not yet customer-countersigned)
    int Approved,
    decimal ApprovedValueDelta,   // contributes to the effective contract value
    decimal PendingValueDelta);   // open exposure not yet approved

/// <summary>State of a command-center question's answer.</summary>
public enum CommandCenterAnswerState
{
    Answered = 0,   // resolved from current substrate
    Attention = 1,  // resolved AND flags a problem (late / over budget / open exposure)
    Pending = 2,    // the data area lands in a later B9 wave
}

/// <summary>One row of the "answer these immediately" command-center panel (spec §22.4).</summary>
public sealed record CommandCenterQuestion(
    string Question,
    string Answer,
    CommandCenterAnswerState State,
    string? WiredInWave = null);  // e.g. "Wave 2" when State == Pending

// ── Command Center bundle ────────────────────────────────────────────

public sealed record ProjectCommandCenterData(
    // Identity
    int ProjectId,
    string Code,
    string Name,
    CustomerProjectStatus Status,
    CustomerProjectMode Mode,
    string? CustomerName,
    string? ProjectManagerName,
    string? CustomerPoNumber,
    ContractType? ContractType,
    QualityProgram? QualityProgram,
    // Commercial
    decimal? ContractValue,
    decimal? EffectiveContractValue,   // ContractValue + approved amendment value deltas
    string Currency,
    // Schedule
    System.DateTime? TargetStartDate,
    System.DateTime? TargetEndDate,
    System.DateTime? ProjectedEndDate,
    int? DaysLateVsTarget,             // (ProjectedEndDate - TargetEndDate) in days; null if either missing
    // EVM / cost / margin
    decimal? EstimatedTotalCost,
    decimal? PercentComplete,
    System.DateTime? LastEvmRollupAt,
    decimal? ProjectedMargin,          // EffectiveContractValue - EstimatedTotalCost
    decimal? ProjectedMarginPct,
    // Execution
    ProjectJobRollup Jobs,
    int PhaseCount,
    // Change exposure
    ProjectAmendmentRollup Amendments,
    // Risk + AI
    short? RiskScore,
    RiskTone? RiskTone,
    string? AiSummaryText,
    System.DateTime? AiSummaryGeneratedAt,
    // The "answer these immediately" panel
    IReadOnlyList<CommandCenterQuestion> Questions);

public interface IProjectCommandCenterService
{
    /// <summary>
    /// Load the read-only command-center bundle for a customer project. Tenant-scoped;
    /// aggregates the project header + linked jobs + phases + amendments + EVM into the
    /// KPI band and the command-center questions panel. Fails when the project is not
    /// found or not visible to the current tenant.
    /// </summary>
    Task<Result<ProjectCommandCenterData>> GetCommandCenterAsync(
        int customerProjectId, CancellationToken ct = default);
}
