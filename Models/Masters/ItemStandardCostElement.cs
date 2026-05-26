// B6 Foundation Sprint PR-FS-3 (2026-05-26) — ItemStandardCostElement entity.
//
// SAP Cost Component Split equivalent (CK11N output). Per-Item / optional per-Site
// cost element breakdown stored as separate rows so the Standard Cost on the
// Item Master decomposes into:
//
//     StandardCost = Material + Labor + VariableOverhead + FixedOverhead
//                  + Subcontract + Setup + Tooling + Other
//
// Why broken out:
//   - **Cost rollup** (Sprint 14.4): parent BOM's Material cost = sum of children's
//     TOTAL cost. Without the split, a parent that has both purchased + produced
//     children rolls up incorrectly.
//   - **Variance analysis**: Material variance vs. Labor variance vs. OH variance
//     all attribute to different posting profile keys (PRA-7 / ADR-019).
//   - **CFO cost-composition reporting**: "of this $185K product, 42% material,
//     31% labor, 18% OH, 9% subcontract." Cherry voice integration.
//   - **AS9100 / DCAA compliance**: cost element segregation required for govt
//     contract pricing audits + indirect-cost-pool reporting.
//   - **Make-vs-buy decisions** (Theme B7): per-element comparison of internal
//     production cost vs. vendor quote.
//
// Effective-dating: every row has EffectiveFrom + nullable EffectiveTo. New cost
// estimates supersede prior ones by inserting a new row with EffectiveFrom=now;
// the prior row's EffectiveTo gets stamped by the service. Audit-friendly.
//
// Per-Site override: SiteId nullable. If set, this row scopes to a specific
// Site (e.g., per-plant labor rate differs because wage bands differ). Cascade:
//   Per-Site cost (SiteId IS NOT NULL)  →  Item-level cost (SiteId IS NULL)  →  $0
//
// Tenant trio: TenantId / CompanyId / SiteId. Null-safe partial unique index
// pattern from [[reference_bic_entity_checklist]] (lesson encoded after PR-FS-2's
// Codex P1 catch).
//
// HARD LOCK B6 GO BIG: full BIC field set. NOT a Minimal subset (which would just
// have a single StandardCost column with no breakdown).

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Models.Masters
{
    /// <summary>
    /// Cost element category. Drives GL posting selection + rollup math +
    /// variance attribution. Aligned with SAP Cost Component Structure
    /// (Material, Wages, Overhead, etc.) and AS9100 / DCAA cost segregation.
    /// </summary>
    public enum CostElementType
    {
        /// <summary>Direct material — raw inputs consumed in production.</summary>
        Material = 0,

        /// <summary>Direct labor — touch-labor hours on routing operations.</summary>
        Labor = 1,

        /// <summary>Variable manufacturing overhead — utilities, supplies, consumables.</summary>
        VariableOverhead = 2,

        /// <summary>Fixed manufacturing overhead — depreciation, supervisor salary, facility.</summary>
        FixedOverhead = 3,

        /// <summary>Subcontract operation cost — outside-process line item from vendor.</summary>
        Subcontract = 4,

        /// <summary>Setup/changeover labor + machine time (amortized per piece).</summary>
        Setup = 5,

        /// <summary>Perishable tooling / amortized fixturing (per piece).</summary>
        Tooling = 6,

        /// <summary>Catch-all for additional cost pools (freight-in, scrap allowance, etc.).</summary>
        Other = 99,
    }

    /// <summary>
    /// Source of a cost element value — drives audit + re-computation rules.
    /// </summary>
    public enum CostElementSource
    {
        /// <summary>Operator entered manually.</summary>
        Manual = 0,

        /// <summary>Rolled up from BOM children via the cost engine (Sprint 14.4).</summary>
        RolledUp = 1,

        /// <summary>Imported from external system (CSV / ERP feed).</summary>
        Imported = 2,

        /// <summary>Calculated from labor rate × routing time / OH application rate / etc.</summary>
        Calculated = 3,
    }

    /// <summary>
    /// Per-(Item, optional Site, CostElementType, effective date) cost element row.
    /// SAP Cost Component Split equivalent. Effective-dated for audit history.
    /// </summary>
    public class ItemStandardCostElement
    {
        public int Id { get; set; }

        // ===== Identity + tenant trio =====================================

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        /// <summary>
        /// Optional per-Site scope. NULL = Item-level cost (applies to every
        /// Site unless overridden). Cascade order:
        ///   per-Site (SiteId IS NOT NULL) → Item-level (SiteId IS NULL) → $0
        /// </summary>
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public int? TenantId { get; set; }
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        // ===== Cost composition ===========================================

        [Required]
        [Display(Name = "Cost Element Type")]
        public CostElementType ElementType { get; set; }

        [Display(Name = "Amount")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// ISO 4217 currency code. Default USD. Multi-currency cost engine
        /// deferred to Sprint 16+ — for now every row is the tenant base
        /// currency.
        /// </summary>
        [Required, StringLength(3)]
        [Display(Name = "Currency")]
        public string CurrencyCode { get; set; } = "USD";

        [Display(Name = "Source")]
        public CostElementSource Source { get; set; } = CostElementSource.Manual;

        // ===== Effective-dating ===========================================

        [Required]
        [Display(Name = "Effective From")]
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Null means "this row is current." Service stamps a value when a
        /// newer row supersedes this one.
        /// </summary>
        [Display(Name = "Effective To")]
        public DateTime? EffectiveToUtc { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        // ===== Calculation provenance =====================================
        //
        // For Source=Calculated rows, store the inputs so the value is
        // reproducible. e.g., "RoutingTime=0.42 hr × LaborRate=$58.50/hr = $24.57"

        [StringLength(500)]
        [Display(Name = "Calculation Notes")]
        public string? CalculationNotes { get; set; }

        // ===== Audit =======================================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
