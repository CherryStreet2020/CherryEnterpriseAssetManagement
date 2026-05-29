// Theme B7 Wave B PR-4 (2026-05-29) — ItemCrystallization.
//
// THE FLAGSHIP DIFFERENTIATOR's audit record.
//
// Crystallization is "harvest-from-actuals, at ship, optional, reversible,
// dedupe-aware." A PoFirst (master-optional / ETO) Production Order builds
// with NO Item Master — the PO's frozen BOM + as-run routing IS the standard
// during the build. When the job ships, ONE explicit, optional action
// (PR-5 IItemCrystallizationService.CrystallizeAsync) promotes the finished
// order's actuals into a new — or deduped-and-linked — Item Master + standard
// BOM + standard Routing + standard cost (= first-unit actual, §5.4).
//
// No incumbent ERP does this from AS-RUN ACTUALS:
//   - SAP CO07 gives material-less build but FORFEITS standard costing/variance
//     (settles to a cost center; no material receiver → no variance).
//   - Oracle/Epicor crystallize but PRE-BUILD, from a template you authored
//     before cutting metal.
//   - PLM (Aras/Teamcenter) captures as-built but NEVER pushes it into ERP as
//     a costed, plannable standard.
// We unify PLM-as-built + ERP-standard ON THE SHIP EVENT.
//
// THIS ENTITY is the event/audit record of one crystallization: a frozen,
// reversible, auditable snapshot. It records WHAT was harvested, the dedupe
// outcome, the cost basis, and the as-built configuration lineage (drawing #
// + rev + ECO effectivity) preserved as the master's origin record per
// AS9100D §8.5.2/§8.5.6.
//
// COMPLIANCE POSTURE (§7): crystallization is a MASTER-DATA CONVENIENCE, NOT a
// compliance event. The crystallized part #/rev MUST equal the FAIR / traveler
// / shipper value; lot-serial genealogy is preserved on the as-built records,
// never rewritten. This entity therefore freezes AsBuiltPartNumber / Rev /
// Drawing / Rev / EcoEffectivity so PR-5's CrystallizeAsync can REJECT a
// contradiction (mint that disagrees with the as-built record).
//
// LOCKS APPLIED FROM DAY ONE (per B6/B7 hard-locks):
//   - Tenant trio (TenantId? + CompanyId + SiteId), mirrors CostTransaction.
//   - Enum HasDefaultValue == CLR sentinel 0 (Outcome=Pending, CostSource=
//     FirstActual) — value-0 semantic defaults, HasDefaultValue safe.
//   - Two-phase numbering for CrystallizationNumber (CRYST-PEND-{guid} →
//     CRYST-YYYY-{Id:D6}) — eliminates the CountAsync race.
//   - xmin RowVersion (MapXminRowVersion) — never IsRowVersion()+bytea.
//   - StructureFingerprintHash = SHA-256 over as-built BOM + as-run routing
//     (reuses the ProductionMaterialStructure.ChildItemFingerprintHash SHA-256
//     pattern — see Services/Production/CrystallizationFingerprint.cs).
//   - Two distinct FKs to Item (CreatedItemId / MatchedItemId) — each SET NULL
//     so the audit record survives master archival.
//   - SourceProductionOrderId RESTRICT — a crystallized PO can't be deleted out
//     from under its audit trail.
//
// REFERENCES:
//   - docs/research/po-as-standard-make-or-buy-dean-research.md §3 (crystallization)
//     + §5.4 (first-actual cost) + §7 (compliance).
//   - docs/research/b7-cascade-design.md §"PR-4" / §"PR-5".
//   - Models/Production/ProductionMaterialStructure.cs — as-built BOM source +
//     the SHA-256 fingerprint pattern this reuses.
//   - Models/Production/ProductionOperation.cs — as-run routing source.
//   - Models/Production/CostTransaction.cs — ProductionOrderCostSummary, the
//     first-actual cost source (PR-6 seeds SeededStandardCost from it).

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// ═══════════════════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// The result of a crystallization attempt. <see cref="Pending"/> (value 0,
/// the CLR + DB default) is a stub being prepared (PR-4 probe / pre-commit);
/// the terminal outcomes are set atomically by PR-5's CrystallizeAsync.
/// </summary>
public enum CrystallizationOutcome
{
    /// <summary>Record prepared, not yet resolved (stub). Semantic default — value 0.</summary>
    Pending = 0,

    /// <summary>A brand-new Item Master was minted from the as-built actuals.</summary>
    CreatedNewItem = 1,

    /// <summary>The as-built fingerprint matched an existing standard; linked instead of created (human-confirmed, §3.3 / decision #3).</summary>
    LinkedToExisting = 2,

    /// <summary>Crystallization was rejected (compliance contradiction, or operator declined). No master change.</summary>
    Rejected = 3,
}

/// <summary>
/// How the crystallized standard cost was derived. <see cref="FirstActual"/>
/// (value 0, the default) is the §5.4 rule: an ETO-originated master's standard
/// cost = the first-unit actual, flagged "unvalidated for repeat" until a 2nd
/// run or a deliberate standard-set promotes it.
/// </summary>
public enum CrystallizationCostSource
{
    /// <summary>Standard cost = first-unit actual from ProductionOrderCostSummary (§5.4). Default — value 0.</summary>
    FirstActual = 0,

    /// <summary>Standard cost = moving-average actual across completed units.</summary>
    MovingAverage = 1,

    /// <summary>Standard cost = a manual override entered by the operator at crystallize time.</summary>
    ManualOverride = 2,
}

