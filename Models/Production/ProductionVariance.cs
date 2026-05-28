// Sprint 14.4 PR-4 (2026-05-28) — Production Variance + Close entities.
//
// Per Dean's research spec:
//   §6  — 5 cost views (Estimated, Current Planned, Actual, Committed, EAC)
//   §12 — Cost status logic (Estimated → InWip → ... → ClosedSettled)
//   §13 — Cost exceptions
//   §17 — Better-than-big-boys: variance to estimate visible at cockpit level
//
// 5 variance types per standard cost accounting:
//   Material Usage  — (actual qty - std qty) × std price
//   Labor Rate      — (actual rate - std rate) × actual hours
//   Labor Efficiency — (actual hours - std hours) × std rate
//   Overhead Volume — (actual volume - budgeted volume) × std OH rate
//   Overhead Spending — actual OH - (actual volume × std OH rate)
//
// Close workflow: compute variances → post variance JEs → stamp columns
// → set ProductionCostStatus.ClosedSettled → freeze cost → immutable.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// ═══════════════════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════════════════

/// <summary>Which variance type.</summary>
public enum ProductionVarianceType
{
    /// <summary>(Actual material qty - Standard qty) × Standard unit price.</summary>
    MaterialUsage = 0,

    /// <summary>(Actual labor rate - Standard rate) × Actual hours.</summary>
    LaborRate = 1,

    /// <summary>(Actual hours - Standard hours) × Standard labor rate.</summary>
    LaborEfficiency = 2,

    /// <summary>(Actual volume - Budgeted volume) × Standard OH rate.</summary>
    OverheadVolume = 3,

    /// <summary>Actual OH spend - (Actual volume × Standard OH rate).</summary>
    OverheadSpending = 4,

    /// <summary>Actual subcontract cost vs estimated subcontract cost.</summary>
    SubcontractVariance = 5,

    /// <summary>Purchase price variance on bought-to-job material.</summary>
    PurchasePriceVariance = 6,

    /// <summary>Scrap variance vs planned scrap allowance.</summary>
    ScrapVariance = 7,

    /// <summary>Net total variance (catch-all for non-decomposed delta).</summary>
    TotalVariance = 8,
}

/// <summary>What step of the close workflow.</summary>
public enum ProductionCloseStep
{
    VarianceComputed = 0,
    VarianceJePosted = 1,
    WipCleared = 2,
    CostFrozen = 3,
    StatusSetClosed = 4,
    CloseComplete = 5,
    CloseReversed = 6,
}

// ═══════════════════════════════════════════════════════════════════
// PRODUCTION VARIANCE — individual variance computation
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// One variance line for a production order. Computed during PRO close
/// by comparing estimated (standard) cost vs actual cost. Posted to GL
/// via variance accounts (GlAccountKind 630-634).
/// </summary>
public class ProductionVariance
{
    public int Id { get; set; }

    // ── Tenant ──────────────────────────────────────────────────
    public int? TenantId { get; set; }
    public int CompanyId { get; set; }

    // ── Identity ────────────────────────────────────────────────
    public int ProductionOrderId { get; set; }
    public ProductionVarianceType VarianceType { get; set; }

    // ── Variance computation ────────────────────────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VarianceAmount { get; set; }

    /// <summary>Variance as percentage: (Actual - Estimated) / Estimated × 100.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? VariancePercent { get; set; }

    // ── Favorable/Unfavorable ───────────────────────────────────
    /// <summary>True if actual < estimated (favorable for cost, unfavorable for revenue).</summary>
    public bool IsFavorable { get; set; }

    // ── Quantity-based variance detail ──────────────────────────
    /// <summary>For material/labor: standard quantity.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? StandardQuantity { get; set; }

    /// <summary>For material/labor: actual quantity.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? ActualQuantity { get; set; }

    /// <summary>For material/labor: standard unit rate.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? StandardRate { get; set; }

    /// <summary>For material/labor: actual unit rate.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? ActualRate { get; set; }

    // ── GL posting ──────────────────────────────────────────────
    public int? JournalEntryId { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedAtUtc { get; set; }

    // ── Notes ───────────────────────────────────────────────────
    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Audit ───────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    // ── Concurrency (xmin) ──────────────────────────────────────
    public byte[]? RowVersion { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
// PRODUCTION CLOSE EVENT — audit trail for close workflow
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Records each step of the PRO close workflow. Immutable audit trail
/// showing who closed, when, what variances were computed, and what
/// JEs were posted. Supports controlled post-close reversal.
/// </summary>
public class ProductionCloseEvent
{
    public int Id { get; set; }

    // ── Tenant ──────────────────────────────────────────────────
    public int? TenantId { get; set; }
    public int CompanyId { get; set; }

    // ── Identity ────────────────────────────────────────────────
    public int ProductionOrderId { get; set; }
    public ProductionCloseStep Step { get; set; }

    // ── Amounts at close ────────────────────────────────────────
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedTotalAtClose { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualTotalAtClose { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalVarianceAtClose { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WipBalanceAtClose { get; set; }

    // ── Variance count ──────────────────────────────────────────
    public int VarianceLineCount { get; set; }
    public int VarianceJeCount { get; set; }

    // ── Exception count at close ────────────────────────────────
    public int UnresolvedExceptionCount { get; set; }

    // ── Close result ────────────────────────────────────────────
    public bool CloseSuccessful { get; set; }
    [MaxLength(500)]
    public string? CloseMessage { get; set; }

    // ── Reversal support ────────────────────────────────────────
    public bool IsReversal { get; set; }
    public int? ReversalOfEventId { get; set; }

    // ── Audit ───────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? ClosedBy { get; set; }

    // ── Concurrency (xmin) ──────────────────────────────────────
    public byte[]? RowVersion { get; set; }
}
