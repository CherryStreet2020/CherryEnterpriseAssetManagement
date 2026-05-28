// Sprint 15.4 PR-16 — PO Acknowledgment / Vendor Confirmation
//
// Spec ref: docs/research/purchasing-subcontracting-supply-demand-dean-research.txt
//   §15  "Supplier acknowledgment" / "Quantity confirmed" / "Confirmed promise date"
//   §23  POAcknowledgment entity in the master list
//
// Design principle: POStatus stays clean (9-state Draft→...→Cancelled). The
// POAcknowledgment record OVERLAYS the PO lifecycle so an Approved+Sent PO
// can have an attached AcknowledgmentStatus = Requested / Acknowledged /
// Confirmed / ConfirmedWithExceptions / Rejected / Expired / Cancelled
// without bloating POStatus.
//
// Sets up PR-17 vendor re-acknowledgment loop — when PO is amended, the
// IsCurrent ack flips to false and a new Requested ack is created. The
// history collection on PurchaseOrder.Acknowledgments preserves the trail.
//
// Per-line confirmation pattern: one POAcknowledgmentLine per
// PurchaseOrderLine. Vendor may confirm as-ordered (IsAccepted=true,
// ExceptionType=None) or flag an exception (QuantityShort / DatePushOut /
// PriceDifference / etc.). Buyer's ApproveExceptionAsync resolves each.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Lifecycle of a single vendor acknowledgment event for a Purchase Order.
    /// </summary>
    public enum POAcknowledgmentStatus
    {
        /// <summary>System has requested ack from vendor; awaiting response.</summary>
        Requested = 0,

        /// <summary>Vendor confirmed receipt of PO but hasn't yet confirmed lines.</summary>
        Acknowledged = 1,

        /// <summary>Vendor confirmed all lines as-ordered; no exceptions raised.</summary>
        Confirmed = 2,

        /// <summary>Vendor confirmed but flagged at least one line exception.</summary>
        ConfirmedWithExceptions = 3,

        /// <summary>Vendor refused to fulfill the PO.</summary>
        Rejected = 4,

        /// <summary>SLA window passed without vendor response.</summary>
        Expired = 5,

        /// <summary>Buyer cancelled the ack request (PO amended/voided/etc.).</summary>
        Cancelled = 6
    }

    /// <summary>
    /// How the vendor delivered the acknowledgment.
    /// </summary>
    public enum POAcknowledgmentMethod
    {
        /// <summary>Self-service vendor portal entry.</summary>
        VendorPortal = 0,

        /// <summary>Email confirmation (buyer transcribed or parsed).</summary>
        Email = 1,

        /// <summary>EDI 855 PO Acknowledgment transaction.</summary>
        Edi = 2,

        /// <summary>Phone call (buyer logged).</summary>
        Phone = 3,

        /// <summary>System auto-acknowledged via rule (e.g., trusted supplier under threshold).</summary>
        Auto = 4
    }

    /// <summary>
    /// Per-line exception types vendors can raise on acknowledgment.
    /// </summary>
    public enum PoAckLineExceptionType
    {
        /// <summary>Line accepted as-ordered.</summary>
        None = 0,

        /// <summary>Vendor will ship LESS than ordered quantity.</summary>
        QuantityShort = 1,

        /// <summary>Vendor wants to ship MORE than ordered quantity.</summary>
        QuantityOver = 2,

        /// <summary>Vendor disputes unit price.</summary>
        PriceDifference = 3,

        /// <summary>Vendor pushing promise date LATER than required date.</summary>
        DatePushOut = 4,

        /// <summary>Vendor pulling promise date EARLIER (rare but legitimate).</summary>
        DatePullIn = 5,

        /// <summary>Partial rejection — some quantity refused.</summary>
        PartialReject = 6,

        /// <summary>Full line rejection — vendor cannot/will not fulfill this line.</summary>
        FullReject = 7
    }

    /// <summary>
    /// One vendor acknowledgment event for a Purchase Order.
    /// Multiple POAcknowledgment rows can exist per PO (each amendment creates
    /// a fresh Requested ack), but only one has IsCurrent=true at a time.
    /// </summary>
    public class POAcknowledgment
    {
        public int Id { get; set; }

        /// <summary>Tenant scope — filtered on every query.</summary>
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>Owning Purchase Order.</summary>
        [Required]
        public int PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; } = null!;

        /// <summary>
        /// Human-readable ack number, format POACK-YYYY-NNNNNN. Tenant-unique.
        /// Assigned post-save via two-phase numbering (Lesson 2 from Session 19):
        /// insert with Guid placeholder, then patch using EF Id post-save.
        /// </summary>
        [Required, StringLength(40)]
        [Display(Name = "Acknowledgment Number")]
        public string AcknowledgmentNumber { get; set; } = string.Empty;

        /// <summary>Current lifecycle position.</summary>
        public POAcknowledgmentStatus Status { get; set; } = POAcknowledgmentStatus.Requested;

        /// <summary>Channel the vendor used to confirm.</summary>
        public POAcknowledgmentMethod Method { get; set; } = POAcknowledgmentMethod.VendorPortal;

        /// <summary>UTC timestamp when buyer requested ack from vendor.</summary>
        [Display(Name = "Requested At (UTC)")]
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp when vendor sent the ack (null until response received).</summary>
        [Display(Name = "Acknowledged At (UTC)")]
        public DateTime? AcknowledgedAtUtc { get; set; }

        /// <summary>UTC timestamp when ack moved to a Confirmed/Rejected/Expired terminal state.</summary>
        [Display(Name = "Closed At (UTC)")]
        public DateTime? ClosedAtUtc { get; set; }

        /// <summary>SLA deadline for vendor response — past this triggers Expired.</summary>
        [Display(Name = "Response Due By (UTC)")]
        public DateTime? ResponseDueByUtc { get; set; }

        /// <summary>Vendor contact who confirmed (name or email).</summary>
        [StringLength(200)]
        [Display(Name = "Vendor Contact")]
        public string? AcknowledgedByVendorContact { get; set; }

        /// <summary>Buyer user who requested the ack.</summary>
        public int? RequestedByUserId { get; set; }
        public User? RequestedByUser { get; set; }

        /// <summary>
        /// Vendor's confirmed overall promise date (delivery for all lines).
        /// Per-line dates live on POAcknowledgmentLine.ConfirmedPromiseDate.
        /// </summary>
        [Display(Name = "Confirmed Promise Date")]
        [DataType(DataType.Date)]
        public DateTime? ConfirmedPromiseDate { get; set; }

        /// <summary>Vendor's free-text reason for rejection or exception narrative.</summary>
        [StringLength(2000)]
        public string? VendorNotes { get; set; }

        /// <summary>Buyer's internal disposition note.</summary>
        [StringLength(2000)]
        public string? BuyerNotes { get; set; }

        /// <summary>
        /// True if this is the LATEST ack record for the PO regardless of
        /// terminal status. Stays true through Confirmed / Rejected /
        /// Expired / Cancelled — flipped to false only when the next
        /// <see cref="IPoAcknowledgmentService.RequestAcknowledgmentAsync"/>
        /// opens a fresh cycle (typically driven by PR-17 PO amendment).
        /// Read <c>Status</c> to decide whether the current ack is still
        /// actionable; read <c>IsCurrent</c> only to filter history.
        /// </summary>
        [Display(Name = "Current")]
        public bool IsCurrent { get; set; } = true;

        /// <summary>
        /// True if every line is accepted as-ordered (no exceptions). Computed
        /// by ConfirmAcknowledgmentAsync and persisted.
        /// </summary>
        [Display(Name = "All Lines Accepted")]
        public bool AllLinesAcceptedAsOrdered { get; set; } = false;

        /// <summary>UTC create timestamp.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC last-modified timestamp.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Per-line confirmation lines.</summary>
        public ICollection<POAcknowledgmentLine> Lines { get; set; }
            = new List<POAcknowledgmentLine>();

        /// <summary>
        /// Optimistic concurrency via PG xmin — same pattern as PurchaseOrder.
        /// Mapped via fluent API in AppDbContext.OnModelCreating.
        /// </summary>
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// Per-PO-line vendor confirmation. One row per PurchaseOrderLine on the
    /// parent acknowledgment. Holds confirmed qty/price/date plus exception
    /// type/reason when the vendor cannot fulfill as-ordered.
    /// </summary>
    public class POAcknowledgmentLine
    {
        public int Id { get; set; }

        [Required]
        public int POAcknowledgmentId { get; set; }
        public POAcknowledgment POAcknowledgment { get; set; } = null!;

        /// <summary>Linked PO line being confirmed.</summary>
        [Required]
        public int PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;

        /// <summary>
        /// Snapshot of the ordered quantity at ack time (for reporting + audit
        /// of vendor's confirmation against the as-ordered baseline).
        /// </summary>
        [Display(Name = "Ordered Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal OrderedQuantity { get; set; }

        /// <summary>
        /// Vendor's confirmed quantity for this line. May differ from
        /// OrderedQuantity when ExceptionType = QuantityShort/Over/Partial.
        /// </summary>
        [Display(Name = "Confirmed Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ConfirmedQuantity { get; set; }

        /// <summary>Snapshot of ordered unit price.</summary>
        [Display(Name = "Ordered Unit Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal OrderedUnitPrice { get; set; }

        /// <summary>Vendor's confirmed unit price. Differs when ExceptionType = PriceDifference.</summary>
        [Display(Name = "Confirmed Unit Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ConfirmedUnitPrice { get; set; }

        /// <summary>Snapshot of line's required date.</summary>
        [Display(Name = "Required Date")]
        [DataType(DataType.Date)]
        public DateTime? RequiredDate { get; set; }

        /// <summary>Vendor's confirmed promise date for this line.</summary>
        [Display(Name = "Confirmed Promise Date")]
        [DataType(DataType.Date)]
        public DateTime? ConfirmedPromiseDate { get; set; }

        /// <summary>
        /// True iff vendor confirms this line exactly as-ordered (qty/price/date
        /// match snapshots and ExceptionType=None). Service-maintained.
        /// </summary>
        [Display(Name = "Accepted As Ordered")]
        public bool IsAccepted { get; set; } = false;

        /// <summary>Exception flavor (None for accepted lines).</summary>
        public PoAckLineExceptionType ExceptionType { get; set; } = PoAckLineExceptionType.None;

        /// <summary>Vendor's narrative reason for the exception.</summary>
        [StringLength(1000)]
        public string? ExceptionReason { get; set; }

        /// <summary>
        /// True once the buyer has reviewed + approved the exception. PR-17
        /// will couple approval to an automatic PO amendment.
        /// </summary>
        [Display(Name = "Exception Approved")]
        public bool ExceptionApproved { get; set; } = false;

        /// <summary>Approver user id (null until approved).</summary>
        public int? ExceptionApprovedByUserId { get; set; }
        public User? ExceptionApprovedByUser { get; set; }

        /// <summary>UTC approval timestamp.</summary>
        public DateTime? ExceptionApprovedAtUtc { get; set; }

        /// <summary>Approver's note.</summary>
        [StringLength(1000)]
        public string? ApprovalNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
