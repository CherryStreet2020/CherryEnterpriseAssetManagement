using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-11 — ItemPackHierarchy (per-Item pack configuration).
    //
    // Master Files Baseline cascade ship #9 of 10.
    //
    // PURPOSE: tie a specific Item to specific pack-level configurations,
    // carrying the physical reality at each level — qty of base units, outer
    // dimensions, weights, barcodes. The data WMS uses for put-away/picking,
    // shipping uses for label generation, MRP uses for purchase rounding.
    //
    // CARDINALITY: one Item can have multiple rows at the same PackLevel
    // (e.g. an item shipped in 12-pack cases AND 24-pack cases — both rows
    // exist; one is marked IsDefault). Service layer picks IsDefault at
    // implicit-config time; explicit per-order config wins when set.
    //
    // CROSS-TENANT — operational data, CompanyId NOT NULL.
    //
    // UNIQUE: (CompanyId, ItemId, PackLevelId, Gtin) — same item × same level
    // × same barcode uniquely identifies a configuration. Allowing the same
    // item × level pair to recur (without Gtin) handles tenants that don't
    // use GS1 barcoding consistently.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.9
    //   - memory: reference_master_files_baseline.md
    //   - GS1 General Specifications (GTIN-8 / GTIN-12 / GTIN-13 / GTIN-14)
    //   - ISTA / ASTM packaging dimensional standards
    // =============================================================================
    [Table("ItemPackHierarchies")]
    public class ItemPackHierarchy
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public int ItemId { get; set; }

        public int PackLevelId { get; set; }
        public PackLevel? PackLevel { get; set; }

        // ---------------------------------------------------------------------
        // QUANTITY.
        // ---------------------------------------------------------------------

        // How many BASE units fit in this pack (e.g. 24 EA per CASE).
        [Column(TypeName = "numeric(18,4)")]
        public decimal QtyOfBaseUnits { get; set; }

        // FK to UnitOfMeasureMaster (PRA-4) — the BASE UOM the qty is measured
        // in. NULL = inherit Item's primary UOM.
        public int? BaseUomId { get; set; }

        // ---------------------------------------------------------------------
        // PHYSICAL DIMENSIONS (outer pack dimensions in cm — WMS slotting input).
        // ---------------------------------------------------------------------
        [Column(TypeName = "numeric(10,2)")]
        public decimal? LengthCm { get; set; }

        [Column(TypeName = "numeric(10,2)")]
        public decimal? WidthCm { get; set; }

        [Column(TypeName = "numeric(10,2)")]
        public decimal? HeightCm { get; set; }

        // Empty/tare weight (kg) — the packaging without product. For
        // shipping cost computation: gross - tare = net product.
        [Column(TypeName = "numeric(10,4)")]
        public decimal? TareWeightKg { get; set; }

        // Gross weight (kg) — packaging + product fully filled.
        [Column(TypeName = "numeric(10,4)")]
        public decimal? GrossWeightKg { get; set; }

        // ---------------------------------------------------------------------
        // BARCODES (GS1 standards).
        // ---------------------------------------------------------------------

        // GTIN — 8/12/13/14 digit. The universal product identifier at THIS
        // pack level (GTIN-13 typical for EACH retail; GTIN-14 typical for CASE).
        [StringLength(14)]
        public string? Gtin { get; set; }

        // UPC — US/Canada retail (12 digit). Subset of GTIN-12.
        [StringLength(12)]
        public string? Upc { get; set; }

        // EAN — Europe retail (13 digit). Subset of GTIN-13.
        [StringLength(13)]
        public string? Ean { get; set; }

        // SSCC — Serial Shipping Container Code. Pallet-level barcode tying
        // a specific physical pallet to an Advance Shipping Notice line.
        [StringLength(18)]
        public string? Sscc { get; set; }

        // ---------------------------------------------------------------------
        // FLAGS.
        // ---------------------------------------------------------------------

        // True = primary config at this level (default when no explicit pack
        // is specified on a transaction). Exactly one row per (Item, PackLevel)
        // should have IsDefault = true; service layer enforces.
        public bool IsDefault { get; set; } = false;

        // True = this pack can be shipped standalone (case-of-1 may not be).
        public bool IsShippable { get; set; } = true;

        // True = pack is fully recyclable (drives sustainability reporting +
        // EU Extended Producer Responsibility / EPR fee computation).
        public bool IsRecyclable { get; set; } = true;

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
