// Theme B9 Wave 5 PR-12 (2026-05-30) — IProjectFinancialsService. The margin engine.
//
// Turns the Wave 2-4 spines into a live margin number:
//   EAC  = ActualCostToDate + EstimateToComplete
//   Margin = ContractValue − EAC
// CommittedCost is wired straight from the PR-10 procurement commitments (open
// balance of Open/PartiallyReceived commitments). ETC comes from the latest
// forecasts, else falls back to (BudgetTotal − ActualCostToDate).
//
// ADR-025: PageModels / voice read THROUGH this service. Tenant scope flows
// THROUGH the parent CustomerProject; every incoming FK on a write is scoped to
// the project's company. The EAC snapshot is immutable once written.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;   // CostElementType
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectFinancialsService
{
    /// <summary>The live margin position + budgets/forecasts/latest snapshot (read). THE margin engine.</summary>
    Task<Result<ProjectFinancialsView>> GetFinancialsAsync(int projectId, CancellationToken ct = default);

    Task<Result<int>> CreateBudgetAsync(CreateBudgetRequest req, CancellationToken ct = default);

    /// <summary>Add a budget line. Rejected once the budget is locked.</summary>
    Task<Result<int>> AddBudgetLineAsync(AddBudgetLineRequest req, CancellationToken ct = default);

    /// <summary>Lock a budget as the immutable cost baseline (set-once).</summary>
    Task<Result<ProjectBudget>> LockBudgetAsync(int budgetId, string? lockedBy = null, CancellationToken ct = default);

    /// <summary>Post an actual cost to the ledger. Blocked when the project is Cancelled.</summary>
    Task<Result<int>> PostActualCostAsync(PostActualCostRequest req, CancellationToken ct = default);

    Task<Result<int>> CreateForecastAsync(CreateForecastRequest req, CancellationToken ct = default);

    /// <summary>Freeze the current live position into an immutable EAC snapshot (the margin bridge).</summary>
    Task<Result<int>> SnapshotEacAsync(SnapshotEacRequest req, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectFinancialsView(
    int ProjectId,
    string Currency,
    decimal ContractValue,
    decimal BudgetTotal,
    decimal ActualCostToDate,
    decimal CommittedCost,
    decimal EstimateToComplete,
    decimal EstimateAtCompletion,
    decimal ProjectedMargin,
    decimal? ProjectedMarginPercent,
    decimal? PercentComplete,
    int? BaselineBudgetId,
    IReadOnlyList<ElementPositionRow> ByElement,
    IReadOnlyList<BudgetRow> Budgets,
    IReadOnlyList<ForecastRow> Forecasts,
    EacSnapshotRow? LatestSnapshot);

public sealed record ElementPositionRow(
    CostElementType CostElementType, decimal Budget, decimal Actual, decimal Variance);

public sealed record BudgetRow(
    int Id, string Code, string Name, ProjectBudgetType BudgetType,
    ProjectBudgetStatus Status, bool IsLocked, decimal LineTotal);

public sealed record ForecastRow(
    int Id, CostElementType CostElementType, ForecastMethod Method,
    decimal? EstimateToComplete, decimal? EstimateAtCompletion, System.DateTime ForecastDate);

public sealed record EacSnapshotRow(
    int Id, System.DateTime SnapshotDate, string? SnapshotReason,
    decimal ContractValue, decimal BudgetTotal, decimal ActualCostToDate, decimal CommittedCost,
    decimal EstimateToComplete, decimal EstimateAtCompletion, decimal ProjectedMargin, decimal? ProjectedMarginPercent);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateBudgetRequest(
    int CustomerProjectId,
    string Code,
    string Name,
    string? Description = null,
    ProjectBudgetType BudgetType = ProjectBudgetType.Working,
    string? Currency = null,
    int SortOrder = 0,
    string? CreatedBy = null);

public sealed record AddBudgetLineRequest(
    int ProjectBudgetId,
    int LineNo,
    CostElementType CostElementType,
    decimal BudgetAmount,
    string? Description = null,
    decimal? Quantity = null,
    decimal? UnitCost = null,
    int? ProjectPhaseId = null,
    string? CreatedBy = null);

public sealed record PostActualCostRequest(
    int CustomerProjectId,
    CostElementType CostElementType,
    decimal Amount,
    System.DateTime PostingDate,
    ActualCostSource SourceType = ActualCostSource.Manual,
    int? SourceId = null,
    int? ProjectPhaseId = null,
    int? ProjectTaskId = null,
    string? PostingReference = null,
    string? Description = null,
    string? Currency = null,
    string? CreatedBy = null);

public sealed record CreateForecastRequest(
    int CustomerProjectId,
    CostElementType CostElementType,
    System.DateTime ForecastDate,
    ForecastMethod Method = ForecastMethod.ManualEac,
    decimal? EstimateToComplete = null,
    decimal? EstimateAtCompletion = null,
    int? ProjectBudgetId = null,
    int? ProjectPhaseId = null,
    string? Currency = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record SnapshotEacRequest(
    int CustomerProjectId,
    string? SnapshotReason = null,
    int? ProjectBudgetId = null,
    string? CreatedBy = null);
