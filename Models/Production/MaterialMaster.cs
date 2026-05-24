using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13b — MaterialMaster reference table.
    //
    // Small, high-value reference table for material identity. Lives
    // separately from Items because:
    //   - An Item is a stockable SKU ("1/4-inch A36 plate, 48x96 sheet")
    //   - A MaterialMaster is the substance class ("ASTM A36 hot-rolled
    //     steel plate")
    //   - Many Items share one MaterialMaster
    //   - Cross-shop analytics + ASME audit defense both need the
    //     structured AstmDesignation join key
    //
    // Why ship as two columns (free + structured):
    //   ShopCode is REQUIRED + UNIQUE — every shop coins its own short
    //   codes ("MS-1/4", "A36-0.250", "AL6061-T6-125") and onboarding
    //   would stall if they had to map every code to ASTM upfront.
    //   AstmDesignation is OPTIONAL — fill in when known; gives the
    //   analytics + audit join key without blocking onboarding.
    //
    // Reference: PR #119.13a research report Q3 (material taxonomy) +
    // ADR-013 §"Recommendation" item 4.
    [Table("MaterialMasters")]
    public class MaterialMaster
    {
        public int Id { get; set; }

        // Sprint 13.5 PR #5c.2 — Cross-tenant reference scoping.
        // NULL CompanyId = system reference data (shared across all tenants — the
        // canonical "ASTM A36 hot-rolled plate" row that every shop sees).
        // NOT NULL CompanyId = tenant-specific extension (a shop's custom shorthand
        // that doesn't apply company-wide).
        // Enforced via two partial UNIQUE indexes:
        //   IX_MaterialMasters_System_ShopCode  WHERE CompanyId IS NULL
        //   IX_MaterialMasters_Company_ShopCode WHERE CompanyId IS NOT NULL
        // (Replit gotcha lesson from PR #5c.1.1 — no COALESCE-in-index.)
        public int? CompanyId { get; set; }
        public int? LocationId { get; set; }

        // Shop-defined short code. Required, UNIQUE per scope (system or tenant).
        [Required]
        [StringLength(64)]
        [Display(Name = "Shop Code")]
        public string ShopCode { get; set; } = string.Empty;

        // Optional structured designation. ASTM is the de-facto metal
        // standard ("ASTM A36", "ASTM A572 Gr 50", "ASTM B221 6061-T6").
        // For non-metals, fill with the equivalent (ISO 1043 plastics,
        // ANSI/ASTM wood grades). Free text — every body has a different
        // schema and we don't want to force a specific one.
        [StringLength(64)]
        [Display(Name = "ASTM / Standard Designation")]
        public string? AstmDesignation { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        // Form factor — disambiguates "A36 plate" vs "A36 bar" which
        // share an ASTM designation but cost / nest differently.
        public MaterialForm Form { get; set; } = MaterialForm.Plate;

        // Density in kg/m^3 — used for per-weight cost allocation and
        // load-mass calculations on heat-treat batches. Nullable
        // because not every shop tracks this.
        [Display(Name = "Density (kg/m³)")]
        [Column(TypeName = "decimal(10,4)")]
        public decimal? DensityKgPerM3 { get; set; }

        // Whether grain direction matters for this material. Drives
        // the GrainDirection column on CutListLine — anisotropic
        // materials (wood, some composites, rolled steel for bend
        // ops) need per-part grain direction; isotropic don't.
        public bool IsAnisotropic { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }

    // Form factor. Drives cut-list / nest UI defaults and what
    // measurements are meaningful.
    public enum MaterialForm
    {
        Plate = 0,        // 2D flat metal stock
        Sheet = 1,        // thin metal stock, typically rolled
        Bar = 2,          // 1D linear stock (round/square/hex bar)
        Tube = 3,         // hollow 1D stock
        Pipe = 4,         // hollow 1D stock with NPS sizing
        Panel = 5,        // wood/composite sheet (MDF, plywood, etc.)
        Coil = 6,         // continuous rolled stock
        Wire = 7,         // small-diameter linear
        Casting = 8,
        Forging = 9,
        Other = 99,
    }
}
