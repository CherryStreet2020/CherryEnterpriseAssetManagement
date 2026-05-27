// Sprint 14.4 PR-1 (2026-05-27) — Production Cost Transaction ledger.
//
// THE atomic cost event. Every material issue, labor entry, overhead
// application, scrap posting, transfer, and adjustment creates exactly
// one CostTransaction row. The cost engine reads these to compute
// rollups, variances, and WIP balances.
//
// Architecture: Cost Object Graph per Dean's research spec
// (docs/research/production-costing-cost-rollup-dean-research.txt):
//   - Every PRO/WO/Job owns its own actual cost
//   - Parent consumes child cost via CostTransfer (Layer B)
//   - Child internal detail is drilldown only (Layer C)
//   - RollupAdditiveFlag prevents double-counting
//
// Cross-site: SiteId on every transaction enables per-site WIP,
// overhead rates, and GL posting via AccountingKey 8-segment.
//
// Nested jobs: ParentCostObjectId + ChildCostObjectId on CostTransfer
// track the supply relationship. RollupAdditiveFlag = false on child
// detail when viewed from parent boundary.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Masters;

namespace Abs.FixedAssets.Models.Production;

// ═══════════════════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════════════════

/// <summary>What type of entity owns the cost.</summary>
public enum CostObjectType
{
    ProductionOrder = 0,
    ChildWorkOrder = 1,
    Operation = 2,
    OutsideOperation = 3,
    ReworkOrder = 4,
    CustomerProject = 5,
    SalesOrderLine = 6,
    NcrMrb = 7,
    WarrantyOrder = 8,
    ServiceOrder = 9,
}

/// <summary>What kind of cost event occurred.</summary>
public enum CostTransactionType
{
    // Material (10-series)
    MaterialIssue = 10,
    MaterialIssueAll = 11,
    MaterialIssueKit = 12,
    MaterialPartialIssue = 13,
    MaterialOverIssue = 14,
    MaterialReturn = 15,
    MaterialReverseIssue = 16,
    MaterialScrapComponent = 17,
    MaterialSubstitute = 18,
    PurchasedToJobReceipt = 19,

    // Labor (20-series)
    DirectLabor = 20,
    SetupLabor = 21,
    IndirectLabor = 22,
    ReworkLabor = 23,
    InspectionLabor = 24,
    ProgrammingLabor = 25,
    EngineeringLabor = 26,

    // Machine (30-series)
    MachineTime = 30,
    MachineSetup = 31,

    // Overhead (40-series)
    LaborBurden = 40,
    MachineBurden = 41,
    ManufacturingOverhead = 42,
    FixedOverheadAbsorption = 43,
    VariableOverheadAbsorption = 44,

    // Outside processing (50-series)
    OutsideProcessing = 50,
    SubcontractService = 51,

    // Landed cost (60-series)
    FreightIn = 60,
    DutyTariff = 61,
    Brokerage = 62,
    NonRecoverableTax = 63,

    // Tooling (70-series)
    ToolingConsumed = 70,
    ToolingAmortized = 71,

    // Quality / packaging (80-series)
    QualityInspection = 80,
    TestCertification = 81,
    PackagingCrating = 82,

    // Scrap / rework (90-series)
    ScrapAbsorbToJob = 90,
    ScrapToAccount = 91,
    ScrapCustomerCharge = 92,
    ScrapVendorChargeback = 93,
    ReworkMaterial = 94,
    ReworkLaborCost = 95,
    ReworkOverhead = 96,

    // Transfers & settlements (100-series)
    ChildSupplyTransfer = 100,
    InventoryIssue = 101,
    WipTransferBetweenOps = 102,
    CompletionToFg = 103,
    DirectShipToCogs = 104,
    VarianceSettlement = 105,
    InterSiteTransfer = 106,

    // Adjustments (200-series)
    CostAdjustment = 200,
    InvoiceVariance = 201,
    PurchasePriceVariance = 202,
    ExchangeRateAdjustment = 203,
    PostCloseCorrection = 204,
}

/// <summary>Higher-level cost grouping for summary views.</summary>
public enum ProductionCostBucket
{
    DirectMaterial = 0,
    PurchasedToJob = 1,
    ChildSupply = 2,
    DirectLabor = 3,
    MachineTime = 4,
    LaborBurden = 5,
    MachineBurden = 6,
    ManufacturingOverhead = 7,
    OutsideProcessing = 8,
    Subcontract = 9,
    Tooling = 10,
    LandedCost = 11,
    Quality = 12,
    Scrap = 13,
    Rework = 14,
    Packaging = 15,
    Engineering = 16,
    Adjustment = 17,
    Variance = 18,
}

