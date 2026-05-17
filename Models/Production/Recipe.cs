using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.14 — Recipe subtype of MaterialStructure.
    //
    // Process-manufacturing recipe. 1:0..1 with MaterialStructure via
    // UNIQUE on MaterialStructureId, ON DELETE CASCADE.
    //
    // The recipe-specific data lives here (scaling rules, batch yield,
    // intermediate item for hybrid bulk-then-pack). Phase definitions
    // live on a child RecipePhase table. Ingredients / co-products /
    // by-products live on MaterialStructureLine shared with Bom.
    //
    // This entity FINALLY wires the RecipeRevision stub that's been
    // sitting in the schema since PR #119.13a (referenced by
    // ProductionBatch.RecipeRevisionId). Now Recipe carries the
    // content; RecipeRevision keeps the identity / approval status.
    //
    // Reference: ADR-013 §"Recommendation" item 3 + Datacor /
    // BatchMaster / SAP PP-PI Master Recipe pattern.
    [Table("Recipes")]
    public class Recipe
    {
        public int Id { get; set; }

        public int MaterialStructureId { get; set; }
        public MaterialStructure? MaterialStructure { get; set; }

        // Wires the RecipeRevision stub from PR #119.13a into real
        // content. ProductionBatch.RecipeRevisionId points at the
        // revision; Recipe carries the actual ingredients + phases.
        public int? RecipeRevisionId { get; set; }
        public RecipeRevision? RecipeRevision { get; set; }

        // How does this recipe scale when produced at non-standard
        // batch sizes?
        public ScalingMode ScalingMode { get; set; } = ScalingMode.Linear;

        // Standard batch size — the "1.0x" denominator. When producing
        // at a different size, all linearly-scaled ingredients multiply
        // by (target_size / StandardBatchSize).
        [Display(Name = "Standard Batch Size")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? StandardBatchSize { get; set; }

        [StringLength(16)]
        public string? BatchUom { get; set; }

        // Expected yield % at standard batch size. Drives planner
        // ingredient inflation. 0-100.
        [Column(TypeName = "decimal(5,2)")]
        public decimal? YieldPercent { get; set; }

        // Hybrid bulk-then-pack pattern: this Recipe produces a bulk
        // intermediate item, which a subsequent Bom packages into the
        // finished SKU. Set IntermediateItemId when applicable.
        // Datacor / BatchMaster two-link-structure convention.
        public int? IntermediateItemId { get; set; }
        public Item? IntermediateItem { get; set; }

        // Total recipe duration estimate in minutes — sum of phase
        // durations. Stored for fast scheduling; recomputed by service
        // layer when phases change.
        public int? TotalDurationMinutes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Phases — child 1:N collection.
        public ICollection<RecipePhase>? Phases { get; set; }
    }

    // Recipe scaling behavior. When a Recipe is run at non-standard
    // batch size, how do ingredients adjust?
    public enum ScalingMode
    {
        Linear = 0,        // all ingredients * (target / standard)
        Fixed = 1,         // ingredients stay constant regardless of size
                           // (e.g., catalysts, indicators)
        PerBatch = 2,      // each phase has its own scaling rule, set on
                           // MaterialStructureLine.TypeSpecificProperties
    }
}
