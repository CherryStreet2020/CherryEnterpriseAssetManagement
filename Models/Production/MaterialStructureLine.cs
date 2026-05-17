using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.14 — MaterialStructureLine (shared lines table).
    //
    // ONE LINES TABLE shared by Bom and Recipe subtypes of
    // MaterialStructure. LineKind discriminator says whether this row
    // is a Component / CoProduct / ByProduct / Scrap / Packaging /
    // Intermediate.
    //
    // Why shared not split:
    //   - BOM lines and Recipe lines are 70% the same fields
    //     (item, quantity, scrap %)
    //   - Single join from MaterialStructure -> Lines simplifies
    //     "what does this structure produce / consume?" queries
    //   - TypeSpecificProperties jsonb absorbs the 30% that differs
    //     (BOM: reference designator + alternate group; Recipe: phase
    //     sequence + scaling override + co-product yield factor)
    //
    // Reference: ADR-013 §"Recommendation" item 3 — "shared
    // MaterialStructureLine with a LineKind enum (component, co-product,
    // by-product, scrap, packaging)."
    [Table("MaterialStructureLines")]
    public class MaterialStructureLine
    {
        public int Id { get; set; }

        public int MaterialStructureId { get; set; }
        public MaterialStructure? MaterialStructure { get; set; }

        // What item is this line? Required for all kinds.
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        // What kind of line is this? Drives whether it's a consumed
        // input (Component), produced co-output (CoProduct), unwanted
        // production output (ByProduct), expected loss (Scrap),
        // packaging material, or intermediate (bulk -> pack flow).
        public LineKind LineKind { get; set; } = LineKind.Component;

        // Position in the BOM / Recipe structure. Drives display
        // ordering and "phase X" grouping for recipes.
        public int Sequence { get; set; } = 10;

        // Quantity per standard batch (for Recipe) or per assembly
        // (for BOM). Always positive — production direction (consumed
        // vs produced) is captured by LineKind.
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [StringLength(16)]
        public string? Uom { get; set; }

        // Expected scrap %. Drives planner inflation of component
        // requirements (consumed quantity = Quantity * (1 + Scrap/100)).
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ScrapPercent { get; set; }

        // For Recipe lines: which phase does this line apply to?
        // FK-like reference to RecipePhase.Sequence (not the row Id —
        // phases are identified by their sequence within a recipe).
        // Null means "applies to all phases."
        public int? PhaseSequence { get; set; }

        // Type-specific properties as jsonb. Mirrors the pattern from
        // PR #119.13a's HSE / Process satellites.
        //
        // BOM-line examples:
        //   { "referenceDesignator": "R47, R48", "alternateGroup": "ALT-1" }
        //
        // Recipe-line examples:
        //   { "scalingOverride": "fixed", "coProductYieldFactor": 0.15,
        //     "addAtTempC": 1450, "addAfterSeconds": 7200 }
        //
        // Free-form so verticals can add their own fields without a
        // migration.
        [Column(TypeName = "jsonb")]
        public string? TypeSpecificProperties { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // What kind of line is this within a MaterialStructure?
    public enum LineKind
    {
        Component = 0,      // raw material / sub-assembly consumed
        CoProduct = 1,      // intentional co-output (e.g., recipe yields
                            //   both A and B in known ratio)
        ByProduct = 2,      // unintentional but valuable output (e.g.,
                            //   waste heat used elsewhere, secondary)
        Scrap = 3,          // expected loss (book to scrap GL)
        Packaging = 4,      // packaging material consumed
        Intermediate = 5,   // bulk intermediate item (hybrid bulk -> pack)
    }
}
