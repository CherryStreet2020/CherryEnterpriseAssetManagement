// Sprint 15.4 PR-19 — 3-Way Match (PO ↔ Receipt ↔ Invoice)
//
// Spec ref: docs/research/purchasing-cascade-design-2026-05-28.md PR-19
//   "IInvoiceMatchService — automated matching with tolerance rules. Upgrade
//    InvoiceMatchStatus from enum-only to active matching service. Exception
//    handling for price/qty/date mismatches."
//
// Design: a PERSISTED match run. IInvoiceMatchService.RunMatchAsync compares
// each VendorInvoiceLine against its linked PurchaseOrderLine (price/qty) and
// GoodsReceiptLine (received qty / receipt date), classifies the line outcome
// against configurable tolerances, and freezes one InvoiceMatchResult (header)
// + N InvoiceMatchResultLine rows. The aggregate outcome drives
// VendorInvoice.MatchStatus AND the Purchasing CC "Cost Exceptions" tab.
//
// One IsCurrent result per invoice (filtered unique index); re-running creates
// a fresh result and flips the prior to false (same IsCurrent pattern as PR-16
// /PR-17/PR-18). RunAndApproveIfCleanAsync posts the AP approval (incl PPV) via
// IApPostingService INSIDE the match transaction — match record + invoice
// approval + journal entry commit atomically (cross-service tx enlistment).

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    /// <summary>Aggregate outcome of a 3-way match run across all invoice lines.</summary>
    public enum InvoiceMatchOutcome
    {
        /// <summary>No line could be matched (all unlinked / no lines).</summary>
        NotMatched = 0,

        /// <summary>Every line matched the PO + receipt exactly.</summary>
        Matched = 1,

        /// <summary>All lines matched but at least one only within tolerance.</summary>
        MatchedWithinTolerance = 2,

        /// <summary>At least one line breached a tolerance — needs review before approval.</summary>
        Exception = 3
    }

    /// <summary>Per-line classification of the 3-way comparison.</summary>
    public enum InvoiceMatchLineOutcome
    {
        /// <summary>Invoice line has no linked PO line — cannot 3-way match.</summary>
        Unlinked = 0,

        /// <summary>Linked to a PO line but no receipt yet — cannot confirm delivery.</summary>
        NotReceived = 1,

        /// <summary>Invoiced qty exceeds received qty beyond tolerance (billed for undelivered goods).</summary>
        OverBilled = 2,

        /// <summary>Unit price differs from the PO beyond tolerance.</summary>
        PriceException = 3,

        /// <summary>Invoiced qty differs from received qty beyond tolerance (and not over-billing).</summary>
        QuantityException = 4,

        /// <summary>Invoice date is outside the tolerated window from the receipt date.</summary>
        DateException = 5,

        /// <summary>A non-zero variance exists but every dimension is within tolerance.</summary>
        WithinTolerance = 6,

        /// <summary>Exact match on price, qty, and date.</summary>
        Matched = 7
    }

    /// <summary>
    /// One frozen 3-way match run for a vendor invoice. Only one IsCurrent
    /// result per invoice (filtered unique index). The header caches counts +
    /// the total price variance so the Cost Exceptions tab + probe render
    /// without re-walking the lines, and snapshots the tolerances applied.
    /// </summary>
    public class InvoiceMatchResult
    {
        public int Id { get; set; }

        /// <summary>Tenant scope — filtered on every query.</summary>
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>The invoice this match run evaluated.</summary>
        [Required]
        public int VendorInvoiceId { get; set; }
        public VendorInvoice VendorInvoice { get; set; } = null!;

        /// <summary>Human-readable run number IM-YYYY-NNNNNN (two-phase numbered).</summary>
        [Required, StringLength(40)]
        [Display(Name = "Match Run Number")]
        public string MatchRunNumber { get; set; } = string.Empty;

        /// <summary>Aggregate outcome across all lines.</summary>
        public InvoiceMatchOutcome Outcome { get; set; } = InvoiceMatchOutcome.NotMatched;

        // ── Tolerance snapshot (what was applied this run) ──────────────────
        [Column(TypeName = "decimal(18,4)")]
        public decimal TolerancePriceAbs { get; set; }
        [Column(TypeName = "decimal(9,4)")]
        public decimal TolerancePricePct { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ToleranceQtyAbs { get; set; }
        [Column(TypeName = "decimal(9,4)")]
        public decimal ToleranceQtyPct { get; set; }
        public int ToleranceDateDays { get; set; }

        // ── Cached line counts ──────────────────────────────────────────────
        public int LinesTotal { get; set; }
        public int LinesMatched { get; set; }
        public int LinesWithinTolerance { get; set; }
        public int LinesException { get; set; }

        /// <summary>Net price variance summed across matched lines (invoice − PO).
        /// Positive = invoice billed above PO (unfavorable).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPriceVariance { get; set; }

        /// <summary>True for the latest run for this invoice.</summary>
        [Display(Name = "Current")]
        public bool IsCurrent { get; set; } = true;

        /// <summary>UTC timestamp the match was run.</summary>
        public DateTime RunAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>True if a clean match drove an AP approval posting in the same transaction.</summary>
        [Display(Name = "Posted On Match")]
        public bool PostedOnMatch { get; set; }

        /// <summary>JournalEntry id from the atomic AP approval (null when not posted).</summary>
        public int? PostedJournalEntryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<InvoiceMatchResultLine> Lines { get; set; }
            = new List<InvoiceMatchResultLine>();

        /// <summary>Optimistic concurrency via PG xmin (mapped fluent in AppDbContext).</summary>
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// One invoice line's 3-way comparison. Snapshots the invoice / PO / receipt
    /// values at run time plus the computed variances and the line outcome.
    /// </summary>
    public class InvoiceMatchResultLine
    {
        public int Id { get; set; }

        [Required]
        public int InvoiceMatchResultId { get; set; }
        public InvoiceMatchResult InvoiceMatchResult { get; set; } = null!;

        [Required]
        public int VendorInvoiceLineId { get; set; }
        public VendorInvoiceLine VendorInvoiceLine { get; set; } = null!;

        public int? PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        public int? GoodsReceiptLineId { get; set; }
        public GoodsReceiptLine? GoodsReceiptLine { get; set; }

        // ── Snapshots ───────────────────────────────────────────────────────
        [Column(TypeName = "decimal(18,4)")]
        public decimal InvoicedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal InvoicedUnitPrice { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal PoQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal PoUnitPrice { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReceivedQuantity { get; set; }

        // ── Computed variances ───────────────────────────────────────────────
        /// <summary>Invoiced unit price − PO unit price.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal PriceVariance { get; set; }
        /// <summary>Price variance as a percentage of PO unit price (null when PO price is 0).</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal? PriceVariancePct { get; set; }
        /// <summary>Invoiced qty − received qty.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityVariance { get; set; }
        /// <summary>Days between invoice date and receipt date (null when no receipt).</summary>
        public int? DateVarianceDays { get; set; }

        /// <summary>Extended price variance for this line: PriceVariance × matched qty.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtendedPriceVariance { get; set; }

        public InvoiceMatchLineOutcome Outcome { get; set; } = InvoiceMatchLineOutcome.Unlinked;

        [StringLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
