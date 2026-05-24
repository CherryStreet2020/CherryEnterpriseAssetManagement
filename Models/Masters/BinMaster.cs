using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-7 — BinMaster (physical leaf inventory location).
    //
    // The leaf of the WMS hierarchy:
    //   Site → WarehouseMaster → BinMaster
    //
    // Replaces the flat `Bin` string column scattered across receiving + put-
    // away + inventory tables. A real Bin carries:
    //   - Zone / Aisle / Bay / Level / Position — multi-axis address
    //   - BinType — describes what fits here (pallet vs case vs each vs bulk)
    //   - MixingRule — single-SKU / single-Lot / mixed-OK (drives put-away)
    //   - Capacity — weight + volume limits (drives WMS slotting)
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NULL = system template row
    // (rare for bins — only used to seed example layouts during tenant
    // onboarding), set = tenant-owned bin.
    //
    // AUTHORITY:
    //   - docs/adr/ADR-019-wms-posting-profile-pattern.md
    //   - docs/research/master-files-baseline-2026-05-24.md §6.4
    //   - memory: reference_bic_entity_checklist.md
    // =============================================================================
    [Table("BinMasters")]
    public class BinMaster
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        // Parent warehouse. REQUIRED — every bin lives in a warehouse.
        public int WarehouseId { get; set; }
        public WarehouseMaster? Warehouse { get; set; }

        // Stable bin code (e.g. "RECV-01", "STAGE-A1", "PALLET-001-A-01",
        // "A-12-3-B-04"). Code is UNIQUE within (CompanyId, WarehouseId).
        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        // Multi-axis address. All optional — system templates use NULL,
        // tenant bins typically populate the dimensions that matter.
        [StringLength(32)] public string? Zone { get; set; }
        [StringLength(32)] public string? Aisle { get; set; }
        [StringLength(32)] public string? Bay { get; set; }
        [StringLength(32)] public string? Level { get; set; }
        [StringLength(32)] public string? Position { get; set; }

        public BinType BinType { get; set; } = BinType.Pallet;

        public BinMixingRule MixingRule { get; set; } = BinMixingRule.Mixed;

        // Optional capacity ceilings. Used by WMS put-away suggestions; NOT
        // enforced as a hard constraint on receipts (those flow through
        // service layer that decides whether to suggest a different bin).
        [Column(TypeName = "numeric(14,4)")]
        public decimal? MaxWeightKg { get; set; }

        [Column(TypeName = "numeric(14,6)")]
        public decimal? MaxVolumeM3 { get; set; }

        // True for receiving/staging/shipping bins — they exist for flow,
        // not for long-term storage. Affects WMS heatmap + slotting reports.
        public bool IsTransient { get; set; } = false;

        // True for pick-face bins that get replenished from bulk storage.
        public bool IsPickFace { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // BinType — describes what fits + how the unit-of-storage behaves.
    //
    // Modeled after Manhattan SCALE + Blue Yonder WMS bin categorization.
    // =============================================================================
    public enum BinType
    {
        // Holds full pallets (typical bulk reserve storage).
        Pallet = 0,

        // Holds individual cases (case-pick area, replenishes Each bins).
        Case = 1,

        // Holds individual eaches/units (pick-face for piece-pick).
        Each = 2,

        // Bulk floor storage with no rack — typical for liquids, raw
        // material piles, slow-moving SKUs.
        Bulk = 3,

        // Inbound receiving lane — transient.
        Receiving = 4,

        // Staging between receipt and put-away, or between pick and pack.
        Staging = 5,

        // Outbound shipping lane — transient.
        Shipping = 6,

        // Quarantine / hold bin within a non-quarantine warehouse.
        Quarantine = 7,

        // Returns processing bin.
        Returns = 8,

        // Scrap / damaged bin.
        Scrap = 9,

        Other = 99
    }

    // =============================================================================
    // BinMixingRule — drives WMS put-away decisions.
    // =============================================================================
    public enum BinMixingRule
    {
        // Bin can hold any combination of SKUs + lots.
        Mixed = 0,

        // Bin holds one SKU at a time (different lots OK). Typical pick-face.
        SingleSku = 1,

        // Bin holds one lot at a time (drives food/pharma traceability).
        SingleLot = 2,

        // Bin holds one serial-tracked unit at a time (high-value goods,
        // medical devices).
        SingleSerial = 3,

        // Bin holds one SKU + one lot — most restrictive (regulated food).
        SingleSkuSingleLot = 4
    }
}
