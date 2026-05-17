using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.14 — RecipePhase.
    //
    // A single phase / step in a Recipe. Phases run in Sequence order
    // and define time / temperature / atmosphere / agitation for the
    // span of execution.
    //
    // Cardinality: many phases per Recipe. UNIQUE on
    // (RecipeId, Sequence) so the planner can ORDER BY Sequence
    // safely. ON DELETE CASCADE from Recipe.
    //
    // Why a separate table (not jsonb on Recipe):
    //   - Phases are queried independently for scheduling
    //   - Each phase may emit a ProcessBatch (from #119.13a) when
    //     executed against equipment — needs an FK target
    //   - Aerospace recipes (NADCAP AC7102) require per-phase
    //     pyrometry charts, which need their own audit trail
    //
    // Reference: ADR-013 §"Recommendation" item 3 + SAP PP-PI Master
    // Recipe "phase" pattern.
    [Table("RecipePhases")]
    public class RecipePhase
    {
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        // Order of this phase within the recipe. UNIQUE within RecipeId
        // so the planner can sort deterministically.
        public int Sequence { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Phase duration in minutes. Drives schedule and (later)
        // ProcessBatch SoakTimeMinutes.
        public int? DurationMinutes { get; set; }

        // Temperature setpoint and tolerance — drives ProcessBatch
        // SetpointTempC when this phase fires.
        [Column(TypeName = "decimal(8,2)")]
        public decimal? SetpointTempC { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal? TempToleranceC { get; set; }

        [StringLength(32)]
        public string? AtmosphereType { get; set; }   // "Air", "Nitrogen", "Argon", etc.

        [StringLength(32)]
        public string? AgitationSpec { get; set; }    // "RPM 100", "low/med/high"

        // Pressure setpoint in PSI — for autoclave / pressure cook /
        // composite cure phases. Nullable.
        [Column(TypeName = "decimal(8,2)")]
        public decimal? PressurePsi { get; set; }

        // What's the equipment class needed for this phase? Free-text
        // 64 chars so per-shop taxonomy survives ("vacuum furnace",
        // "tray oven", "autoclave"). The actual equipment selection
        // happens at ProductionBatch creation time.
        [StringLength(64)]
        public string? RequiredEquipmentClass { get; set; }

        // Whether this phase has a quality / pyrometry hold gate that
        // operators must clear before continuing. NADCAP AC7102.
        public bool HasQualityHold { get; set; } = false;

        [StringLength(2000)]
        public string? OperatorInstructions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
