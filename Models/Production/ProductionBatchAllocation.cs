using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — ProductionBatchAllocation.
    //
    // The cost-allocation join: how a ProductionBatch's cost splits
    // across the parent production orders / operations whose parts
    // were in the batch.
    //
    // Cardinality: many allocations per batch (one per consuming WO
    // operation). UNIQUE on (ProductionBatchId, WorkOrderOperationId)
    // — one allocation per operation per batch.
    //
    // Critical design call from research:
    //   AllocationMethod lives on ProductionBatch (parent), NOT on
    //   each allocation row. ASC 330 inventory-valuation consistency
    //   demands one systematic method per batch. The allocation row
    //   carries the *measurement* (basis), the *resulting share*
    //   (pct, derived), and the *resulting amount* (cost). Never the
    //   method choice.
    //
    // What "basis" means by method:
    //   PerPiece           -> AllocationBasis = piece count
    //   PerArea            -> AllocationBasis = nested area (mm^2)
    //   PerSurfaceArea     -> AllocationBasis = part surface area (mm^2)
    //   PerWeight          -> AllocationBasis = part weight (kg)
    //   PerLoadMass        -> AllocationBasis = mass contribution (kg)
    //   PerCycle           -> AllocationBasis = cycle count
    //   PerDollar          -> AllocationBasis = part dollar value
    //   PerLinealLength    -> AllocationBasis = cut length (mm)
    //
    // AllocationPct is computed: basis / SUM(basis across allocations).
    // Stored for fast reporting but is a derived view; on re-allocation
    // it must be recomputed for the whole batch.
    //
    // ProductionOrderId is denormalized off the WorkOrderOperation
    // for fast aggregate queries ("total batch cost allocated to PO X")
    // and is kept in sync at allocation-write time.
    //
    // ProductionOrderOperationId is reserved for ADR-014 when production
    // orders get their own operations table. Nullable now.
    //
    // Reference: PR #119.13a research report Q6 (ASC 330) + Q8
    // (allocation method enum canonicalization).
    [Table("ProductionBatchAllocations")]
    public class ProductionBatchAllocation
    {
        public int Id { get; set; }

        public int ProductionBatchId { get; set; }
        public ProductionBatch? ProductionBatch { get; set; }

        // The consuming operation. Maintenance / quality / engineering
        // WO operations don't typically consume batches; production
        // ones do. But we don't constrain at the table level — the
        // CASCADE from the operation handles cleanup.
        public int WorkOrderOperationId { get; set; }
        public WorkOrderOperation? WorkOrderOperation { get; set; }

        // Denormalized for fast aggregate queries. Kept in sync at
        // allocation-write time.
        public int? ProductionOrderId { get; set; }

        // Reserved for ADR-014 — production orders will get their own
        // operations table at some point. Nullable until then.
        public int? ProductionOrderOperationId { get; set; }

        // The measurement (see file-level comment for what this means
        // by method). Always non-negative.
        [Display(Name = "Allocation Basis")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal AllocationBasis { get; set; }

        // Computed share: basis / SUM(basis). Stored for fast reporting
        // but rebuilt on re-allocation. 0-1.0.
        [Display(Name = "Allocation %")]
        [Column(TypeName = "decimal(7,4)")]
        public decimal AllocationPct { get; set; }

        // The resulting cost share. Set post-batch when actual cost is
        // known. Null until the batch completes + costs roll up.
        [Display(Name = "Allocated Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? AllocatedCost { get; set; }

        // Origin classification — distinguishes "this is a real order
        // allocation" from "this is a stock replenishment / rework /
        // synthetic allocation."
        public AllocationOrigin Origin { get; set; } = AllocationOrigin.WorkOrder;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }

    // ADR-013 / PR #119.13a — AllocationOrigin classification.
    //
    // Distinguishes a real-order allocation from synthetic rows that
    // exist for accounting reasons. Per SigmaNest / ProNest convention,
    // spare-to-stock and rework allocations get explicit origin tags
    // so cost rollups can include/exclude them.
    public enum AllocationOrigin
    {
        WorkOrder = 0,           // normal: allocates to a production-order operation
        StockReplenishment = 1,  // spare pieces go to inventory
        Rework = 2,              // re-runs against an existing order
        Scrap = 3,               // scrap-cost write-off
    }
}
