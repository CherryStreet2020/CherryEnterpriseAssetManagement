using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-11 — PackLevel (named pack tiers for the hierarchy).
    //
    // Master Files Baseline cascade ship #9 of 10. Closes the "Pack hierarchy
    // (Each/Inner/Case/Pallet) absent" gap from
    // docs/research/master-files-baseline-2026-05-24.md §6.9.
    //
    // POSITION IN THE STACK:
    //   PackLevel (this — named pack tiers like EACH/INNER/CASE/PALLET/TRUCK)
    //     └── ItemPackHierarchy (per-Item config — qty per pack, dimensions,
    //                            weights, barcodes per Item × Level)
    //
    // WHY SEPARATE FROM UnitOfMeasure (PRA-4):
    //   - UOM is a UNIT-of-measurement (kg, m, EA, L). Conversion math.
    //   - PackLevel is a NAMED TIER (Each/Inner/Case/Pallet/Truck) that an item
    //     can be packed at, regardless of underlying UOM. A "Case" of cleaning
    //     fluid might be 12 EA × 1L bottles = 12L; the Case is a PACK TIER,
    //     the L/EA is the UOM. ItemPackHierarchy carries both.
    //   - Same pack TIER can have different physical configurations per Item
    //     (Item A "Case" = 24 EA; Item B "Case" = 6 EA). ItemPackHierarchy
    //     captures the per-Item physical reality at each tier.
    //
    // CROSS-TENANT REFERENCE pattern:
    //   CompanyId NULL = system template (5 standard tiers seeded)
    //   CompanyId set  = tenant-specific tier (e.g., "Mini-Pack" between
    //                    Inner and Case for some retailers).
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.9
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - GS1 packaging hierarchy (Each / Inner Pack / Case / Pallet / Container)
    //   - SAP MM packaging master + Oracle Cloud Item Packaging
    // =============================================================================
    [Table("PackLevels")]
    public class PackLevel
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        // Stable code (e.g. "EACH", "INNER", "CASE", "PALLET", "TRUCK").
        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Order in the hierarchy. 1 = lowest (typically EACH), 5+ = highest
        // (TRUCK/CONTAINER). Lets a query sort tiers in physical-size order
        // without hard-coding the codes.
        public int LevelOrder { get; set; } = 1;

        // Optional default UOM at this tier (e.g. EACH → EA, CASE → CS,
        // PALLET → PLT). FK to UnitOfMeasureMaster (PRA-4). NULL = tier
        // doesn't have a canonical UOM (tier-only abstraction).
        public int? DefaultUomId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
