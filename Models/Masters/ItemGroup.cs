using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-7 — ItemGroup (item classification → GL posting matrix).
    //
    // Closes the "MaterialGroup / ProductGroup → Posting Profile missing"
    // gap from docs/research/master-files-baseline-2026-05-24.md §6.5.
    //
    // ItemGroup is the COARSE classification of an item that drives GL
    // posting decisions:
    //   - Raw material → debits RawMaterialInventory on receipt
    //   - Finished goods → debits FinishedGoodsInventory on production complete
    //   - Consumable → debits Consumables on receipt + immediate expense on issue
    //   - Service → no inventory side, only revenue/expense
    //   - Asset → debits FixedAsset / triggers AssetCapitalizationService
    //   - Subassembly → debits SubAssemblyInventory + tracks BOM-level rollup
    //   - Subcontract → debits SubcontractInventory + tracks contract pricing
    //
    // The PRA-5a COA expansion put the GlAccountCategory enum values in
    // place (RawMaterialInventory=111, WipInventoryProduction=112, etc.).
    // ItemGroup now WIRES them via Default*GlAccountId FKs, and the
    // PostingProfile sibling table (PRA-7) lets tenants override per
    // transaction type + warehouse.
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NULL = system template row
    // (the 6 RAW/WIP/FG/CONSUMABLE/SERVICE/ASSET shells), set = tenant
    // extension (rare — tenant adds a custom group like "TOOLING-INTERNAL").
    //
    // AUTHORITY:
    //   - docs/adr/ADR-019-wms-posting-profile-pattern.md
    //   - docs/research/master-files-baseline-2026-05-24.md §6.5
    //   - memory: reference_master_files_baseline.md
    // =============================================================================
    [Table("ItemGroups")]
    public class ItemGroup
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        // Stable code (e.g. "RAW", "WIP", "FG", "CONSUMABLE", "SERVICE",
        // "ASSET", "SUBASSY", "SUBCONTRACT"). UPPERCASE, underscore-OK.
        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public ItemGroupType GroupType { get; set; } = ItemGroupType.RawMaterial;

        // ---------------------------------------------------------------------
        // DEFAULT GL ACCOUNTS — per-group fallbacks. PostingProfile (sibling
        // table below) overrides these per (ItemGroup × TransactionType ×
        // Warehouse).
        //
        // All nullable so system templates can ship without wiring; tenant
        // onboarding service populates them.
        // ---------------------------------------------------------------------
        public int? DefaultInventoryGlAccountId { get; set; }
        public int? DefaultCogsGlAccountId { get; set; }
        public int? DefaultRevenueGlAccountId { get; set; }
        public int? DefaultScrapGlAccountId { get; set; }
        public int? DefaultVarianceGlAccountId { get; set; }

        // For Asset-group items — bridge into the fixed-asset capitalization
        // workflow.
        public int? DefaultFixedAssetGlAccountId { get; set; }

        // ---------------------------------------------------------------------
        // BEHAVIORAL FLAGS.
        // ---------------------------------------------------------------------

        // True for items that get expensed immediately on issue (consumables,
        // services) — no on-hand inventory tracked.
        public bool ExpenseOnIssue { get; set; } = false;

        // True for items that capitalize into the fixed-asset ledger instead
        // of inventory.
        public bool CapitalizesAsAsset { get; set; } = false;

        // True when items in this group require serial tracking by default.
        public bool RequiresSerialTracking { get; set; } = false;

        // True when items in this group require lot tracking by default.
        public bool RequiresLotTracking { get; set; } = false;

        // True for items subject to FAI (First Article Inspection) workflow —
        // aerospace + medical defaults.
        public bool RequiresFai { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // ItemGroupType — the categorical bucket. Drives default flags + posting.
    // =============================================================================
    public enum ItemGroupType
    {
        // Inputs to manufacturing — bar stock, sheet, electronics, chemicals.
        RawMaterial = 0,

        // Items in-process between work centers (transient).
        WorkInProcess = 1,

        // Outputs of manufacturing — sellable units.
        FinishedGoods = 2,

        // Items that get expensed on issue (cutting fluid, gloves, lubricant).
        Consumable = 3,

        // Services — labor, freight, third-party — no inventory side.
        Service = 4,

        // Items that capitalize into the fixed-asset ledger.
        Asset = 5,

        // Sub-assemblies — internally produced inputs to higher-level FG.
        Subassembly = 6,

        // Items produced by external subcontractor (we own the IP + materials).
        Subcontract = 7,

        // Tooling / dies / fixtures — capitalized or expensed depending on
        // tenant policy.
        Tooling = 8,

        // Spare parts (for maintenance, not for resale).
        SparePart = 9,

        // Packaging materials — cartons, pallets, dunnage.
        Packaging = 10,

        Other = 99
    }
}
