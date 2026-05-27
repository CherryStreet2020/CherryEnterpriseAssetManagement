// B8 PR-PRO-3 (2026-05-27) — ProductionMaterialTransaction entity.
//
// THE TRANSACTION LOG for every material movement in the factory.
// Every Issue, Return, Transfer, Substitute, Split, and Scrap action
// against a frozen BOM line (ProductionMaterialStructure) creates
// one of these records. The BOM line's 10 execution quantity columns
// (IssuedQuantity, ConsumedQuantity, etc.) are the SUMMARY; this entity
// is the DETAIL.
//
// WHY A SEPARATE ENTITY (not just updating BOM line quantities):
//   1. AUDIT — AS9100 §8.5.2 / IATF 16949 §8.5.1.1 require
//      traceable material transaction records per discrete event.
//   2. COST — each transaction captures the actual unit cost at time
//      of movement, not the frozen standard cost on the BOM line.
//      The cost engine (Sprint 14.4) reads these for actual cost
//      accumulation + variance decomposition.
//   3. REVERSAL — every transaction except Scrap can be reversed by
//      creating a paired transaction with IsReversal = true and
//      OriginalTransactionId pointing to the source.
//   4. TRANSFER — job-to-job transfers create PAIRED transactions:
//      one TransferFromJob on the source PO + one TransferToJob on
//      the destination PO. The TransferPairId links them.
//   5. LOT GENEALOGY — lot/serial/heat/cert captured per-transaction,
//      not just on the BOM line summary. A single BOM line can have
//      multiple lots issued across multiple transactions.
//
// ABSORBS B2 (Job Splitting): The TransferToJob/TransferFromJob
// actions implement cross-job material movement with 6 enforced rules.
//
// 12 TRANSACTION TYPES:
//   Issue, IssueAll, IssueKit, PartialIssue, OverIssue,
//   Return, ReverseIssue, TransferToJob, TransferFromJob,
//   Substitute, Split, ScrapComponent
//
// LIFECYCLE: Posted (one-shot — material transactions are not
//   workflow-gated like engineering changes). However, they CAN be
//   reversed, which is the undo mechanism.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// Type of material movement on a frozen BOM line.
    /// Each type drives different validation rules and quantity
    /// column updates on the parent ProductionMaterialStructure row.
    /// </summary>
    public enum MaterialTransactionType
    {
        /// <summary>Issue material from inventory to the BOM line.</summary>
        Issue = 0,

        /// <summary>Issue all remaining required quantity in one action.</summary>
        IssueAll = 1,

        /// <summary>Issue as part of a kit (all BOM lines for a kit group).</summary>
        IssueKit = 2,

        /// <summary>Partial issue — less than the remaining required quantity.</summary>
        PartialIssue = 3,

        /// <summary>Issue more than the required quantity (reason required).</summary>
        OverIssue = 4,

        /// <summary>Return previously issued material back to inventory.</summary>
        Return = 5,

        /// <summary>Reverse a prior issue (creates a paired reversal record).</summary>
        ReverseIssue = 6,

        /// <summary>Transfer material OUT of this job TO another job.</summary>
        TransferToJob = 7,

        /// <summary>Transfer material IN to this job FROM another job.</summary>
        TransferFromJob = 8,

        /// <summary>Substitute one component for another on the BOM line.</summary>
        Substitute = 9,

        /// <summary>Split a BOM line requirement into multiple lots.</summary>
        Split = 10,

        /// <summary>Scrap issued material (removes from usable inventory).</summary>
        ScrapComponent = 11,
    }

    /// <summary>
    /// Transaction status. Material transactions are one-shot (Posted),
    /// but can be reversed.
    /// </summary>
    public enum MaterialTransactionStatus
    {
        Posted = 0,         // Active transaction
        Reversed = 1,       // This transaction has been reversed by another
        Cancelled = 2,      // Cancelled before posting (edge case)
    }

    [Table("ProductionMaterialTransactions")]
    public class ProductionMaterialTransaction
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        // ===== Transaction identity =========================================

        /// <summary>Unique transaction number within company.</summary>
        [Required] [StringLength(32)]
        public string TransactionNumber { get; set; } = string.Empty;

        public MaterialTransactionType TransactionType { get; set; } = MaterialTransactionType.Issue;
        public MaterialTransactionStatus Status { get; set; } = MaterialTransactionStatus.Posted;

        /// <summary>Timestamp of the material movement.</summary>
        public DateTime TransactionDateUtc { get; set; } = DateTime.UtcNow;

        // ===== Source — which PRO + BOM line ================================

        /// <summary>FK to the production order this transaction belongs to.</summary>
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>FK to the frozen BOM line being acted on.</summary>
        public int BomLineId { get; set; }
        public ProductionMaterialStructure? BomLine { get; set; }

        /// <summary>FK to the component item being moved.</summary>
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        /// <summary>Operation sequence where consumption occurs.</summary>
        public int? OperationSequence { get; set; }

        // ===== Quantity =====================================================

        /// <summary>
        /// Quantity moved (always positive). The TransactionType determines
        /// whether this adds to or subtracts from BOM line execution quantities.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        /// <summary>Unit of measure for this transaction.</summary>
        [StringLength(16)]
        public string? Uom { get; set; }

        /// <summary>
        /// BOM line's remaining-to-issue BEFORE this transaction.
        /// Captured for audit — allows exact reconstruction of the
        /// BOM line state at any point in time.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? QuantityBeforeTransaction { get; set; }

        /// <summary>
        /// BOM line's issued quantity AFTER this transaction.
        /// Captured for audit — the BOM line's IssuedQuantity at
        /// transaction time.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? QuantityAfterTransaction { get; set; }

        // ===== Location =====================================================

        /// <summary>Source warehouse/bin the material was issued from.</summary>
        [StringLength(50)]
        public string? FromWarehouse { get; set; }

        [StringLength(50)]
        public string? FromBin { get; set; }

        /// <summary>Destination warehouse/bin (for returns, transfers).</summary>
        [StringLength(50)]
        public string? ToWarehouse { get; set; }

        [StringLength(50)]
        public string? ToBin { get; set; }

        // ===== Lot / serial / traceability ==================================

        [StringLength(50)]
        public string? LotNumber { get; set; }

        [StringLength(50)]
        public string? SerialNumber { get; set; }

        [StringLength(50)]
        public string? HeatNumber { get; set; }

        [StringLength(50)]
        public string? VendorLot { get; set; }

        [StringLength(50)]
        public string? CertificateNumber { get; set; }

        /// <summary>Expiry date for shelf-life-controlled components.</summary>
        public DateTime? ExpiryDate { get; set; }

        // ===== Cost =========================================================

        /// <summary>Actual unit cost at time of movement.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ActualUnitCost { get; set; }

        /// <summary>Extended cost = Quantity * ActualUnitCost.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ExtendedCost { get; set; }

        /// <summary>Cost bucket for variance analysis.</summary>
        public CostBucket CostBucket { get; set; } = CostBucket.Material;

        // ===== Over-issue / scrap reason ====================================

        /// <summary>Required when TransactionType = OverIssue or ScrapComponent.</summary>
        [StringLength(500)]
        public string? ReasonCode { get; set; }

        [StringLength(1000)]
        public string? ReasonDescription { get; set; }

        /// <summary>True if supervisor override was used (e.g., over-issue,
        /// transfer creating shortage).</summary>
        public bool SupervisorOverride { get; set; }

        [StringLength(100)]
        public string? SupervisorOverrideBy { get; set; }

        // ===== Reversal linkage =============================================

        /// <summary>True if this transaction reverses a prior one.</summary>
        public bool IsReversal { get; set; }

        /// <summary>FK to the original transaction being reversed.</summary>
        public int? OriginalTransactionId { get; set; }
        public ProductionMaterialTransaction? OriginalTransaction { get; set; }

        // ===== Transfer linkage (B2 absorption) =============================

        /// <summary>
        /// For TransferToJob: FK to the destination ProductionOrder.
        /// For TransferFromJob: FK to the source ProductionOrder.
        /// </summary>
        public int? TransferProductionOrderId { get; set; }
        public ProductionOrder? TransferProductionOrder { get; set; }

        /// <summary>
        /// For TransferToJob: FK to the destination BOM line.
        /// For TransferFromJob: FK to the source BOM line.
        /// </summary>
        public int? TransferBomLineId { get; set; }
        public ProductionMaterialStructure? TransferBomLine { get; set; }

        /// <summary>
        /// Links the paired TransferToJob + TransferFromJob transactions.
        /// Both sides of the transfer share the same TransferPairId.
        /// </summary>
        [StringLength(64)]
        public string? TransferPairId { get; set; }

        /// <summary>Reason for the job-to-job transfer.</summary>
        [StringLength(500)]
        public string? TransferReason { get; set; }

        /// <summary>True if transfer required approval (Rule #3: customer-owned material).</summary>
        public bool TransferApprovalRequired { get; set; }

        [StringLength(100)]
        public string? TransferApprovedBy { get; set; }

        // ===== Substitution linkage =========================================

        /// <summary>For Substitute: the original component item being replaced.</summary>
        public int? OriginalItemId { get; set; }
        public Item? OriginalItem { get; set; }

        /// <summary>Substitution reason.</summary>
        [StringLength(500)]
        public string? SubstitutionReason { get; set; }

        /// <summary>Substitution authorization reference (ECN/deviation number).</summary>
        [StringLength(50)]
        public string? SubstitutionAuthReference { get; set; }

        /// <summary>True if customer approval was obtained for substitution.</summary>
        public bool SubstitutionCustomerApproved { get; set; }

        // ===== Backflush metadata ===========================================

        /// <summary>True if this transaction was auto-generated by backflush.</summary>
        public bool IsBackflushed { get; set; }

        /// <summary>FK to the operation completion that triggered backflush.</summary>
        public int? BackflushTriggerOperationId { get; set; }

        // ===== Audit ========================================================

        [StringLength(100)]
        public string? PerformedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        public string? UpdatedBy { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
