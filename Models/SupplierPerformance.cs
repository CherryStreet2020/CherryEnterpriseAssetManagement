// Sprint 15.4 PR-18 — Vendor Performance / Scorecard
//
// Spec ref: docs/research/purchasing-subcontracting-supply-demand-dean-research.txt
//   §21 tab 13 "Supplier Performance — OTD, quality, price variance"
//   §25 Reports & KPIs: "Supplier OTD" / "Supplier quality PPM" /
//        "Purchase price variance" / "Supplier NCR count"
// Cascade ref: docs/research/purchasing-cascade-design-2026-05-28.md PR-18
//
// Design principle: SupplierPerformance is a COMPUTED SNAPSHOT, not a live
// transaction. ISupplierPerformanceService.RecomputeAsync derives the four
// scorecard metrics from existing purchasing/quality facts (GoodsReceipt(Line),
// PurchaseOrder(Line), Item.StandardCost, CorrectiveActionRequest) and freezes
// them into one row per (Vendor, PeriodType). The row is immutable evidence of
// what the metrics were at ComputedAtUtc — recompute creates a fresh snapshot
// and flips the prior IsCurrent to false, preserving history (same IsCurrent
// pattern as POAcknowledgment / POChangeHistory in this sprint).
//
// PR-20 dependency (locked): the RFQ/Quote composite-score ranker consumes
// OnTimeDeliveryPct + QualityPPM + PriceVariancePct off the current
// Rolling90Days snapshot. Those three fields are first-class columns here so
// PR-20 composes with a single read and no recomputation. Keep them stable.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Rolling window a SupplierPerformance snapshot summarizes. The service
    /// computes [PeriodStartUtc, PeriodEndUtc] from this + the recompute clock.
    /// </summary>
    public enum SupplierPerformancePeriod
    {
        /// <summary>Trailing 30 days from the recompute timestamp.</summary>
        Rolling30Days = 0,

        /// <summary>Trailing 90 days from the recompute timestamp. The default
        /// window PR-20's quote ranker reads.</summary>
        Rolling90Days = 1,

        /// <summary>January 1 of the recompute year through the recompute timestamp.</summary>
        YearToDate = 2
    }

    /// <summary>
    /// A frozen scorecard for one supplier over one rolling window. Multiple
    /// rows exist per (Vendor, PeriodType) over time; only one has
    /// IsCurrent=true (enforced by a filtered unique index). The four headline
    /// metrics — OTD %, quality PPM, price variance %, NCR count — are derived
    /// from receipts, POs, item standard cost, and CARs at ComputedAtUtc.
    /// </summary>
    public class SupplierPerformance
    {
        public int Id { get; set; }

        /// <summary>Tenant scope — filtered on every query.</summary>
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>The supplier this scorecard summarizes.</summary>
        [Required]
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; } = null!;

        /// <summary>Which rolling window this snapshot covers.</summary>
        public SupplierPerformancePeriod PeriodType { get; set; }
            = SupplierPerformancePeriod.Rolling90Days;

        /// <summary>Inclusive start of the window (UTC).</summary>
        [Display(Name = "Period Start (UTC)")]
        public DateTime PeriodStartUtc { get; set; }

        /// <summary>Inclusive end of the window (UTC) — typically the recompute clock.</summary>
        [Display(Name = "Period End (UTC)")]
        public DateTime PeriodEndUtc { get; set; }

        /// <summary>When this snapshot was computed.</summary>
        [Display(Name = "Computed At (UTC)")]
        public DateTime ComputedAtUtc { get; set; } = DateTime.UtcNow;

        // ─── Delivery (OTD) ─────────────────────────────────────────────────

        /// <summary>
        /// Receipt events in the window that had a comparable required/promise
        /// date (the OTD denominator). Receipts with no required date are
        /// excluded from the OTD basis rather than silently counted on-time.
        /// </summary>
        [Display(Name = "Receipt Events (OTD Basis)")]
        public int ReceiptEventsTotal { get; set; }

        /// <summary>Receipt events delivered on or before the required date.</summary>
        [Display(Name = "Receipt Events On Time")]
        public int ReceiptEventsOnTime { get; set; }

        /// <summary>
        /// On-time delivery percentage = ReceiptEventsOnTime / ReceiptEventsTotal
        /// * 100. Null when there were no datable receipt events in the window
        /// (no basis — distinct from 0% which means "had receipts, all late").
        /// PR-20 ranker input.
        /// </summary>
        [Display(Name = "On-Time Delivery %")]
        [Column(TypeName = "decimal(9,4)")]
        public decimal? OnTimeDeliveryPct { get; set; }

        // ─── Quality (PPM + NCR) ────────────────────────────────────────────

        /// <summary>Total quantity received from this supplier in the window.</summary>
        [Display(Name = "Quantity Received")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReceivedTotal { get; set; }

        /// <summary>Total quantity rejected at incoming inspection in the window.</summary>
        [Display(Name = "Quantity Rejected")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityRejectedTotal { get; set; }

        /// <summary>
        /// Quality defects in parts-per-million = QuantityRejected /
        /// QuantityReceived * 1,000,000. Null when nothing was received in the
        /// window (no basis). PR-20 ranker input.
        /// </summary>
        [Display(Name = "Quality PPM")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? QualityPPM { get; set; }

        /// <summary>
        /// Count of Corrective Action Requests raised against this supplier in
        /// the window (CorrectiveActionRequest.VendorId == this vendor). A
        /// supplier NCR proxy independent of the PPM volume metric.
        /// </summary>
        [Display(Name = "NCR Count")]
        public int NcrCount { get; set; }

        // ─── Price variance (PPV) ───────────────────────────────────────────

        /// <summary>
        /// Number of received lines that had an Item standard cost &gt; 0 and
        /// therefore contributed to the PriceVariancePct basis. Lines for
        /// non-Item-Master parts or items without a standard cost are excluded.
        /// </summary>
        [Display(Name = "Price Variance Basis Lines")]
        public int PriceVarianceBasisLineCount { get; set; }

        /// <summary>
        /// Sum over basis lines of (QuantityReceived * Item.StandardCost) — the
        /// expected/standard spend denominator for PPV. Stored so the percentage
        /// is auditable and PR-20 can re-weight if needed.
        /// </summary>
        [Display(Name = "Standard Cost Basis")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal StandardCostBasisAmount { get; set; }

        /// <summary>
        /// Sum over basis lines of (QuantityReceived * PO line UnitPrice) — the
        /// actual spend numerator for PPV.
        /// </summary>
        [Display(Name = "Actual Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ActualCostAmount { get; set; }

        /// <summary>
        /// Purchase price variance percentage =
        /// (ActualCostAmount - StandardCostBasisAmount) / StandardCostBasisAmount
        /// * 100. Positive = paid above standard (unfavorable). Null when no
        /// basis lines had a standard cost in the window. PR-20 ranker input.
        /// </summary>
        [Display(Name = "Price Variance %")]
        [Column(TypeName = "decimal(9,4)")]
        public decimal? PriceVariancePct { get; set; }

        // ─── Snapshot lifecycle ─────────────────────────────────────────────

        /// <summary>
        /// True if this is the latest snapshot for the (Vendor, PeriodType)
        /// pair. Flipped to false when the next RecomputeAsync for the same
        /// pair lands. A filtered unique index enforces one IsCurrent row per
        /// pair. Read this to filter history; read ComputedAtUtc to age the
        /// scorecard.
        /// </summary>
        [Display(Name = "Current")]
        public bool IsCurrent { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Optimistic concurrency via PG xmin (mapped fluent in AppDbContext).</summary>
        public byte[]? RowVersion { get; set; }
    }
}
