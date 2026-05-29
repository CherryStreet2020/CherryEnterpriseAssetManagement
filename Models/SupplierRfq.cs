// Sprint 15.4 PR-20 — RFQ / Quote Flow (CLOSES the 20-PR purchasing cascade)
//
// Spec ref: docs/research/purchasing-cascade-design-2026-05-28.md PR-20
//   "SupplierRFQ + SupplierRFQLine + SupplierQuote + SupplierQuoteLine. Convert
//    winning quote to PO line."
// Enhancement (Dean's locked Wave 4 decision): RankQuotesAsync returns a ranked
// list with a composite score + winner badge + reason text. Composite =
// Price 50% + LeadTime 30% + SupplierOTD 20% (weights configurable); SupplierOTD
// pulled from PR-18 ISupplierPerformanceService.GetCompositeInputsAsync. Falls
// back to price+lead-time only when a supplier has no Rolling90Days snapshot.
//
// Flow: Draft RFQ (+ lines) → Issue (invite suppliers, one SupplierQuote each) →
// RecordQuote (vendor's prices + lead time per line) → RankQuotes (compute
// composite, stamp RankPosition + IsWinner + ScoreReason) → AwardQuote →
// ConvertQuoteToPoLine (create a Draft PO carrying the §17 demand link forward).

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    /// <summary>RFQ lifecycle.</summary>
    public enum RfqStatus
    {
        Draft = 0,
        Issued = 1,             // Sent to invited suppliers; awaiting quotes.
        QuotesReceived = 2,     // At least one quote recorded.
        Evaluated = 3,          // RankQuotes has run; winner identified.
        Awarded = 4,            // A quote was awarded + converted to a PO.
        Cancelled = 5,
        Closed = 6
    }

    /// <summary>Per-supplier quote lifecycle within an RFQ.</summary>
    public enum SupplierQuoteStatus
    {
        Invited = 0,            // Supplier invited; no quote yet.
        Received = 1,           // Quote prices recorded.
        Declined = 2,           // Supplier declined to quote.
        Shortlisted = 3,        // Passed initial screen.
        Awarded = 4,            // Winning quote.
        Rejected = 5            // Lost the award.
    }

    /// <summary>
    /// A Request For Quote — header + lines + the supplier quotes received against
    /// it. Tenant-scoped; RFQ-YYYY-NNNNNN two-phase numbered.
    /// </summary>
    public class SupplierRFQ
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required, StringLength(40)]
        [Display(Name = "RFQ Number")]
        public string RfqNumber { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public RfqStatus Status { get; set; } = RfqStatus.Draft;

        [Display(Name = "Required By")]
        [DataType(DataType.Date)]
        public DateTime? RequiredByDate { get; set; }

        [Display(Name = "Issued At (UTC)")]
        public DateTime? IssuedAtUtc { get; set; }

        [Display(Name = "Quotes Due (UTC)")]
        public DateTime? QuotesDueUtc { get; set; }

        [Display(Name = "Evaluated At (UTC)")]
        public DateTime? EvaluatedAtUtc { get; set; }

        /// <summary>The awarded quote (set by AwardQuote). Null until awarded.</summary>
        public int? AwardedQuoteId { get; set; }

        /// <summary>PO created from the awarded quote (set by ConvertQuoteToPoLine).</summary>
        public int? ResultingPurchaseOrderId { get; set; }

        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<SupplierRFQLine> Lines { get; set; } = new List<SupplierRFQLine>();
        public ICollection<SupplierQuote> Quotes { get; set; } = new List<SupplierQuote>();

        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// One item the RFQ is soliciting. Carries optional demand linkage so a quote
    /// converted to a PO preserves §17 PRO/BOM/demand traceability.
    /// </summary>
    public class SupplierRFQLine
    {
        public int Id { get; set; }

        [Required]
        public int SupplierRFQId { get; set; }
        public SupplierRFQ SupplierRFQ { get; set; } = null!;

        public int LineNumber { get; set; }

        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(50)]
        public string? PartNumber { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [StringLength(20)]
        public string UOM { get; set; } = "EA";

        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Display(Name = "Required Date")]
        [DataType(DataType.Date)]
        public DateTime? RequiredDate { get; set; }

        // ── §17 demand linkage (optional) ──────────────────────────────────
        public int? ProductionSupplyDemandId { get; set; }
        public int? ProductionOrderId { get; set; }
        public int? BomLineId { get; set; }
        public int? OperationSequence { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// One supplier's response to an RFQ. Header carries the blended composite
    /// score + rank + winner flag + human-readable reason stamped by RankQuotes.
    /// </summary>
    public class SupplierQuote
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required]
        public int SupplierRFQId { get; set; }
        public SupplierRFQ SupplierRFQ { get; set; } = null!;

        [Required]
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; } = null!;

        public SupplierQuoteStatus Status { get; set; } = SupplierQuoteStatus.Invited;

        /// <summary>Supplier's own quote reference, if provided.</summary>
        [StringLength(60)]
        public string? VendorQuoteReference { get; set; }

        [Display(Name = "Received At (UTC)")]
        public DateTime? ReceivedAtUtc { get; set; }

        [Display(Name = "Valid Until")]
        [DataType(DataType.Date)]
        public DateTime? ValidUntilDate { get; set; }

        [Required, StringLength(3)]
        public string Currency { get; set; } = "USD";

        /// <summary>Header-level promised lead time (days) — the ranker's lead-time input.</summary>
        [Display(Name = "Lead Time (days)")]
        public int LeadTimeDays { get; set; }

        /// <summary>Sum of line extended prices — the ranker's price input.</summary>
        [Display(Name = "Total Quoted")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalQuotedAmount { get; set; }

        // ── Ranking output (stamped by RankQuotes) ──────────────────────────
        /// <summary>Blended composite score 0–100 (higher = better). Null until ranked.</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal? CompositeScore { get; set; }

        [Display(Name = "Rank")]
        public int? RankPosition { get; set; }

        [Display(Name = "Winner")]
        public bool IsWinner { get; set; }

        /// <summary>Why this quote ranked where it did — drives the comparison UI.</summary>
        [StringLength(500)]
        public string? ScoreReason { get; set; }

        /// <summary>SupplierOTD% used in the score (from PR-18), or null when no snapshot (fallback).</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal? SupplierOnTimeDeliveryPct { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<SupplierQuoteLine> Lines { get; set; } = new List<SupplierQuoteLine>();

        public byte[]? RowVersion { get; set; }
    }

    /// <summary>One supplier's quoted price for one RFQ line.</summary>
    public class SupplierQuoteLine
    {
        public int Id { get; set; }

        [Required]
        public int SupplierQuoteId { get; set; }
        public SupplierQuote SupplierQuote { get; set; } = null!;

        [Required]
        public int SupplierRFQLineId { get; set; }
        public SupplierRFQLine SupplierRFQLine { get; set; } = null!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal QuotedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal QuotedUnitPrice { get; set; }

        /// <summary>Extended price = QuotedQuantity × QuotedUnitPrice.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        /// <summary>Per-line lead time override (days); 0 = use the quote header lead time.</summary>
        [Display(Name = "Lead Time (days)")]
        public int LeadTimeDays { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
