using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Models
{
    public enum ReceiptStatus
    {
        Draft = 0,
        Received = 1,
        PartiallyReceived = 2,
        Inspecting = 3,
        Accepted = 4,
        Rejected = 5
    }

    public class GoodsReceipt
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Receipt Number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Required]
        public int PurchaseOrderId { get; set; }
        [Display(Name = "Purchase Order")]
        public PurchaseOrder? PurchaseOrder { get; set; }

        public ReceiptStatus Status { get; set; } = ReceiptStatus.Draft;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        [Required]
        [Display(Name = "Receipt Date")]
        [DataType(DataType.Date)]
        public DateTime ReceiptDate { get; set; } = DateTime.Today;

        [StringLength(100)]
        [Display(Name = "Received By")]
        public string? ReceivedBy { get; set; }

        [StringLength(100)]
        [Display(Name = "Shipping Carrier")]
        public string? ShippingCarrier { get; set; }

        [StringLength(100)]
        [Display(Name = "Tracking Number")]
        public string? TrackingNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Packing Slip #")]
        public string? PackingSlipNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Receiving Location")]
        public string? ReceivingLocation { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();

        // S1-8 / S2-8: optimistic concurrency via PG xmin. See
        // Data/XminRowVersionExtensions.cs.
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    public class GoodsReceiptLine
    {
        public int Id { get; set; }

        public int GoodsReceiptId { get; set; }
        public GoodsReceipt? GoodsReceipt { get; set; }

        public int PurchaseOrderLineId { get; set; }
        [Display(Name = "PO Line")]
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        public int LineNumber { get; set; }

        [Display(Name = "Quantity Received")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReceived { get; set; }

        [Display(Name = "Quantity Accepted")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityAccepted { get; set; }

        [Display(Name = "Quantity Rejected")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityRejected { get; set; }

        [StringLength(500)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        [StringLength(100)]
        [Display(Name = "Storage Location")]
        public string? StorageLocation { get; set; }

        [Display(Name = "Receiving Location")]
        public int? ReceivingLocationId { get; set; }
        public Location? ReceivingLocation { get; set; }

        [StringLength(50)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? CipProjectId { get; set; }
        [Display(Name = "CIP Project")]
        public CipProject? CipProject { get; set; }

        public bool IsInvoiced { get; set; } = false;

        // ===== Sprint 15.1 PR-1 — Receipt-to-Job Direct Posting =============
        // Buy-to-job material flows PO Receipt → direct charge to PRO BOM line,
        // skipping inventory entirely. THE foundational ETO/MTO architectural
        // change. See docs/research/purchasing-cascade-design-2026-05-28.md PR-1.

        /// <summary>
        /// When true, this receipt line bypasses inventory and posts cost directly
        /// to a Production Order BOM line. The ETO/MTO default — bought-to-job
        /// material never touches inventory valuation layers.
        /// </summary>
        [Display(Name = "Direct to Job")]
        public bool IsDirectToJob { get; set; } = false;

        /// <summary>
        /// FK to the target Production Order for direct-to-job receipts.
        /// Null for standard inventory receipts.
        /// </summary>
        [Display(Name = "Production Order (Direct)")]
        public int? DirectToJobProductionOrderId { get; set; }
        public ProductionOrder? DirectToJobProductionOrder { get; set; }

        /// <summary>
        /// FK to the specific BOM line on the PRO that this receipt satisfies.
        /// Links GR line → frozen BOM snapshot row for cost allocation + supply
        /// status update. Null for standard inventory receipts.
        /// </summary>
        [Display(Name = "BOM Line (Direct)")]
        public int? DirectToJobBomLineId { get; set; }
        public ProductionMaterialStructure? DirectToJobBomLine { get; set; }

        /// <summary>
        /// When true, this receipt line requires incoming inspection before cost
        /// is posted to the PRO. Inspection hold prevents premature material
        /// availability claims on the BOM line supply link.
        /// </summary>
        [Display(Name = "Inspection Required")]
        public bool InspectionRequired { get; set; } = false;

        /// <summary>
        /// Timestamp when the direct-to-job cost was posted to the PRO.
        /// Null = not yet posted (pending inspection or processing).
        /// </summary>
        [Display(Name = "Direct Post Date")]
        public DateTime? DirectToJobPostedUtc { get; set; }
    }
}
