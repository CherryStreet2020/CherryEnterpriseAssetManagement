// Theme B9 Wave 5 PR-12 (2026-05-30) — Project financials / the margin engine.
//
// ProjectBudget / ProjectBudgetLine / ProjectActualCost / ProjectForecast /
// ProjectEACSnapshot — the cost-control layer that turns the Wave 2-4 spines
// (estimate, contract, procurement commitments, labor/expense actuals) into a
// live margin number: Contract − EAC, where EAC = actual-to-date + ETC.
//
// Cost is typed with the SAME B7 CostElementType used by ProjectEstimateLine
// (Material / Labor / Overhead / Subcontract / …) so budget ↔ estimate ↔ actual
// line up element-for-element (the quote-vs-actual tie-in lands in PR-13).
//
// Conventions (per ProjectResource / ProjectProcurement precedent): children
// tenant-scoped THROUGH the parent project; no CompanyId. Budget CASCADEs from
// the project; BudgetLine CASCADEs from its budget (single path
// project→budget→line); ActualCost / Forecast / EACSnapshot CASCADE from the
// project; every optional peg SET NULL. xmin; enum DB defaults == 0-member
// model default. ProjectEACSnapshot is IMMUTABLE once written.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abs.FixedAssets.Models.Masters;   // CostElementType (B7 estimate-as-standard tie-in)

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectBudget — a budget version for a project (Working / Baseline /
    // Revised). Locking a budget freezes it as the immutable cost baseline.
    // =====================================================================
    public class ProjectBudget
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProjectBudgetType BudgetType { get; set; } = ProjectBudgetType.Working;
        public ProjectBudgetStatus Status { get; set; } = ProjectBudgetStatus.Draft;

        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public int SortOrder { get; set; } = 0;

        // Set-once lock — once locked the lines are frozen (the cost baseline).
        public bool IsLocked { get; set; } = false;
        public DateTime? LockedAt { get; set; }
        [StringLength(100)]
        public string? LockedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public ICollection<ProjectBudgetLine> Lines { get; set; } = new List<ProjectBudgetLine>();

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectBudgetLine — a budgeted amount by cost element (+ optional phase).
    // Scoped THROUGH the parent budget (no CompanyId; CASCADE from budget).
    // =====================================================================
    public class ProjectBudgetLine
    {
        public int Id { get; set; }

        public int ProjectBudgetId { get; set; }
        public ProjectBudget? Budget { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int LineNo { get; set; }

        public CostElementType CostElementType { get; set; } = CostElementType.Material;

        [StringLength(500)]
        public string? Description { get; set; }

        public decimal? Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal BudgetAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectActualCost — a posted actual cost entry (the canonical cost
    // ledger). Provenance via SourceType + soft SourceId (no FK).
    // =====================================================================
    public class ProjectActualCost
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? ProjectTaskId { get; set; }
        public ProjectTask? ProjectTask { get; set; }

        public CostElementType CostElementType { get; set; } = CostElementType.Material;

        public ActualCostSource SourceType { get; set; } = ActualCostSource.Manual;
        // Soft reference to the source row (time entry / expense / receipt / …) — no FK.
        public int? SourceId { get; set; }

        [StringLength(128)]
        public string? PostingReference { get; set; }
        [StringLength(500)]
        public string? Description { get; set; }

        public decimal Amount { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime PostingDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectForecast — a cost-to-complete / estimate-at-completion input,
    // optionally by element + phase, for a budget version.
    // =====================================================================
    public class ProjectForecast
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectBudgetId { get; set; }
        public ProjectBudget? Budget { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public CostElementType CostElementType { get; set; } = CostElementType.Material;

        public ForecastMethod Method { get; set; } = ForecastMethod.ManualEac;

        // Estimate-to-complete (remaining) + estimate-at-completion (total).
        public decimal? EstimateToComplete { get; set; }
        public decimal? EstimateAtCompletion { get; set; }

        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime ForecastDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectEACSnapshot — IMMUTABLE point-in-time financial position (the
    // margin bridge). Frozen totals + a per-element JSON breakdown (text).
    // =====================================================================
    public class ProjectEACSnapshot
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectBudgetId { get; set; }
        public ProjectBudget? Budget { get; set; }

        public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
        [StringLength(200)]
        public string? SnapshotReason { get; set; }

        // Frozen position.
        public decimal ContractValue { get; set; }
        public decimal BudgetTotal { get; set; }
        public decimal ActualCostToDate { get; set; }
        public decimal CommittedCost { get; set; }
        public decimal EstimateToComplete { get; set; }
        public decimal EstimateAtCompletion { get; set; }
        public decimal? PercentComplete { get; set; }
        public decimal ProjectedMargin { get; set; }
        public decimal? ProjectedMarginPercent { get; set; }

        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        // Per-cost-element breakdown frozen as JSON text (read-back only).
        public string? FrozenBreakdownJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — the 0 member is the CLR/model default (== DB default).
    // ---------------------------------------------------------------------

    public enum ProjectBudgetType
    {
        Working = 0,
        Baseline = 1,
        Revised = 2,
        Forecast = 3
    }

    public enum ProjectBudgetStatus
    {
        Draft = 0,
        Approved = 1,
        Locked = 2,
        Superseded = 3,
        Cancelled = 4
    }

    public enum ActualCostSource
    {
        Manual = 0,
        TimeEntry = 1,
        Expense = 2,
        Receipt = 3,
        ProductionCost = 4,
        Journal = 5,
        Other = 6
    }

    public enum ForecastMethod
    {
        ManualEac = 0,
        RemainingBudget = 1,
        CpiBased = 2,
        RunRate = 3
    }
}
