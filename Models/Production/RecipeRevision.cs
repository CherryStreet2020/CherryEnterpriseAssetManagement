using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — RecipeRevision stub.
    //
    // Shipped as a stub FK target so ProductionBatch.RecipeRevisionId
    // is a valid foreign key from day one. The #1 documented v1-to-v2
    // migration regret across NADCAP / AS9100 manufacturing systems
    // surveyed is "we stored Recipe as varchar, now we need to convert
    // to FK after 6 months of production data" — brutal.
    //
    // What this stub holds:
    //   - Id, Name, Version, ParentRecipeId (revision-chain self-FK),
    //     Status, IsControlled, audit
    //
    // What lands later (PR #119.14 - Phase E.3):
    //   - Full Recipe content schema: RecipeStep, RecipeIngredient,
    //     RecipePhase, RegulatoryProfile gating
    //   - The polymorphic MaterialStructure -> Recipe subtype linkage
    //
    // For NADCAP AC7102 / AC7108 audit defensibility we need:
    //   - A controlled identifier traceable to part number
    //   - Revision history (master + revision chain)
    //   - Approval status
    // All of which are in the stub already.
    //
    // Reference: PR #119.13a research report Q10 (#2 regret pattern) +
    // ADR-013 §"Phase E ship plan" item 3.
    [Table("RecipeRevisions")]
    public class RecipeRevision
    {
        public int Id { get; set; }

        [Required]
        [StringLength(64)]
        [Display(Name = "Recipe Name")]
        public string Name { get; set; } = string.Empty;

        // Revision identifier. Free-text 16 chars so "A", "Rev-01",
        // "2026-05-17-A" all work.
        [Required]
        [StringLength(16)]
        public string Version { get; set; } = "A";

        // Revision-chain self-FK. SET NULL on master delete so
        // revisions don't cascade-orphan.
        public int? MasterRecipeId { get; set; }
        public RecipeRevision? MasterRecipe { get; set; }

        public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

        // True once the recipe has been formally approved per the
        // shop's controlled-document procedure. NADCAP requires this
        // gate for special processes.
        public bool IsControlled { get; set; }

        // Optional pointer to the external controlled-document store
        // (typically a DMS / Box / SharePoint URL or document ID).
        [StringLength(500)]
        public string? ControlledDocumentUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [StringLength(100)]
        public string? ApprovedBy { get; set; }
    }

    // Lifecycle of a recipe revision.
    public enum RecipeStatus
    {
        Draft = 0,
        InReview = 1,
        Approved = 2,
        Superseded = 3,
        Retired = 4,
    }
}
