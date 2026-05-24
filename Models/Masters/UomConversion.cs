using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-4 — UomConversion (overrides only).
    //
    // STANDARD in-category conversions are computed arithmetically from
    // UnitOfMeasureMaster.ConversionFactorToBase + ConversionOffsetToBase —
    // they DO NOT need a row here. UomConversion exists for the exceptions:
    //
    //   (1) PER-ITEM OVERRIDES — when a specific Item's pack/conversion
    //       differs from the category default. Example: a catch-weight
    //       fish Item where 1 EA actually weighs 0.45 KG (CATCH_WEIGHT pattern).
    //       Set ItemId = the fish item.
    //
    //   (2) CROSS-CATEGORY OVERRIDES — when two UOMs in DIFFERENT categories
    //       have a domain-specific conversion. Example: a CASE (PACKAGE
    //       category) of THIS item = 24 EA (COUNT category). Set ItemId NULL
    //       and CompanyId scoped.
    //
    // SHAPE — affine like UnitOfMeasureMaster:
    //   value_in_ToUom = Multiplier * value_in_FromUom + Offset
    //
    // TENANT TRIO — direct (not cross-tenant ref):
    //   CompanyId NOT NULL — every UomConversion override belongs to a tenant
    //   (system cross-category conversions seeded with reserved CompanyId? NO —
    //    use UomCategory/UnitOfMeasureMaster system rows + factors instead.
    //    UomConversion is ONLY for tenant-specific overrides.)
    //
    // UNIQUE per (CompanyId, FromUomId, ToUomId, ItemId or 0).
    // (Using a partial UNIQUE per the Replit prod-validator convention — see
    // PR #5c.1.1 gotcha.)
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §5.3
    //   - memory: reference_bic_entity_checklist.md
    // =============================================================================
    [Table("UomConversions")]
    public class UomConversion
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public int FromUomId { get; set; }

        public int ToUomId { get; set; }

        // NULL = company-wide cross-category override.
        // NOT NULL = per-item override (catch-weight, special pack).
        public int? ItemId { get; set; }

        [Column(TypeName = "numeric(28,12)")]
        public decimal Multiplier { get; set; } = 1m;

        [Column(TypeName = "numeric(28,12)")]
        public decimal Offset { get; set; } = 0m;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
