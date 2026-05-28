// Sprint 15.1 PR-3 (2026-05-28) — PurchaseOrderLineDemandLink entity.
//
// THE TRACEABILITY LINK between a PO line and the production-order demands
// that PO line satisfies.
//
// WHY THIS EXISTS:
// Buyers consolidate purchases — one PO line of "10,000 lbs steel bar"
// often covers multiple production-order BOM lines across multiple jobs.
// Without this entity, the consolidation erases per-job traceability:
// the receipt arrives, but which job got which lbs?
//
// This entity preserves the per-demand allocation even when PO lines are
// shared, so:
//   - Receipts can split to N PRO BOM lines
//   - AS9100 §8.3 traceability survives consolidation
//   - Project costing rolls up to the correct jobs
//   - PPV variance attributes to the right cost objects
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §15, §17
//   - docs/research/purchasing-cascade-design-2026-05-28.md PR-3
//   - Related: ProductionSupplyAllocation (Sprint 15.1 PR-2) — the generic
//     M:M allocation. This entity is the PO-side specialization with
//     consolidation semantics layered on top.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Status of this specific demand-link allocation.
    /// </summary>
    public enum PoDemandLinkStatus
    {
        /// <summary>Proposed by buyer/planner but not committed.</summary>
        Proposed = 0,
        /// <summary>Active link — PO line is committed to this demand line.</summary>
        Active = 1,
        /// <summary>Partial receipt has occurred against this allocation.</summary>
        PartiallyReceived = 2,
        /// <summary>Fully received — demand consumed.</summary>
        FullyReceived = 3,
        /// <summary>Link released — supply reassigned.</summary>
        Released = 4,
        /// <summary>Cancelled (PO line voided or demand cancelled).</summary>
        Cancelled = 5,
    }

    /// <summary>
    /// Links one PurchaseOrderLine to one ProductionSupplyDemand with an
    /// explicit allocated quantity. Many-to-many between PO lines and demands:
    /// one PO line covers many demands (consolidation), one demand drawn from
    /// many PO lines (multi-source supply).
    /// </summary>
    public class PurchaseOrderLineDemandLink
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── PO side ───────────────────────────────────
        [Required]
        public int PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        /// <summary>
        /// Optional PO release line — when the PO has multiple delivery
        /// releases each can be split across demands independently.
        /// </summary>
        public int? PurchaseOrderReleaseId { get; set; }
        public PurchaseOrderRelease? PurchaseOrderRelease { get; set; }

        // ──────────────────────── Demand side ───────────────────────────────
        [Required]
        public int ProductionSupplyDemandId { get; set; }
        public ProductionSupplyDemand? ProductionSupplyDemand { get; set; }

        /// <summary>
        /// Denormalized PRO Id — fast filtering "show me PO line links by
        /// production order". Synced on insert; immutable thereafter.
        /// </summary>
        public int ProductionOrderId { get; set; }
        public Production.ProductionOrder? ProductionOrder { get; set; }

        /// <summary>
        /// Denormalized BOM line Id from the demand — survives demand-side
        /// re-snapshots. Same access pattern as PRO Id.
        /// </summary>
        public int? BomLineId { get; set; }
        public ProductionMaterialStructure? BomLine { get; set; }

        /// <summary>Denormalized operation sequence for fast op-level queries.</summary>
        public int? OperationSequence { get; set; }

        // ──────────────────────── Quantity ──────────────────────────────────

        /// <summary>Quantity from this PO line earmarked for this demand.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal AllocatedQuantity { get; set; }

        /// <summary>Quantity from this PO line actually received against this demand.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReceivedQuantity { get; set; }

        /// <summary>AllocatedQuantity - ReceivedQuantity (computed at write time).</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal RemainingQuantity { get; set; }

        /// <summary>Unit price snapshotted at link time (PO line price can change).</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPriceAtLink { get; set; }

        // ──────────────────────── Status / dates ────────────────────────────

        public PoDemandLinkStatus Status { get; set; } = PoDemandLinkStatus.Proposed;

        [Display(Name = "Promised Date")]
        public DateTime? PromiseDate { get; set; }

        [Display(Name = "Need-By Date")]
        public DateTime? NeedByDate { get; set; }

        [Display(Name = "First Receipt UTC")]
        public DateTime? FirstReceiptUtc { get; set; }

        [Display(Name = "Fully Received UTC")]
        public DateTime? FullyReceivedUtc { get; set; }

        [Display(Name = "Released UTC")]
        public DateTime? ReleasedUtc { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // xmin concurrency
        public byte[]? RowVersion { get; set; }
    }
}
