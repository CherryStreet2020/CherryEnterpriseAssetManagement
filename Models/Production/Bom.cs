using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.14 — Bom subtype of MaterialStructure.
    //
    // Discrete-manufacturing Bill of Materials. 1:0..1 with
    // MaterialStructure via UNIQUE on MaterialStructureId, ON DELETE
    // CASCADE.
    //
    // The BOM-specific data lives here (assembly type, phantom-flag,
    // total weight). Component lines live on MaterialStructureLine
    // shared with Recipe.
    //
    // Reference: ADR-013 §"Recommendation" item 3 + classic discrete-
    // manufacturing BOM pattern (SAP MM-BOM, Oracle Bill of Materials,
    // Plex BOM, Fulcrum BOM).
    [Table("Boms")]
    public class Bom
    {
        public int Id { get; set; }

        public int MaterialStructureId { get; set; }
        public MaterialStructure? MaterialStructure { get; set; }

        public BomType BomType { get; set; } = BomType.Engineering;

        // Phantom BOM — components flow through to the parent BOM but
        // this BOM has no on-hand inventory. Common for sub-assemblies
        // that are never built and stocked (only built as part of the
        // larger assembly).
        public bool IsPhantom { get; set; } = false;

        // Total weight in kg — sum of component-line weights * quantities.
        // Stored rather than computed for fast lookup; recomputed by
        // service layer when lines change.
        [Display(Name = "Total Weight (kg)")]
        [Column(TypeName = "decimal(12,4)")]
        public decimal? TotalWeightKg { get; set; }

        // Lead time in days — longest path through component lines.
        // Stored for fast scheduling lookup; recomputed when lines
        // change.
        public int? LeadTimeDays { get; set; }

        // Production yield % — historical good-output percentage.
        // 0-100. Used by the planner to inflate component requirements
        // (1.0 / yield) to cover expected scrap.
        [Column(TypeName = "decimal(5,2)")]
        public decimal? YieldPercent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    // BOM lifecycle classification — drives which engineering /
    // manufacturing / sales context this BOM is used in. SAP MM-BOM
    // pattern.
    public enum BomType
    {
        Engineering = 0,   // EBOM — as-designed by engineering
        Manufacturing = 1, // MBOM — as-built on the shop floor
        Sales = 2,         // SBOM — as-sold (configurable + options)
        Service = 3,       // SerBOM — for spare parts / service
        Phantom = 4,       // see IsPhantom flag — but kept here for type
    }
}
