using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-4 — UomCategory master.
    //
    // The dimensional bucket every UnitOfMeasure rolls up under (Length, Mass,
    // Volume, Time, Count, Temperature, Pressure, etc.). In-category conversions
    // are arithmetic (factor + offset to the category's base unit, stored on
    // UnitOfMeasure). Cross-category conversions require explicit UomConversion
    // rows.
    //
    // CROSS-TENANT REFERENCE PATTERN (mirrors ReasonCode / MaterialMaster):
    //   - CompanyId NULL  = system reference category (every tenant sees)
    //   - CompanyId set   = tenant-specific extension
    //
    // Two partial UNIQUEs enforce code-scoped uniqueness (NO COALESCE-in-index,
    // per the PR #5c.1.1 Replit prod-validator gotcha):
    //   IX_UomCategories_System_Code   WHERE CompanyId IS NULL
    //   IX_UomCategories_Company_Code  WHERE CompanyId IS NOT NULL
    //
    // BaseUomId is nullable because we have a chicken-and-egg at seed time
    // (categories exist before their base UOM rows are inserted). The seeder
    // populates BaseUomId in a second pass after UnitsOfMeasure are inserted.
    //
    // Audit + AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §5.3
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - memory: feedback_no_shortcuts_multi_tenant_lineage.md
    // =============================================================================
    [Table("UomCategories")]
    public class UomCategory
    {
        public int Id { get; set; }

        // NULL = system reference category (cross-tenant shared).
        // INT > 0 = tenant-specific extension.
        public int? CompanyId { get; set; }

        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // The canonical base unit of this category (e.g. METER for Length,
        // GRAM for Mass). All in-category conversions normalize through this.
        // Nullable to break the seed-time chicken-and-egg (see header comment).
        public int? BaseUomId { get; set; }

        public bool IsSystem { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
