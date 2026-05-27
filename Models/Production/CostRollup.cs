// Sprint 14.4 PR-3 (2026-05-28) — Cost Rollup Engine entities.
//
// Three entities per Dean's research spec §19-20:
//   CostRollupRun  — Rollup execution header (who, when, what mode, what graph)
//   CostRollupLine — Each cost line in the rollup output, tagged for presentation
//   CostRollupException — Missing rates, uncosted children, double-count risks
//
// Architecture: Cost Object Graph per Dean's 910-line spec
// (docs/research/production-costing-cost-rollup-dean-research.txt):
//   §7  — correct multi-level rollup logic
//   §8  — three-layer anti-compounding model (A=originating, B=transfer, C=presentation)
//   §9  — two valid rollup methods (Financial vs Exploded)
//   §13 — 16 exception types to catch
//   §20 — 8-step rollup algorithm
//
// The rollup engine builds a directed acyclic graph of cost objects,
// validates it (cycle detection + single ownership path), pulls
// originating costs (Layer A) and transfer costs (Layer B), classifies
// each line, calculates totals by mode, detects exceptions, and
// stores the run with all lines and exceptions for audit trail.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// ═══════════════════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════════════════

/// <summary>What mode the rollup was executed in.</summary>
public enum CostRollupMode
{
    /// <summary>Financial: parent total = own postings + child transfer values. §9 Method 1.</summary>
    Financial = 0,

    /// <summary>Exploded: ignores transfers, sums all originating costs across graph. §9 Method 2.</summary>
    Exploded = 1,

    /// <summary>Operational: actual vs estimated by job/operation.</summary>
    Operational = 2,

    /// <summary>Project: jobs + POs + engineering + install + warranty exposure.</summary>
    Project = 3,

    /// <summary>Quote-to-Actual: compare quote estimate to actual execution.</summary>
    QuoteToActual = 4,

    /// <summary>Cost-to-Complete: actual + committed + remaining forecast.</summary>
    CostToComplete = 5,

    /// <summary>Margin: revenue minus EAC.</summary>
    Margin = 6,
}

/// <summary>How each line in the rollup output should be treated for totaling.</summary>
public enum CostRollupLineClassification
{
    /// <summary>Included in displayed total. Layer A originating cost at owning level.</summary>
    Additive = 0,

    /// <summary>Layer B transfer — additive at receiving object boundary.</summary>
    Transfer = 1,

    /// <summary>Layer C — explanation only, not summed into parent total. Child detail.</summary>
    DrilldownOnly = 2,

    /// <summary>Reversal — deducted from totals.</summary>
    Reversal = 3,

    /// <summary>Variance settlement posting.</summary>
    Variance = 4,

    /// <summary>Cost not yet finalized (child still open, PO not invoiced).</summary>
    Provisional = 5,
}

/// <summary>What kind of cost exception was detected.</summary>
public enum CostExceptionType
{
    MaterialIssuedZeroCost = 0,
    LaborMissingRate = 1,
    MachineTimeMissingBurden = 2,
    PoReceivedNotInvoiced = 3,
    PoInvoiceVarianceNotAllocated = 4,
    ChildWoCompletedNotCosted = 5,
    ChildDetailCountedTwice = 6,
    ScrapNoReasonOrAccounting = 7,
    ReworkNotLinkedToSource = 8,
    OutsideProcessingNoCost = 9,
    FreightLandedNotAllocated = 10,
    NegativeWip = 11,
    ParentClosedBeforeChildFinalized = 12,
    CostPostedAfterClose = 13,
    CurrencyMismatch = 14,
    UomMismatch = 15,
    QuantityExceedsCostedSupply = 16,
    CycleDetected = 17,
    OrphanedCostObject = 18,
}

/// <summary>Severity of a cost exception.</summary>
public enum CostExceptionSeverity
{
    /// <summary>Informational — flag but do not block.</summary>
    Info = 0,

    /// <summary>Warning — cost may be inaccurate.</summary>
    Warning = 1,

    /// <summary>Error — cost IS inaccurate, blocks close.</summary>
    Error = 2,

    /// <summary>Critical — data integrity risk.</summary>
    Critical = 3,
}

/// <summary>Status of a rollup run.</summary>
public enum CostRollupRunStatus
{
    Running = 0,
    Completed = 1,
    CompletedWithExceptions = 2,
    Failed = 3,
}

// ═══════════════════════════════════════════════════════════════════
// COST ROLLUP RUN — execution header
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Records one execution of the cost rollup engine. Captures the mode,
/// root cost object, graph depth, timing, exception count, and totals.
/// Audit trail for cost accuracy questions: "when was this last rolled up?"
/// </summary>
public class CostRollupRun
{
    public int Id { get; set; }

    // ── Tenant ──────────────────────────────────────────────────
    public int? TenantId { get; set; }
    public int CompanyId { get; set; }

    // ── Run identity ────────────────────────────────────────────
    [MaxLength(64)]
    public string RunNumber { get; set; } = string.Empty;

    // ── What was rolled up ──────────────────────────────────────
    public CostRollupMode Mode { get; set; }
    public CostObjectType RootCostObjectType { get; set; }
    public int RootCostObjectId { get; set; }

    /// <summary>Production order ID if root is a PRO.</summary>
    public int? ProductionOrderId { get; set; }

