// Sprint 15.4 PR-17 — PO Amendment / Change Order
//
// Spec ref:
//   docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §22, §15
//   docs/research/purchasing-cascade-design-2026-05-28.md PR-17
//   project_wave4_enhancement_decisions_2026_05_28.md (Dean's locked PR-17 spec)
//
// THE BIC DIFFERENTIATOR — demand-link impact preview + auto-resync.
// Before an amendment is approved, the system surfaces every
// PurchaseOrderLineDemandLink / ProductionSupplyAllocation / PRO operation
// affected by the qty/date/price change. On Apply, the amendment atomically:
//   1) Updates the PO line snapshots (qty / price / promise date)
//   2) Re-syncs PurchaseOrderLineDemandLink AllocatedQuantity / PromiseDate
//   3) Mirrors the resync to ProductionSupplyAllocation (§17 traceability)
//   4) Flips the current POAcknowledgment IsCurrent → false and opens a new
//      Requested ack via IPoAcknowledgmentService (vendor re-ack loop)
//
// SAP / Oracle / MIE leave this manual — buyer has to chase downstream
// impact by hand. We make the blast radius visible BEFORE the buyer signs.
//
// Two-phase numbering for AmendmentNumber (POAMD-YYYY-NNNNNN) — same
// pattern as PR-16 POAcknowledgment (Lesson 2, Session 19).

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Lifecycle of a single PO amendment cycle.
    /// </summary>
    public enum POAmendmentStatus
    {
        /// <summary>Buyer is drafting the amendment; lines may be added/removed.</summary>
        Draft = 0,

        /// <summary>PreviewAmendmentImpactAsync ran; ImpactReport materialized for review.</summary>
        Previewed = 1,

        /// <summary>Awaiting approver decision (after preview is reviewed).</summary>
        PendingApproval = 2,

        /// <summary>Approver signed off; ready for ApplyAmendmentAsync.</summary>
        Approved = 3,

        /// <summary>Atomically applied — PO lines updated, demand links re-synced, vendor re-ack opened.</summary>
        Applied = 4,

        /// <summary>Approver rejected; PO header / lines untouched.</summary>
        Rejected = 5,

        /// <summary>Buyer abandoned the draft before approval.</summary>
        Cancelled = 6
    }

    /// <summary>
    /// Why the amendment is being raised. Drives downstream notifications +
    /// impact-narrative templates + re-approval routing.
    /// </summary>
    public enum POChangeReason
    {
        /// <summary>Auto-raised when the buyer approves a vendor exception via PR-16 ApproveLineExceptionAsync.</summary>
        VendorExceptionApproved = 0,

        /// <summary>Buyer initiated for internal scope/timing reasons.</summary>
        BuyerRequested = 1,

        /// <summary>Customer scope change cascaded down the supply chain.</summary>
        ScopeChange = 2,

        /// <summary>Negotiated price revision.</summary>
        PriceRenegotiation = 3,

        /// <summary>Date change (push-out or pull-in).</summary>
        DateChange = 4,

        /// <summary>Quantity revision (short/over without canceling the line).</summary>
        QuantityChange = 5,

        /// <summary>Supplier-initiated cancellation of one or more lines.</summary>
        SupplierCancellation = 6,

        /// <summary>PRO reschedule forced new promise-date alignment.</summary>
        ProductionRescheduled = 7,

        /// <summary>Engineering change (ECR/ECO) altered part / revision / spec.</summary>
        EngineeringChange = 8,

        /// <summary>Catch-all.</summary>
        Other = 9
    }

    /// <summary>
    /// What changed on a single line of the amendment.
    /// </summary>
    public enum POAmendmentLineChangeType
    {
        /// <summary>No change on this snapshot row (impact-preview placeholder).</summary>
        Unchanged = 0,

        /// <summary>Quantity changed.</summary>
        QuantityChange = 1,

        /// <summary>Unit price changed.</summary>
        PriceChange = 2,

        /// <summary>Promise date changed.</summary>
        DateChange = 3,

        /// <summary>Two or more of (qty / price / date) changed.</summary>
        MultipleChanges = 4,

        /// <summary>Line was added by the amendment (not yet on the original PO).</summary>
        NewLine = 5,

        /// <summary>Line was removed/voided by the amendment.</summary>
        RemovedLine = 6
    }

    /// <summary>
    /// Header record for one PO amendment cycle. Multiple POChangeHistory rows
    /// can exist per PO (each amendment is its own cycle), but only one carries
    /// IsCurrent = true at a time; prior amendments stay for the audit trail.
    /// </summary>
    public class POChangeHistory
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>PO being amended.</summary>
        [Required]
        public int PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; } = null!;

        /// <summary>
        /// Human-readable amendment number, format POAMD-YYYY-NNNNNN.
        /// Two-phase numbering (same pattern as POAcknowledgment): insert with
        /// Guid placeholder, SaveChanges, patch with EF-assigned Id, SaveChanges.
        /// Tenant-unique via filtered index (CompanyId IS NOT NULL).
        /// </summary>
        [Required, StringLength(40)]
        [Display(Name = "Amendment Number")]
        public string AmendmentNumber { get; set; } = string.Empty;

        public POAmendmentStatus Status { get; set; } = POAmendmentStatus.Draft;
        public POChangeReason Reason { get; set; } = POChangeReason.BuyerRequested;

        /// <summary>Free-text narrative — buyer's description of why and what.</summary>
        [StringLength(2000)]
        [Display(Name = "Reason Narrative")]
        public string? ReasonNarrative { get; set; }

        /// <summary>Buyer who drafted the amendment.</summary>
        public int? DraftedByUserId { get; set; }
        public User? DraftedByUser { get; set; }

        /// <summary>UTC timestamps for each lifecycle hop.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "Previewed At (UTC)")]
        public DateTime? PreviewedAtUtc { get; set; }

        [Display(Name = "Submitted At (UTC)")]
        public DateTime? SubmittedForApprovalAtUtc { get; set; }

        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }

        [Display(Name = "Approved At (UTC)")]
        public DateTime? ApprovedAtUtc { get; set; }

        [Display(Name = "Applied At (UTC)")]
        public DateTime? AppliedAtUtc { get; set; }

        [Display(Name = "Closed At (UTC)")]
        public DateTime? ClosedAtUtc { get; set; }

        // ─── Impact preview cache (BIC differentiator) ───────────────────────
        // PreviewAmendmentImpactAsync stamps these so the Razor partial can
        // render the impact table without re-walking the graph on every read.

        /// <summary>Affected PurchaseOrderLineDemandLink rows (count).</summary>
        [Display(Name = "Affected Demand Links")]
        public int AffectedDemandLinkCount { get; set; }

        /// <summary>Distinct ProductionOrders affected by this amendment.</summary>
        [Display(Name = "Affected Production Orders")]
        public int AffectedProductionOrderCount { get; set; }

        /// <summary>Distinct PRO operations (Operation Sequences) affected.</summary>
        [Display(Name = "Affected Operations")]
        public int AffectedOperationCount { get; set; }

        /// <summary>True if any affected demand link had a promise date push-out vs. its NeedByDate.</summary>
        [Display(Name = "Ship-Date Risk")]
        public bool ShipDateRiskFlag { get; set; }

        /// <summary>Aggregate value delta (NewExtended - OriginalExtended) across changed lines.</summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Total Value Delta")]
        public decimal TotalValueDelta { get; set; }

        /// <summary>Aggregate qty delta across changed lines.</summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Total Quantity Delta")]
        public decimal TotalQuantityDelta { get; set; }

        /// <summary>
        /// Service-rendered narrative summary (HTML-safe) of the impact preview.
        /// Cached at preview time; the Razor partial renders this verbatim.
        /// </summary>
        [StringLength(4000)]
        [Display(Name = "Impact Narrative")]
        public string? ImpactNarrative { get; set; }

        /// <summary>True iff the previously current POAcknowledgment for this PO
        /// must be flipped to non-current and a new Requested ack opened on Apply.
        /// Default true — most amendments invalidate any prior vendor confirmation.</summary>
        [Display(Name = "Requires Vendor Re-Acknowledgment")]
        public bool VendorReAcknowledgmentRequired { get; set; } = true;

        // ─── Approval / rejection ────────────────────────────────────────────

        [StringLength(2000)]
        [Display(Name = "Approval Note")]
        public string? ApprovalNote { get; set; }

        [StringLength(2000)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        // ─── IsCurrent invariant + concurrency ───────────────────────────────

        /// <summary>
        /// True if this is the most recent amendment record for the PO. Mirrors
        /// the POAcknowledgment.IsCurrent semantic: stays true on terminal
        /// statuses (Applied / Rejected / Cancelled) and is flipped only by
        /// the NEXT DraftAmendmentAsync that opens a fresh cycle. A filtered
        /// unique index on (PurchaseOrderId WHERE IsCurrent=TRUE) enforces
        /// "one current amendment per PO" at the DB level.
        /// </summary>
        [Display(Name = "Current")]
        public bool IsCurrent { get; set; } = true;

        public ICollection<POChangeHistoryLine> Lines { get; set; }
            = new List<POChangeHistoryLine>();

        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// Per-line change record. One row per PurchaseOrderLine affected by the
    /// amendment (new lines and removed lines also persisted with NullableId
    /// + ChangeType = NewLine / RemovedLine).
    /// </summary>
    public class POChangeHistoryLine
    {
        public int Id { get; set; }

        [Required]
        public int POChangeHistoryId { get; set; }
        public POChangeHistory POChangeHistory { get; set; } = null!;

        /// <summary>
        /// Existing PO line being amended. NULL only when ChangeType = NewLine
        /// (line was created by this amendment and stamped post-Apply).
        /// </summary>
        public int? PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        public POAmendmentLineChangeType ChangeType { get; set; }
            = POAmendmentLineChangeType.Unchanged;

        // ─── Original snapshots (frozen at Draft time) ───────────────────────

        [Column(TypeName = "decimal(18,4)")]
        public decimal OriginalQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal OriginalUnitPrice { get; set; }

        [DataType(DataType.Date)]
        public DateTime? OriginalPromiseDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? OriginalRequiredDate { get; set; }

        // ─── Proposed new values (filled by buyer in the Draft) ──────────────

        [Column(TypeName = "decimal(18,4)")]
        public decimal NewQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal NewUnitPrice { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NewPromiseDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NewRequiredDate { get; set; }

        // ─── Per-line impact (populated by PreviewAmendmentImpactAsync) ──────

        [Display(Name = "Demand Links Affected")]
        public int AffectedDemandLinkCount { get; set; }

        [Display(Name = "Production Orders Affected")]
        public int AffectedProductionOrderCount { get; set; }

        /// <summary>Affected production-order numbers (comma-separated for display).</summary>
        [StringLength(1000)]
        [Display(Name = "Affected PRO Numbers")]
        public string? AffectedProductionOrderNumbers { get; set; }

        /// <summary>Worst push-out across affected demand links, in days.</summary>
        [Display(Name = "Max Date Push-Out (days)")]
        public int? MaxDatePushOutDays { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Value Delta")]
        public decimal ValueDelta { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Delta")]
        public decimal QuantityDelta { get; set; }

        /// <summary>Per-line narrative — drives the row tooltip in the impact partial.</summary>
        [StringLength(1000)]
        public string? LineNarrative { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
