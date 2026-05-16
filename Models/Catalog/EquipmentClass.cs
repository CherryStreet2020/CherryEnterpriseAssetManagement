using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models.Catalog
{
    // Sprint 2 PR #117.2 — Equipment Catalog (per Dean: "Best in Class Process
    // to Produce a Best In Class product").
    //
    // EquipmentClass groups assets by what they ARE — a CNC machining center,
    // a welding robot, a stamping press. Each class owns its own sensor profile
    // and its own list of real-world manufacturer + model lines. This is what
    // makes the demo defensible: a sophisticated buyer sees real Mfr/Model
    // pairings and class-appropriate sensors (presses don't have spindle temp,
    // CNCs don't have hydraulic ram pressure), not the universal Temp/Vib/PSI
    // shortcut from PR #117.
    //
    // Source of truth: EQUIPMENT_CATALOG.md (curated, reviewable). The
    // EquipmentCatalogSeeder ingests the catalog into these tables on
    // startup; the IndustrialAssetSeeder reads from the tables, not from
    // C# arrays.
    public class EquipmentClass
    {
        public int Id { get; set; }

        // Stable machine-friendly identifier — used by seeders and code
        // (e.g. "CNC_MACHINING_CENTER", "WELDING_ROBOT", "STAMPING_PRESS").
        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        // Human-readable display name (e.g. "CNC Machining Center").
        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Coarse grouping for the Plant Floor filter chips and admin pages
        // (e.g. "Machining", "Welding", "Material Handling", "Stamping",
        // "Facility", "Measurement").
        [Required, StringLength(60)]
        public string Category { get; set; } = string.Empty;

        // For the Plant Floor card icon. Keep small — emoji or a short
        // CSS class name. Optional; UI falls back to a generic icon.
        [StringLength(40)]
        public string? IconCode { get; set; }

        // Sort key for displaying classes in admin lists / dropdowns.
        public int DisplayOrder { get; set; } = 100;

        // Whether new assets can be created in this class. Lets us
        // retire a class without losing historical assets that reference it.
        public bool Active { get; set; } = true;

        public System.DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
        public System.DateTime UpdatedAt { get; set; } = System.DateTime.UtcNow;

        // Navigation
        public ICollection<EquipmentModel> Models { get; set; } = new List<EquipmentModel>();
        public ICollection<SensorProfile> SensorProfiles { get; set; } = new List<SensorProfile>();
    }
}