    // ── Graph metrics ───────────────────────────────────────────
    /// <summary>Number of cost objects in the graph.</summary>
    public int GraphNodeCount { get; set; }

    /// <summary>Number of edges (parent-child + transfer links).</summary>
    public int GraphEdgeCount { get; set; }

    /// <summary>Maximum depth of the graph from root.</summary>
    public int GraphMaxDepth { get; set; }

    // ── Results ─────────────────────────────────────────────────
    public int LineCount { get; set; }
    public int ExceptionCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public CostRollupRunStatus Status { get; set; }

    // ── Computed totals ─────────────────────────────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAdditiveCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalTransferCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDrilldownCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalExplodedCost { get; set; }

    // ── 5-element breakdown of additive total ───────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal MaterialTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LaborTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OverheadTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubcontractTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherTotal { get; set; }

    // ── Timing ──────────────────────────────────────────────────
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public int? DurationMs { get; set; }

    // ── Error detail ────────────────────────────────────────────
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    // ── Audit ───────────────────────────────────────────────────
    [MaxLength(100)]
    public string? ExecutedBy { get; set; }

    // ── Concurrency (xmin) ──────────────────────────────────────
    public byte[]? RowVersion { get; set; }

    // ── Navigation ──────────────────────────────────────────────
    public ICollection<CostRollupLine>? Lines { get; set; }
    public ICollection<CostRollupException>? Exceptions { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
// COST ROLLUP LINE — each cost line in the rollup output
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// One line in a rollup output. Tagged with classification (additive,
/// transfer, drilldown-only, reversal, variance, provisional) so the
/// UI can render the cost tree with correct totaling semantics.
/// </summary>
public class CostRollupLine
{
    public int Id { get; set; }

    // ── Parent run ──────────────────────────────────────────────
    public int CostRollupRunId { get; set; }
    public CostRollupRun? CostRollupRun { get; set; }

    // ── Position in tree ────────────────────────────────────────
    /// <summary>0 = root PRO, 1 = child of root, 2 = grandchild, etc.</summary>
    public int Depth { get; set; }

    /// <summary>Display order within the tree rendering.</summary>
    public int SortOrder { get; set; }

    // ── Cost object ─────────────────────────────────────────────
    public CostObjectType CostObjectType { get; set; }
    public int CostObjectId { get; set; }

    /// <summary>Parent cost object in the graph (null for root).</summary>
    public CostObjectType? ParentCostObjectType { get; set; }
    public int? ParentCostObjectId { get; set; }

    // ── Source ───────────────────────────────────────────────────
    /// <summary>If from a CostTransaction, this is the Id.</summary>
    public int? CostTransactionId { get; set; }

    /// <summary>If from a CostTransfer, this is the Id.</summary>
    public int? CostTransferId { get; set; }

    // ── Classification — THE key field for UI rendering ─────────
    public CostRollupLineClassification Classification { get; set; }

    // ── Cost bucket + type ──────────────────────────────────────
    public ProductionCostBucket CostBucket { get; set; }
    public CostTransactionType? TransactionType { get; set; }

    // ── Description ─────────────────────────────────────────────
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    // ── Amounts ─────────────────────────────────────────────────
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [MaxLength(10)]
    public string? Uom { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExtendedCost { get; set; }

    // ── 5-element breakdown ─────────────────────────────────────
    [Column(TypeName = "decimal(18,4)")]
    public decimal MaterialCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LaborCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OverheadCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal SubcontractCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OtherCost { get; set; }

    // ── Site tracking (cross-site dispersal) ────────────────────
    public int? SiteId { get; set; }

    // ── Flags ───────────────────────────────────────────────────
    public bool IsRollupAdditive { get; set; }
    public bool IsProvisional { get; set; }
    public bool IsFinal { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
// COST ROLLUP EXCEPTION — detected issues
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// An exception detected during rollup. Per Dean's research spec §13:
/// 16 exception types that must be caught. Severity determines whether
/// the exception blocks PRO close (Error/Critical) or is informational.
/// </summary>
public class CostRollupException
{
    public int Id { get; set; }

    // ── Parent run ──────────────────────────────────────────────
    public int CostRollupRunId { get; set; }
    public CostRollupRun? CostRollupRun { get; set; }

    // ── Exception identity ──────────────────────────────────────
    public CostExceptionType ExceptionType { get; set; }
    public CostExceptionSeverity Severity { get; set; }

    // ── Where ───────────────────────────────────────────────────
    public CostObjectType? CostObjectType { get; set; }
    public int? CostObjectId { get; set; }
    public int? ProductionOrderId { get; set; }
    public int? OperationId { get; set; }
    public int? BomLineId { get; set; }
    public int? ItemId { get; set; }
    public int? CostTransactionId { get; set; }

    // ── What ────────────────────────────────────────────────────
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Suggested resolution for the operator/controller.</summary>
    [MaxLength(500)]
    public string? Resolution { get; set; }

    // ── Impact ──────────────────────────────────────────────────
    /// <summary>Estimated cost impact of this exception (positive = understated, negative = overstated).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedImpact { get; set; }

    // ── Flags ───────────────────────────────────────────────────
    /// <summary>Whether this exception blocks PRO close.</summary>
    public bool BlocksClose { get; set; }

    /// <summary>Whether this exception has been acknowledged by a user.</summary>
    public bool Acknowledged { get; set; }

    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
}
