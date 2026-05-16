using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Catalog
{
    // Sprint 2 PR #117.2 — A real manufacturer + model line within an
    // EquipmentClass. Pulled from the curated EQUIPMENT_CATALOG.md and
    // seeded into this table by EquipmentCatalogSeeder.
    //
    // Examples: "Haas Automation, Inc." × "VF-2SS" (class = CNC_MACHINING_CENTER),
    // "Lincoln Electric" × "Power Wave S350" (class = WELDING_POWER_SOURCE),
    // "Schuler" × "MSP 400" (class = STAMPING_PRESS).
    //
    // ImageUrl / MaintenanceManualUrl accept either a local path
    // ("/assets/equipment/cnc/haas-vf2ss.jpg") or an absolute URL
    // ("https://www.haascnc.com/...). UI inspects the leading character
    // to choose the rendering.
    public class EquipmentModel
    {
        public int Id { get; set; }

        [Required]
        public int EquipmentClassId { get; set; }
        public EquipmentClass? EquipmentClass { get; set; }

        [Required, StringLength(120)]
        public string Manufacturer { get; set; } = string.Empty;

        [Required, StringLength(120)]
        public string ModelNumber { get; set; } = string.Empty;

        // Pre-formatted display string the seeder uses for Asset.Description /
        // Asset.AssetType. Avoids re-concatenating "Haas Automation, Inc. VF-2SS"
        // 300 times at seed time. Optional — falls back to "{Manufacturer} {ModelNumber}".
        [StringLength(180)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? ProductPageUrl { get; set; }

        // Local path (/assets/equipment/...) or absolute URL.
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // Local path (/assets/manuals/...) or absolute URL (OEM doc page).
        [StringLength(500)]
        public string? MaintenanceManualUrl { get; set; }

        // Typical North American street price in USD. Lets the seeder
        // pick a realistic Asset.AcquisitionCost / OriginalCost rather than
        // a synthetic one. Optional — seeder uses a class default if null.
        [Column(TypeName = "decimal(14,2)")]
        public decimal? TypicalAcquisitionCost { get; set; }

        // Typical service life in years. Drives realistic DepreciationLife.
        public int? ServiceLifeYears { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relative weight when the seeder picks a random model in this class.
        // Higher = more common in the demo plant. e.g. Haas VF-2SS is everywhere
        // (weight 5); Mazak VARIAXIS i-700 is rarer (weight 1).
        public int Weight { get; set; } = 1;

        public bool Active { get; set; } = true;

        public System.DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
        public System.DateTime UpdatedAt { get; set; } = System.DateTime.UtcNow;
    }
}