// ═══════════════════════════════════════════════════════════════════
// ENTITY
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// The audit/event record of one crystallization-at-ship: the moment a
/// master-optional (PoFirst / ETO) Production Order's as-built BOM, as-run
/// routing, and actual cost were promoted into a reusable Item Master standard
/// — or deduped and linked to an existing one. Frozen, reversible, auditable.
/// </summary>
[Table("ItemCrystallizations")]
public class ItemCrystallization
{
    public int Id { get; set; }

    // ── Tenant trio (mirrors CostTransaction) ───────────────────
    public int? TenantId { get; set; }

    [Required]
    public int CompanyId { get; set; }

    public int? SiteId { get; set; }

    // ── Source (the as-built Production Order) ──────────────────
    /// <summary>
    /// The PoFirst Production Order whose actuals are being crystallized.
    /// RESTRICT on delete — a crystallized order can't be removed out from
    /// under its audit trail.
    /// </summary>
    [Required]
    public int SourceProductionOrderId { get; set; }
    public ProductionOrder? SourceProductionOrder { get; set; }

    // ── Identity ────────────────────────────────────────────────
    /// <summary>
    /// Human-readable crystallization number, two-phase
    /// <c>CRYST-YYYY-NNNNNN</c> (placeholder <c>CRYST-PEND-{guid}</c> until the
    /// Id is assigned, then patched). Unique per (CompanyId, CrystallizationNumber).
    /// </summary>
    [Required]
    [MaxLength(40)]
    [Display(Name = "Crystallization #")]
    public string CrystallizationNumber { get; set; } = string.Empty;

    // ── Outcome + the resulting / matched master ────────────────
    /// <summary>The result — Pending stub, CreatedNewItem, LinkedToExisting, or Rejected.</summary>
    public CrystallizationOutcome Outcome { get; set; } = CrystallizationOutcome.Pending;

    /// <summary>FK to the minted Item Master when <see cref="Outcome"/> == CreatedNewItem. SET NULL on item delete.</summary>
    public int? CreatedItemId { get; set; }
    public Item? CreatedItem { get; set; }

    /// <summary>FK to the existing standard when <see cref="Outcome"/> == LinkedToExisting (dedupe hit). SET NULL on item delete.</summary>
    public int? MatchedItemId { get; set; }
    public Item? MatchedItem { get; set; }

    // ── The dedupe fingerprint ──────────────────────────────────
    /// <summary>
    /// SHA-256 (lower-case hex, 64 chars) over the as-built BOM
    /// (ProductionMaterialStructure lines) + as-run routing
    /// (ProductionOperation rows) of the source PRO. Identical structures
    /// produce identical hashes — that's the dedupe key surfaced to the human
    /// as "this matches TI-BRKT-0042 Rev C — link instead of create."
    /// </summary>
    [MaxLength(64)]
    [Display(Name = "Structure Fingerprint")]
    public string? StructureFingerprintHash { get; set; }

    // ── Cost basis ──────────────────────────────────────────────
    /// <summary>The standard cost seeded onto the minted master (= first-unit actual by default, §5.4).</summary>
    [Column(TypeName = "decimal(18,4)")]
    [Display(Name = "Seeded Standard Cost")]
    public decimal? SeededStandardCost { get; set; }

    /// <summary>How the seeded standard cost was derived. Default FirstActual (§5.4).</summary>
    public CrystallizationCostSource CostSource { get; set; } = CrystallizationCostSource.FirstActual;

    // ── As-built configuration lineage (frozen — compliance anchor §7) ──
    /// <summary>As-built part number — MUST equal the FAIR/traveler/shipper value. PR-5 rejects a contradicting mint.</summary>
    [MaxLength(64)]
    [Display(Name = "As-Built Part #")]
    public string? AsBuiltPartNumber { get; set; }

    /// <summary>As-built part revision — frozen at crystallize time.</summary>
    [MaxLength(16)]
    [Display(Name = "As-Built Part Rev")]
    public string? AsBuiltPartRev { get; set; }

    /// <summary>As-built drawing number that governed the build (AS9100D §8.5.2).</summary>
    [MaxLength(64)]
    [Display(Name = "As-Built Drawing #")]
    public string? AsBuiltDrawingNumber { get; set; }

    /// <summary>As-built drawing revision that governed the build (AS9100D §8.5.2).</summary>
    [MaxLength(16)]
    [Display(Name = "As-Built Drawing Rev")]
    public string? AsBuiltDrawingRev { get; set; }

    /// <summary>ECO effectivity that governed the build (AS9100D §8.5.6) — date/unit/serial as captured.</summary>
    [MaxLength(64)]
    [Display(Name = "As-Built ECO Effectivity")]
    public string? AsBuiltEcoEffectivity { get; set; }

    // ── Rationale ───────────────────────────────────────────────
    /// <summary>Plain-language: what was harvested, the dedupe result, and the cost basis.</summary>
    [MaxLength(2000)]
    [Display(Name = "Rationale")]
    public string? RationaleText { get; set; }

    // ── Crystallize event ───────────────────────────────────────
    public DateTime CrystallizedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    [Display(Name = "Crystallized By")]
    public string? CrystallizedBy { get; set; }

    // ── Reversal (never rewrites as-built history) ──────────────
    public bool IsReversed { get; set; } = false;
    public DateTime? ReversedAtUtc { get; set; }

    [MaxLength(100)]
    public string? ReversedBy { get; set; }

    [MaxLength(500)]
    public string? ReversalReason { get; set; }

    // ── Audit ───────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    // ── Concurrency (xmin) ──────────────────────────────────────
    public byte[]? RowVersion { get; set; }
}
