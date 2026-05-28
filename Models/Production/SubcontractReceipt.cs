// Sprint 15.2 PR-6 (2026-05-28) — SubcontractReceipt + SubcontractReceiptLine.
//
// THE RECEIVE-BACK FROM VENDOR.
//
// When the vendor finishes processing (or rejects, or scraps) and ships WIP
// back, we record a SubcontractReceipt header + N receipt lines. Each line
// carries one of 10 RECEIPT SCENARIOS from Dean's spec §11 and an explicit
// disposition. Some scenarios block downstream operations (RejectedReceipt,
// CertMissing, WrongRevision); some require approval (OverReceipt,
// WrongJobOrPo); the "happy path" FullGoodReceipt updates QuantityAccepted
// on the subcontract op and allows the next routing op to start.
//
// The receipt also drives:
//   - VendorWipTransaction (ReceiveFromVendor / RejectAtReceipt / ScrapAtVendor)
//     consuming vendor WIP balance
//   - Update to op.QuantityReceivedBack / QuantityAccepted / QuantityRejected
//   - Lifecycle advancement (FullyReceived / PartiallyReceived / Rejected /
//     InInspection)
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §5 §11
//   - docs/research/purchasing-cascade-design-2026-05-28.md PR-6

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// 10 receipt scenarios from spec §11. Drives downstream behavior +
    /// approval workflow + cost posting tags.
    /// </summary>
    public enum SubcontractReceiptScenario
    {
        /// <summary>Full quantity returned + accepted. Complete + move to next op.</summary>
        FullGoodReceipt = 0,
        /// <summary>Partial qty back; balance still at vendor.</summary>
        PartialReceipt = 1,
        /// <summary>Hold for incoming inspection; next op cannot start until cleared.</summary>
        ReceiptWithInspection = 2,
        /// <summary>Material failed inspection — block or rework, create NCR/MRB.</summary>
        RejectedReceipt = 3,
        /// <summary>Vendor scrapped part during processing — record scrap cost,
        /// require replacement/rework decision.</summary>
        VendorScrap = 4,
        /// <summary>Short receipt — PO + vendor WIP stay open.</summary>
        ShortReceipt = 5,
        /// <summary>Over receipt — requires supervisor approval before processing.</summary>
        OverReceipt = 6,
        /// <summary>Certification/quality docs missing — document hold.</summary>
        CertMissing = 7,
        /// <summary>Wrong revision returned — quality/engineering hold.</summary>
        WrongRevision = 8,
        /// <summary>Wrong job or wrong PO referenced — block or supervisor override.</summary>
        WrongJobOrPo = 9,
    }

    /// <summary>
    /// Per-line disposition. The "what now" after the line is recorded.
    /// </summary>
    public enum SubcontractReceiptDisposition
    {
        /// <summary>Move accepted qty to the next routing operation.</summary>
        ReleaseToNextOp = 0,
        /// <summary>Hold pending inspection.</summary>
        HoldForInspection = 1,
        /// <summary>Hold pending cert/doc receipt.</summary>
        HoldForDocs = 2,
        /// <summary>Hold pending quality/engineering review.</summary>
        HoldForQuality = 3,
        /// <summary>Send back to vendor for rework.</summary>
        ReworkAtVendor = 4,
        /// <summary>Scrap and trigger replacement decision.</summary>
        ScrapReplace = 5,
        /// <summary>Pending supervisor approval (over-receipt / wrong-PO scenarios).</summary>
        PendingApproval = 6,
        /// <summary>Recorded for audit — no immediate action (cancellations).</summary>
        InformationalOnly = 7,
    }

    /// <summary>
    /// Receipt-header lifecycle.
    /// </summary>
    public enum SubcontractReceiptLifecycle
    {
        /// <summary>Header created, lines being added.</summary>
        Draft = 0,
        /// <summary>All lines added; posting in progress.</summary>
        Posting = 1,
        /// <summary>Receipt posted — qtys + cost reflected on op + vendor WIP.</summary>
        Posted = 2,
        /// <summary>Posted but pending approval (over-receipt / wrong-PO).</summary>
        PendingApproval = 3,
        /// <summary>Approved after pending state.</summary>
        Approved = 4,
        /// <summary>Reversed (e.g., wrong-PO error correction).</summary>
        Reversed = 5,
        /// <summary>Closed — no further activity expected.</summary>
        Closed = 6,
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SubcontractReceipt — the header for one receive-back event
    // ═══════════════════════════════════════════════════════════════════════

    public class SubcontractReceipt
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── Receipt identity ──────────────────────────

        /// <summary>Tenant-unique receipt number (e.g., SCRCP-2026-00027).</summary>
        [Required, StringLength(48)]
        [Display(Name = "Receipt Number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        /// <summary>Vendor's packing slip / shipper number (their reference).</summary>
        [StringLength(96)]
        [Display(Name = "Vendor Packing Slip")]
        public string? VendorPackingSlip { get; set; }

        // ──────────────────────── Subcontract context ───────────────────────

        [Required]
        public int SubcontractOperationId { get; set; }
        public SubcontractOperation? SubcontractOperation { get; set; }

        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        public int OperationSequence { get; set; }

        /// <summary>The original shipment this receipt covers (optional —
        /// partial receipts may not map 1:1).</summary>
        public int? SubcontractShipmentId { get; set; }
        public SubcontractShipment? SubcontractShipment { get; set; }

        public int? ServicePurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? ServicePurchaseOrderLine { get; set; }

        // ──────────────────────── Supplier + receiver ───────────────────────

        [Required]
        public int SupplierId { get; set; }
        public Vendor? Supplier { get; set; }

        public int? VendorLocationId { get; set; }
        public VendorLocation? VendorLocation { get; set; }

        /// <summary>Location where receipt was physically performed (our dock).</summary>
        public int? ReceivingLocationId { get; set; }
        public Location? ReceivingLocation { get; set; }

        // ──────────────────────── Dates ─────────────────────────────────────

        [Required]
        [Display(Name = "Receipt Date")]
        public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

        [StringLength(96)]
        [Display(Name = "Carrier")]
        public string? Carrier { get; set; }

        [StringLength(96)]
        [Display(Name = "Tracking Number")]
        public string? TrackingNumber { get; set; }

        // ──────────────────────── Cert + compliance ─────────────────────────

        [Display(Name = "Cert Received")]
        public bool CertReceived { get; set; }

        [StringLength(96)]
        [Display(Name = "Cert Reference")]
        public string? CertReference { get; set; }

        [Display(Name = "Inspection Required")]
        public bool InspectionRequired { get; set; }

        // ──────────────────────── Lifecycle ─────────────────────────────────

        public SubcontractReceiptLifecycle Status { get; set; } = SubcontractReceiptLifecycle.Draft;

        [Display(Name = "Approval Required")]
        public bool ApprovalRequired { get; set; }

        [StringLength(120)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved Utc")]
        public DateTime? ApprovedUtc { get; set; }

        [Display(Name = "Posted Utc")]
        public DateTime? PostedUtc { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public ICollection<SubcontractReceiptLine> Lines { get; set; } = new List<SubcontractReceiptLine>();

        public byte[]? RowVersion { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SubcontractReceiptLine — one physical lot/scenario received back
    // ═══════════════════════════════════════════════════════════════════════

    public class SubcontractReceiptLine
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        [Required]
        public int SubcontractReceiptId { get; set; }
        public SubcontractReceipt? SubcontractReceipt { get; set; }

        /// <summary>1-based line number within the receipt.</summary>
        [Display(Name = "Line #")]
        public int LineNumber { get; set; }

        /// <summary>Original shipment line this receipt line settles (optional).</summary>
        public int? SubcontractShipmentLineId { get; set; }
        public SubcontractShipmentLine? SubcontractShipmentLine { get; set; }

        // ──────────────────────── Physical item identity ────────────────────

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(64)]
        [Display(Name = "Part Number")]
        public string? PartNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(32)]
        [Display(Name = "Drawing Revision")]
        public string? DrawingRevision { get; set; }

        [StringLength(64)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(64)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        // ──────────────────────── Quantities (§11) ──────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Received")]
        public decimal QuantityReceived { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Accepted")]
        public decimal QuantityAccepted { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Rejected")]
        public decimal QuantityRejected { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Scrapped At Vendor")]
        public decimal QuantityScrappedAtVendor { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Short")]
        public decimal QuantityShort { get; set; }

        [Required, StringLength(16)]
        public string Uom { get; set; } = "EA";

        // ──────────────────────── Scenario + disposition (§11) ──────────────

        public SubcontractReceiptScenario Scenario { get; set; } = SubcontractReceiptScenario.FullGoodReceipt;

        public SubcontractReceiptDisposition Disposition { get; set; } = SubcontractReceiptDisposition.ReleaseToNextOp;

        [StringLength(96)]
        [Display(Name = "Reject Reason")]
        public string? RejectReason { get; set; }

        /// <summary>NCR or MRB document reference if scenario triggered one.</summary>
        [StringLength(96)]
        [Display(Name = "NCR Reference")]
        public string? NcrReference { get; set; }

        // ──────────────────────── Downstream linkage ────────────────────────

        public int? VendorWipTransactionId { get; set; }
        public VendorWipTransaction? VendorWipTransaction { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
