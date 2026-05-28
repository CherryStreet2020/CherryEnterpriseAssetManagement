// Sprint 15.1 PR-2 (2026-05-28) — ProductionSupplyAllocation entity.
//
// THE M:M LINK between demand and supply.
//
// One ProductionSupplyDemand row can have multiple ProductionSupplyAllocation rows
// when:
//   - Partial supply: a PO line of 100 covers two demand lines of 50 each
//   - Multi-source supply: 30 from inventory + 70 from new PO
//   - Split shipments: one PO line gets received in 3 partial shipments
//
// One supply record (PO line / WO / reservation / transfer) can serve multiple
// demands (consolidated buying — common in Inventory-First and Manual policies).
//
// PurchaseOrderLineDemandLink (PR-3) is the more detailed PO-side link for
// purchase-specific allocations. This entity is the generic M:M.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// What kind of supply record is being allocated to this demand?
    /// </summary>
    public enum AllocationSupplyType
    {
        PurchaseOrderLine = 0,
        ChildProductionOrder = 1,
        InventoryReservation = 2,
        TransferOrder = 3,
        SubcontractShipment = 4,
        Floorstock = 5,
        CustomerSuppliedReceipt = 6,
        ConsignedConsumption = 7,
    }

    /// <summary>
    /// Allocation lifecycle status.
    /// </summary>
    public enum AllocationStatus
    {
        /// <summary>Allocation proposed by planner/buyer but not committed.</summary>
        Proposed = 0,
        /// <summary>Allocation active — supply is committed to this demand.</summary>
        Active = 1,
        /// <summary>Allocation partially consumed by receipts/issues.</summary>
        PartiallyConsumed = 2,
        /// <summary>Allocation fully consumed (demand satisfied by this allocation).</summary>
        FullyConsumed = 3,
        /// <summary>Allocation released — supply freed for another demand.</summary>
        Released = 4,
        /// <summary>Allocation cancelled (supply or demand cancelled).</summary>
        Cancelled = 5,
    }

    public class ProductionSupplyAllocation
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────────── Demand side ───────────────────────────

        [Required]
        public int ProductionSupplyDemandId { get; set; }
        public ProductionSupplyDemand? ProductionSupplyDemand { get; set; }

        // ──────────────────────────── Supply side ───────────────────────────

        [Required]
        public AllocationSupplyType SupplyType { get; set; } = AllocationSupplyType.PurchaseOrderLine;

        /// <summary>Polymorphic FK to the supply record. Interpreted per SupplyType.</summary>
        public int SupplyRecordId { get; set; }

        /// <summary>Optional sub-line id (e.g., PO line release, child WO operation).</summary>
        public int? SupplyRecordLineId { get; set; }

        // Typed convenience FKs (when SupplyType identifies an entity in this DB)
        public int? PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        public int? ChildProductionOrderId { get; set; }
        public ProductionOrder? ChildProductionOrder { get; set; }

        // ──────────────────────────── Quantity ──────────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        public decimal AllocatedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ConsumedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal RemainingQuantity { get; set; }

        // ──────────────────────────── Status / dates ────────────────────────

        public AllocationStatus Status { get; set; } = AllocationStatus.Proposed;

        public DateTime? PromiseDate { get; set; }

        public DateTime? FirstConsumedAtUtc { get; set; }

        public DateTime? FullyConsumedAtUtc { get; set; }

        public DateTime? ReleasedAtUtc { get; set; }

        // ──────────────────────────── Audit ─────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // xmin concurrency token
        public byte[]? RowVersion { get; set; }
    }
}