/// <summary>Cost status for PRO close workflow.</summary>
public enum ProductionCostStatus
{
    Estimated = 0,
    InWip = 1,
    PartiallyComplete = 2,
    AwaitingSupplierInvoice = 3,
    AwaitingChildCost = 4,
    AwaitingOverhead = 5,
    CostExceptions = 6,
    ReadyToClose = 7,
    ClosedSettled = 8,
    ReopenedForAdjustment = 9,
}

// ═══════════════════════════════════════════════════════════════════
// COST TRANSACTION — the atomic cost event
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Atomic cost posting against a cost object. Every material issue, labor entry,
/// overhead application, scrap posting, transfer, and adjustment creates one row.
/// This is the production sub-ledger — GL journal entries are created separately
/// via IProductionCostPostingService.
/// </summary>
public class CostTransaction
{
    public int Id { get; set; }

    // ── Tenant ──────────────────────────────────────────────────
    public int? TenantId { get; set; }
    public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    // ── Cost object ownership ───────────────────────────────────
    public CostObjectType CostObjectType { get; set; }
    public int CostObjectId { get; set; }

    // ── Transaction identity ────────────────────────────────────
    [MaxLength(64)]
    public string TransactionNumber { get; set; } = string.Empty;
    public CostTransactionType TransactionType { get; set; }
    public ProductionCostBucket CostBucket { get; set; }

    // ── Source traceability ─────────────────────────────────────
    /// <summary>E.g. "MaterialTransaction", "LaborEntry", "PurchaseReceipt"</summary>
    [MaxLength(64)]
    public string? SourceTransactionType { get; set; }
    public int? SourceTransactionId { get; set; }

    // ── Production context ──────────────────────────────────────
    public int? ProductionOrderId { get; set; }
    public int? OperationId { get; set; }
    public int? BomLineId { get; set; }
    public int? ItemId { get; set; }

