using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-4 — UnitOfMeasureMaster (the master UOM table).
    //
    // CLASS NAME: deliberately `UnitOfMeasureMaster` to avoid collision with the
    // two existing enums:
    //   - Models/Item.UnitOfMeasure (22-value inventory enum, [Obsolete] after this PR)
    //   - Models/Telemetry/UnitOfMeasure.cs (40+ sensor enum, also marked for
    //     deprecation after Phase B converges into this table)
    //
    // Table name: "UnitsOfMeasure" (plural; cleaner than UnitOfMeasureMasters).
    //
    // CONVERSION SHAPE — affine to the category's base unit:
    //   value_in_base = ConversionFactorToBase * value + ConversionOffsetToBase
    //
    // Example (METER as Length base):
    //   METER: Factor=1,   Offset=0
    //   INCH:  Factor=0.0254, Offset=0
    //   FEET:  Factor=0.3048, Offset=0
    //
    // Example (KELVIN as Temperature base):
    //   KELVIN:     Factor=1,    Offset=0
    //   CELSIUS:    Factor=1,    Offset=273.15
    //   FAHRENHEIT: Factor=5/9,  Offset=255.3722222...
    //
    // In-category conversion A → B is computed arithmetically without consulting
    // UomConversion. UomConversion ONLY carries:
    //   (a) per-item overrides (catch-weight: 1 EA of THIS fish = 0.45 KG)
    //   (b) cross-category overrides (1 COUNT_PACK = 24 EA — only when COUNT_PACK
    //       is in a different category than EA, e.g. PACKAGE category)
    //
    // CROSS-TENANT REFERENCE (mirrors ReasonCode):
    //   CompanyId NULL  = system row
    //   CompanyId set   = tenant-specific
    // Two partial UNIQUEs on (Code) prefixed by NULL-vs-NOT-NULL CompanyId.
    //
    // DECIMAL PRECISION per UOM: how many decimal places this UOM rounds to in
    // UI / posting. EA → 0; KG → 3; LITER → 3; CELSIUS → 2; PSI → 1. Default 4.
    //
    // ISO + UNECE + UCUM codes:
    //   IsoCode    — ISO 80000 quantity-and-units symbol (e.g. "m", "kg", "K")
    //   UneceCode  — UNECE Recommendation 20 trade code (e.g. "MTR", "KGM", "EA")
    //   UcumCode   — Unified Code for Units of Measure (used in FDA + HL7 + EU
    //                health-care submissions). Optional unless pharma vertical.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §5.3
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - memory: feedback_no_shortcuts_multi_tenant_lineage.md
    //   - existing affine pattern: Models/Telemetry/UnitConversion.cs
    // =============================================================================
    [Table("UnitsOfMeasure")]
    public class UnitOfMeasureMaster
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Short display glyph (e.g. "kg", "°C", "EA").
        [Required, StringLength(16)]
        public string Symbol { get; set; } = string.Empty;

        public int UomCategoryId { get; set; }

        // Affine factor to the category's BaseUom.
        [Column(TypeName = "numeric(28,12)")]
        public decimal ConversionFactorToBase { get; set; } = 1m;

        // Affine offset to the category's BaseUom (only Temperature uses this today).
        [Column(TypeName = "numeric(28,12)")]
        public decimal ConversionOffsetToBase { get; set; } = 0m;

        // UI / posting rounding precision (0-12).
        public int DecimalPrecision { get; set; } = 4;

        // ISO 80000 / 80000-1 quantity-and-units symbol (case-sensitive).
        [StringLength(16)]
        public string? IsoCode { get; set; }

        // UNECE Recommendation 20 three-letter trade code (e.g. "KGM", "MTR", "EA").
        [StringLength(8)]
        public string? UneceCode { get; set; }

        // UCUM code for FDA + HL7 + EU healthcare submissions.
        [StringLength(32)]
        public string? UcumCode { get; set; }

        public bool IsSystem { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
