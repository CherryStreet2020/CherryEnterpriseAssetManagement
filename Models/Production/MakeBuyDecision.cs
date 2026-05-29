// Theme B7 Wave C PR-7 (2026-05-29) — MakeBuyDecision + MakeBuyDecisionPolicy.
//
// The "why" record behind every make-or-buy call. B7's duality says an item can be
// MADE in-house or BOUGHT from a supplier; for MakeBuyCode.MakeOrBuy items the
// IMakeBuyDecisionService (PR-8) evaluates six factors and decides — and writes ONE
// of these rows so the decision is auditable and re-explainable forever:
//   F1 eligibility (hard gate) · F2 capacity (now the REAL R4-10 Load% / R4-11
//   finite schedule — this is why B11 shipped first) · F3 cost delta · F4 break-even
//   · F5 lead-time fit · F6 quality/risk.
//
// Frozen snapshots capture the numbers AS DECIDED (cost, bottleneck WC + load%,
// completion/delivery dates, chosen supplier/quote) so a later re-run or a changed
// calendar can't rewrite history. FactorBreakdown holds the full per-factor jsonb.
//
// New TABLES ⇒ default initializers are safe (no backfill). xmin concurrency.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

/// <summary>What triggered the make-or-buy evaluation.</summary>
public enum MakeBuyDecisionContext
{
    ProductionOrder = 0,   // releasing or planning a production order
    ParentPoBomLine = 1,   // a component line under a PO-first parent
    Mrp = 2,               // MRP/demand generation
    ManualWhatIf = 3,      // a planner's what-if from the probe/cockpit
}

/// <summary>The verdict.</summary>
public enum MakeBuyOutcome
{
    Make = 0,
    Buy = 1,
}

/// <summary>The final tie-break when the score sits on the threshold.</summary>
public enum MakeBuyTieBreak
{
    PreferMake = 0,
    PreferBuy = 1,
}

/// <summary>
/// The audit / explainability record for one make-or-buy decision. Written by
/// <c>IMakeBuyDecisionService.DecideAsync</c>; replayable via <c>ExplainAsync</c>.
/// </summary>
[Table("MakeBuyDecisions")]
public class MakeBuyDecision
{
    public int Id { get; set; }

    // ── Tenant trio ─────────────────────────────────────────────
    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteIdSnapshot { get; set; }

    // ── Subject ─────────────────────────────────────────────────
    /// <summary>The item being made-or-bought. RESTRICT — an item outlives its decisions.</summary>
    [Required] public int ItemId { get; set; }
    public Item? Item { get; set; }

    [Column(TypeName = "decimal(18,4)")] public decimal Qty { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;

    // ── Trigger / source ────────────────────────────────────────
    public MakeBuyDecisionContext Context { get; set; } = MakeBuyDecisionContext.ManualWhatIf;
    /// <summary>Free-form source discriminator (e.g. "ProductionOrder", "ProductionSupplyDemand").</summary>
    [MaxLength(60)] public string? SourceType { get; set; }
    public int? SourceId { get; set; }

    // ── Verdict ─────────────────────────────────────────────────
    public MakeBuyOutcome Outcome { get; set; } = MakeBuyOutcome.Make;
    /// <summary>Aggregate buy score 0..1; ≥ policy threshold → BUY.</summary>
    [Column(TypeName = "decimal(6,4)")] public decimal BuyScore { get; set; }
    /// <summary>0..1 confidence in the verdict (how far the score is from the threshold + data completeness).</summary>
    [Column(TypeName = "decimal(6,4)")] public decimal Confidence { get; set; }

    public bool WasHardGated { get; set; } = false;
    [MaxLength(300)] public string? HardGateReason { get; set; }

    /// <summary>The top 2-3 plain-English lines a human reads.</summary>
    [MaxLength(2000)] public string? RationaleText { get; set; }

    /// <summary>Full per-factor breakdown (jsonb array of {code,label,score,weight,weightedImpact,reason}).</summary>
    [Column(TypeName = "jsonb")] public string? FactorBreakdown { get; set; }

    // ── Frozen snapshots (as-decided; never recomputed) ─────────
    [Column(TypeName = "decimal(18,4)")] public decimal? MakeCostFullyLoaded { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? BuyCostLanded { get; set; }
    [MaxLength(50)] public string? BottleneckWorkCenterCode { get; set; }
    [Column(TypeName = "decimal(7,2)")] public decimal? BottleneckLoadPct { get; set; }
    public bool RoutedThroughDrum { get; set; } = false;
    public DateTime? MakeCompletionDate { get; set; }
    public DateTime? VendorDeliveryDate { get; set; }
    public int? ChosenSupplierId { get; set; }   // snapshot (Vendor id) — no FK, decision outlives vendor changes
    public int? ChosenQuoteId { get; set; }       // snapshot (SupplierQuote id) — no FK

    // ── Audit + concurrency ─────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Per-tenant/site policy: thresholds + factor weights + final tie-break that the
/// decision service reads. One row per (CompanyId, SiteId); SiteId null = company default.
/// </summary>
[Table("MakeBuyDecisionPolicies")]
public class MakeBuyDecisionPolicy
{
    public int Id { get; set; }

    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    /// <summary>Capacity load% at/above which the make path is considered constrained (drum pressure).</summary>
    [Column(TypeName = "decimal(7,2)")] public decimal CapacityThresholdPct { get; set; } = 85m;
    /// <summary>If routing through the drum, force BUY when buy is within this % cost premium of make.</summary>
    [Column(TypeName = "decimal(7,2)")] public decimal DrumOffloadCostTolerancePct { get; set; } = 8m;
    /// <summary>Aggregate buy score at/above which the verdict is BUY.</summary>
    [Column(TypeName = "decimal(6,4)")] public decimal BuyDecisionScoreThreshold { get; set; } = 0.50m;

    // Factor weights (F1 is a hard gate, unweighted). Defaults sum to 1.0.
    [Column(TypeName = "decimal(6,4)")] public decimal WeightCapacity { get; set; } = 0.25m;     // F2
    [Column(TypeName = "decimal(6,4)")] public decimal WeightCostDelta { get; set; } = 0.30m;    // F3
    [Column(TypeName = "decimal(6,4)")] public decimal WeightBreakEven { get; set; } = 0.10m;    // F4
    [Column(TypeName = "decimal(6,4)")] public decimal WeightLeadTime { get; set; } = 0.20m;     // F5
    [Column(TypeName = "decimal(6,4)")] public decimal WeightQualityRisk { get; set; } = 0.15m;  // F6

    public MakeBuyTieBreak FinalTieBreak { get; set; } = MakeBuyTieBreak.PreferMake;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }
    public byte[]? RowVersion { get; set; }
}