    // ── Quantities ──────────────────────────────────────────────
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }
    [MaxLength(10)]
    public string? Uom { get; set; }

    // ── Cost ────────────────────────────────────────────────────
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ExtendedCost { get; set; }
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";
    [Column(TypeName = "decimal(18,6)")]
    public decimal? ExchangeRate { get; set; }

    // ── Cost classification ─────────────────────────────────────
    /// <summary>Maps to ItemStandardCostElement.CostElementType for variance analysis.</summary>
    public CostElementType CostElement { get; set; }

    // ── Inventory valuation method at time of posting ───────────
    public CostMethod? InventoryValuationMethod { get; set; }

    // ── Dates ───────────────────────────────────────────────────
    public DateTime EffectiveCostDate { get; set; }
    public DateTime? GlPostingDate { get; set; }

    // ── Capitalization / margin flags ───────────────────────────
    public bool CapitalizedToInventory { get; set; }
    public bool IncludedInJobMargin { get; set; } = true;
    public bool IncludedInProjectMargin { get; set; } = true;

    // ── THE anti-compounding field ──────────────────────────────
    /// <summary>
    /// When true, this row contributes to the parent total in a financial rollup.
    /// Child internal costs are additive inside the child but when viewed from the
    /// parent boundary, only the transfer event (Layer B) is additive.
    /// </summary>
    public bool RollupAdditiveFlag { get; set; } = true;

    // ── Transfer links ──────────────────────────────────────────
    public int? TransferEventId { get; set; }
    public int? ParentCostObjectId { get; set; }
    public CostObjectType? ParentCostObjectType { get; set; }
    public int? ChildCostObjectId { get; set; }
    public CostObjectType? ChildCostObjectType { get; set; }

    // ── Reversal ────────────────────────────────────────────────
    public bool IsReversal { get; set; }
    public int? ReversalOfTransactionId { get; set; }

    // ── Variance ────────────────────────────────────────────────
    [MaxLength(32)]
    public string? VarianceType { get; set; }

    // ── Lot/Serial traceability ─────────────────────────────────
    [MaxLength(50)]
    public string? LotNumber { get; set; }
    [MaxLength(50)]
    public string? SerialNumber { get; set; }
    [MaxLength(50)]
    public string? HeatNumber { get; set; }

    // ── GL integration ──────────────────────────────────────────
    public int? JournalEntryId { get; set; }
    public int? AccountingKeyId { get; set; }

    // ── Notes ───────────────────────────────────────────────────
    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Audit ───────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    // ── Concurrency (xmin) ──────────────────────────────────────
    public byte[]? RowVersion { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
// COST TRANSFER — movement of value between cost objects
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Records the movement of cost value from one cost object to another.
/// Layer B in the 3-layer model. Created when a child WO completes to
/// parent, when WIP settles to FG, or when cost is transferred between sites.
/// </summary>
public class CostTransfer
{
    public int Id { get; set; }

    // ── Tenant ──────────────────────────────────────────────────
    public int? TenantId { get; set; }
    public int CompanyId { get; set; }

    // ── Transfer identity ───────────────────────────────────────
    [MaxLength(64)]
    public string TransferNumber { get; set; } = string.Empty;

    // ── Source (child / sending) ─────────────────────────────────
    public CostObjectType SourceCostObjectType { get; set; }
    public int SourceCostObjectId { get; set; }
    public int? SourceSiteId { get; set; }

    // ── Destination (parent / receiving) ─────────────────────────
    public CostObjectType DestinationCostObjectType { get; set; }
    public int DestinationCostObjectId { get; set; }
    public int? DestinationSiteId { get; set; }

    // ── Transfer details ────────────────────────────────────────
    [Column(TypeName = "decimal(18,4)")]
    public decimal TransferQuantity { get; set; }
    [MaxLength(10)]
    public string? Uom { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TransferUnitCost { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal TransferExtendedCost { get; set; }
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    // ── Cost breakdown of transferred value ─────────────────────
    [Column(TypeName = "decimal(18,4)")]
    public decimal MaterialCostTransferred { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal LaborCostTransferred { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal OverheadCostTransferred { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal SubcontractCostTransferred { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal OtherCostTransferred { get; set; }

    // ── Transfer type ───────────────────────────────────────────
    public CostTransferType TransferType { get; set; }

    // ── Finality ────────────────────────────────────────────────
    public bool IsProvisional { get; set; }
    public bool IsFinal { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }

    // ── Reversal ────────────────────────────────────────────────
    public bool IsReversal { get; set; }
    public int? ReversalOfTransferId { get; set; }

    // ── GL integration ──────────────────────────────────────────
    public int? JournalEntryId { get; set; }

    // ── Notes ───────────────────────────────────────────────────
    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Audit ───────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    // ── Concurrency (xmin) ──────────────────────────────────────
    public byte[]? RowVersion { get; set; }
}

/// <summary>What kind of cost transfer.</summary>
public enum CostTransferType
{
    ChildCompletionToParent = 0,
    ChildIssueToParent = 1,
    SubassemblyToParent = 2,
    WipTransferBetweenOps = 3,
    WipToFinishedGoods = 4,
    DirectShipToCogs = 5,
    InterSiteTransfer = 6,
    ProjectCostTransfer = 7,
    VarianceSettlement = 8,
    CostAdjustment = 9,
}

// ═══════════════════════════════════════════════════════════════════
// PRODUCTION ORDER COST SUMMARY — denormalized for fast cockpit
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Denormalized cost summary per production order. Stamped by the cost
/// rollup engine after each cost event. Provides the data behind the
/// cost cockpit on the PRO Cockpit UI.
/// </summary>
public class ProductionOrderCostSummary
{
    public int Id { get; set; }

    // ── Identity ────────────────────────────────────────────────
    public int? TenantId { get; set; }
    public int CompanyId { get; set; }
    public int ProductionOrderId { get; set; }

    // ── Cost status ─────────────────────────────────────────────
    public ProductionCostStatus CostStatus { get; set; }

    // ── Estimated costs (baseline, never overwritten) ───────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedMaterialCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedLaborCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedMachineCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedBurdenCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedOutsideProcessingCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedSubcontractCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedFreightLandedCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedToolingCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedScrapReworkCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedTotalCost { get; set; }

    // ── Actual costs (from CostTransaction rollup) ──────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualMaterialCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualLaborCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualMachineCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualBurdenCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualOutsideProcessingCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualSubcontractCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualFreightLandedCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualToolingCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualScrapReworkCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualTotalCost { get; set; }

    // ── Committed costs (open POs, planned child jobs) ──────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal OpenCommittedCost { get; set; }

    // ── Forecast / EAC ──────────────────────────────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal ForecastRemainingCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimateAtCompletion { get; set; }

    // ── Variance ────────────────────────────────────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostVariance { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal MarginImpact { get; set; }

    // ── WIP / completion / settlement ───────────────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal WipBalance { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CompletedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ClosedSettledValue { get; set; }

    // ── Parent-child transfer tracking ──────────────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal ParentCostTransferIn { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ChildCostTransferOut { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal NonAdditiveChildDetailTotal { get; set; }

    // ── Per-unit ────────────────────────────────────────────────
    [Column(TypeName = "decimal(18,4)")]
    public decimal? CostPerGoodUnit { get; set; }
    public decimal? GoodQuantityCompleted { get; set; }
    public decimal? ScrapQuantityTotal { get; set; }

    // ── Rollup metadata ─────────────────────────────────────────
    public DateTime? LastRollupTimestamp { get; set; }
    [MaxLength(32)]
    public string? RollupStatus { get; set; }
    public int CostExceptionCount { get; set; }

    // ── Audit ───────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    // ── Concurrency (xmin) ──────────────────────────────────────
    public byte[]? RowVersion { get; set; }
}
