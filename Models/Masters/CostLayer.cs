// B6 Foundation Sprint PR-FS-4 (2026-05-26) — CostLayer entity.
//
// Inventory-valuation layer (SAP MM "stock with values" / Oracle Cost Layer /
// D365 inventTrans cost-stack equivalent). Each material receipt creates one
// immutable layer at the receipt unit cost. Each issue consumes from open
// layers per the costing policy (FIFO / LIFO / Weighted Average / Standard).
//
// Why this exists:
//   - **Sprint 14.4 cost-rollup engine** needs the cost basis for every issue.
//     With FIFO/LIFO, the unit cost charged to WIP depends on which physical
//     receipt the material came from. Cost-layer math is the deterministic
//     source of that basis.
//   - **Variance tracking**: the difference between layer cost (actual) and
//     standard cost (ItemStandardCostElement, PR-FS-3) is the purchase-price
//     variance + usage variance — both post to dedicated GL accounts via
//     PRA-7 PostingProfile + PRA-5b AccountingKey.
//   - **AS9100 / DCAA / IFRS auditability**: full lineage from FG cost back
//     to receipt PO line + vendor invoice + heat number. Cost layers are
//     the spine.
//   - **Make-or-buy decisions** (Theme B7): real-time average cost from
//     current layers vs. vendor quote is the decision input.
//
// Immutability invariants (enforced at the service layer):
//   - `ReceivedQuantity`, `UnitCost`, `ReceivedAtUtc`, `LayerNumber`, `LotNumber`
//     are SET ONCE at create and never mutated.
//   - `RemainingQuantity` is the ONLY mutable quantity — decreases monotonically
//     to zero. When it hits zero, `Status` flips to `Exhausted` + `ExhaustedAtUtc`
//     is stamped.
//   - `Status=Reversed` is set when an entire receipt is reversed (return-to-
//     vendor, receipt error). RemainingQuantity is restored to 0 (not to
//     ReceivedQuantity, because Reversed means "treat as never happened").
//
// Layer ordering for FIFO/LIFO consumption:
//   - FIFO: ORDER BY `ReceivedAtUtc` ASC, `LayerNumber` ASC.
//   - LIFO: ORDER BY `ReceivedAtUtc` DESC, `LayerNumber` DESC.
//   - Average: a single weighted-avg unit cost computed across all open layers
//     for the (Item, Site) pair; the consume operation decrements proportionally
//     and the resulting weighted-avg recomputes after each receipt.
//   - Standard: never consumed by cost-layer math; standard-cost items use
//     ItemStandardCostElement (PR-FS-3) directly and any layer/standard delta
//     is a variance. (CostLayer rows are still created for traceability.)
//
// Tenant trio + null-safe partial UNIQUE per [[reference_bic_entity_checklist]]
// and the lesson encoded in PR-FS-2's Codex P1 — applied prophylactically.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Models.Masters
{
    /// <summary>
    /// Source document type that created this layer. Drives the receipt-
    /// reversal flow and the GL posting via PRA-7 PostingProfile.
    /// </summary>
    public enum CostLayerReceiptType
    {
        /// <summary>Purchase Order receipt — typical inbound goods.</summary>
        PurchaseOrder = 0,

        /// <summary>Production Order completion — internally produced FG/SUBASSY.</summary>
        ProductionOrder = 1,

        /// <summary>Customer return / RMA receipt.</summary>
        CustomerReturn = 2,

        /// <summary>Subcontract return — outsourced operation finished and shipped back.</summary>
        SubcontractReturn = 3,

        /// <summary>Inventory transfer in (from another site / warehouse).</summary>
        TransferIn = 4,

        /// <summary>Inventory adjustment in (positive count / found stock).</summary>
        AdjustmentIn = 5,

        /// <summary>Opening balance — initial system load.</summary>
        OpeningBalance = 99,
    }

    /// <summary>
    /// Lifecycle status of a cost layer.
    /// </summary>
    public enum CostLayerStatus
    {
        /// <summary>Layer is active and has remaining quantity available for issue.</summary>
        Open = 0,

        /// <summary>Layer's RemainingQuantity reached zero through normal issue consumption.</summary>
        Exhausted = 1,

        /// <summary>Layer was reversed (return-to-vendor, receipt error). Not consumable.</summary>
        Reversed = 2,
    }

    /// <summary>
    /// Inventory valuation method used to consume cost layers. Matches the
    /// Item Master's <c>CostMethod</c> enum value at the time of receipt
    /// (per-Item or per-Site override via ItemSite or ItemStandardCostElement).
    /// </summary>
    public class CostLayer
    {
        public int Id { get; set; }

        // ===== Identity + tenant trio =====================================

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        /// <summary>
        /// Optional per-Site scope. NULL = company-level layer (cross-Site pool).
        /// Most real installs run per-Site valuation; null is valid for tenants
        /// that pool across Sites at the company level.
        /// </summary>
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public int? TenantId { get; set; }
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        // ===== Sequence (FIFO/LIFO tiebreaker) ============================

        /// <summary>
        /// Monotonically increasing per (Item, Site, TenantId). Used as the
        /// secondary order key after <c>ReceivedAtUtc</c> when two receipts
        /// share a timestamp (rare but possible on bulk imports).
        /// </summary>
        [Required]
        public long LayerNumber { get; set; }

        // ===== Receipt provenance =========================================

        [Required]
        [Display(Name = "Received At")]
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "Receipt Type")]
        public CostLayerReceiptType ReceiptType { get; set; }

        /// <summary>
        /// FK-like reference to the source document. Discriminated by
        /// <see cref="ReceiptType"/>: PurchaseOrder.Id for PO receipts,
        /// ProductionOrder.Id for production completions, etc. Stored as int
        /// (not a hard FK) to avoid 7 different conditional FK relationships.
        /// </summary>
        public int? ReceiptReferenceId { get; set; }

        [StringLength(50)]
        [Display(Name = "Receipt Document Number")]
        public string? ReceiptDocumentNumber { get; set; }

        // ===== Quantities ==================================================

        /// <summary>
        /// Quantity received at this layer. IMMUTABLE after create.
        /// </summary>
        [Display(Name = "Received Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReceivedQuantity { get; set; }

        /// <summary>
        /// Quantity still available for issue from this layer. Decrements
        /// monotonically through issue consumption. Reaches zero → Status flips
        /// to Exhausted. Reversed receipts: this drops to 0 immediately.
        /// </summary>
        [Display(Name = "Remaining Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal RemainingQuantity { get; set; }

        // ===== Cost ========================================================

        /// <summary>
        /// Per-unit cost locked at receipt time. IMMUTABLE. Multi-currency
        /// deferred to Sprint 16+; everything is base-currency (USD default).
        /// </summary>
        [Display(Name = "Unit Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitCost { get; set; }

        [Required, StringLength(3)]
        [Display(Name = "Currency")]
        public string CurrencyCode { get; set; } = "USD";

        // ===== Traceability ================================================

        [StringLength(50)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Heat Number")]
        public string? HeatNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Vendor Lot")]
        public string? VendorLot { get; set; }

        [StringLength(50)]
        [Display(Name = "Vendor Reference")]
        public string? VendorReference { get; set; }

        // ===== Lifecycle ===================================================

        [Required]
        public CostLayerStatus Status { get; set; } = CostLayerStatus.Open;

        [Display(Name = "Exhausted At")]
        public DateTime? ExhaustedAtUtc { get; set; }

        [Display(Name = "Reversed At")]
        public DateTime? ReversedAtUtc { get; set; }

        [StringLength(500)]
        [Display(Name = "Reversal Reason")]
        public string? ReversalReason { get; set; }

        // ===== Concurrency token ===========================================
        //
        // PR-FS-4 P1 fix (Codex on PR #360): RemainingQuantity is decremented
        // by ConsumeQuantityAsync — a classic read-then-write hot path. Two
        // concurrent consumes for the same (Item, Site) can both pass the
        // availability check then overwrite each other's decrement (lost
        // update) without optimistic concurrency.
        //
        // Concurrency token via Postgres xmin system column. Mapped in
        // AppDbContext via MapXminRowVersion (project convention, HARD LOCK
        // from PR #365). NEVER IsRowVersion()+bytea — PG can't auto-populate
        // bytea, every INSERT throws 23502. CostLayerService catches
        // DbUpdateConcurrencyException + retries (up to 3 times) by
        // re-reading the current layer state and re-applying the consume math.
        // PR-XminBackfill 2026-05-27: converted from IsRowVersion()+bytea
        // to xmin pattern (was latent 23502 bug on first INSERT).
        public byte[]? RowVersion { get; set; }

        // ===== Audit =======================================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
