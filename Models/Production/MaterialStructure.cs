using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.14 — MaterialStructure polymorphic parent.
    //
    // The "what does this thing consume and produce" backbone. Two
    // concrete subtypes:
    //   - Bom: discrete assembly Bill of Materials
    //   - Recipe: process / formula with phases, co-products, by-products
    //
    // Why polymorphic vs separate tables:
    //   - A BOM line and a Recipe line are 70% the same fields
    //     (component item, quantity, scrap %)
    //   - The 30% that's different (Recipe has phase ordinal + scaling;
    //     BOM has reference designator + alternate group) is captured
    //     on the subtype tables and in MaterialStructureLine's jsonb
    //     TypeSpecificProperties column
    //   - One uniform query for "what does this MaterialStructure produce?"
    //
    // Same architectural pattern as ADR-013's ProductionBatch + Nest +
    // ProcessBatch from PR #119.13a. Third polymorphic primitive in
    // the production schema.
    //
    // Reference: ADR-013 §"Recommendation" item 3.
    [Table("MaterialStructures")]
    public class MaterialStructure
    {
        public int Id { get; set; }

        // Human-facing identifier — "MS-2026-00042" or shop convention.
        [Required]
        [StringLength(64)]
        [Display(Name = "Structure #")]
        public string StructureNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        // Discriminator — which subtype table holds the type-specific
        // fields. Bom or Recipe at first ship; future Formula or
        // BatchTicket slot in without schema change.
        public StructureType StructureType { get; set; } = StructureType.Bom;

        // Lifecycle. Same shape as RecipeStatus from #119.13a so the
        // approval / control workflow is uniform.
        public MaterialStructureStatus Status { get; set; } = MaterialStructureStatus.Draft;

        // Revision identifier. Free-text 16 chars so "A", "Rev-01",
        // "2026-05-17-A" all work.
        [StringLength(16)]
        public string? Revision { get; set; } = "A";

        // What item does this structure produce? Pointing at the
        // primary output. For BOMs this is the assembly; for Recipes
        // this is the principal product. Co-products + by-products
        // come through MaterialStructureLines.
        public int? OutputItemId { get; set; }
        public Item? OutputItem { get; set; }

        // Revision-chain self-FK. Mirrors RecipeRevision + WorkOrder
        // master/revision pattern.
        public int? MasterStructureId { get; set; }
        public MaterialStructure? MasterStructure { get; set; }

        // Regulatory profile binding — set when this structure must
        // conform to a specific regime (FDA, AS9100, NADCAP). NULL
        // means standard discrete / process manufacturing.
        public int? RegulatoryProfileId { get; set; }
        public RegulatoryProfile? RegulatoryProfile { get; set; }

        // Approval gates. NADCAP / AS9100 / FDA 21 CFR 820 + 211
        // require that controlled documents have a documented
        // approval trail per revision.
        public bool IsControlled { get; set; }

        [StringLength(500)]
        public string? ControlledDocumentUrl { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Optimistic concurrency via PG xmin.
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // ----- Navs -----

        // 1:0..1 subtypes via UNIQUE on MaterialStructureId in migration.
        public Bom? Bom { get; set; }
        public Recipe? Recipe { get; set; }

        // 1:N — shared lines table.
        public ICollection<MaterialStructureLine>? Lines { get; set; }
    }

    // Discriminator.
    public enum StructureType
    {
        Bom = 0,
        Recipe = 1,
    }

    // Lifecycle. Mirrors RecipeStatus from PR #119.13a stub.
    public enum MaterialStructureStatus
    {
        Draft = 0,
        InReview = 1,
        Approved = 2,
        Superseded = 3,
        Retired = 4,
    }
}
