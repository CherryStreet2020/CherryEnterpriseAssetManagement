using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-7 — PostingProfile (ItemGroup × TransactionType → GL).
    //
    // The matrix table that wires the COA expansion (PRA-5a) to the inventory
    // event stream. Each row says:
    //   "For this CompanyId + this ItemGroup + this TransactionType +
    //    (optional) Warehouse override, debit GL X and credit GL Y."
    //
    // RESOLUTION ORDER (highest priority wins, lookup goes most-specific →
    // least-specific):
    //   1. PostingProfile WHERE CompanyId = tenant AND ItemGroupId = X
    //                       AND TransactionType = T AND WarehouseId = W
    //   2. PostingProfile WHERE CompanyId = tenant AND ItemGroupId = X
    //                       AND TransactionType = T AND WarehouseId IS NULL
    //   3. ItemGroup.Default*GlAccountId  (group-level fallback)
    //   4. WarehouseMaster.Default*GlAccountId  (warehouse-level fallback)
    //   5. Hard error (mis-configured tenant)
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NULL = system template
    // (the skeleton rows we ship for each (ItemGroupType × TransactionType)
    // combo), set = tenant override.
    //
    // UNIQUE: (CompanyId, ItemGroupId, TransactionType, WarehouseId).
    // Partial indexes per the NULL pattern.
    //
    // AUTHORITY:
    //   - docs/adr/ADR-019-wms-posting-profile-pattern.md (THIS PR ships it)
    //   - docs/research/master-files-baseline-2026-05-24.md §6.5
    //   - ADR-003 central-gl-account-resolver (PostingProfile is the new
    //     primary path; legacy resolver stays as fallback)
    //   - memory: reference_master_files_baseline.md
    // =============================================================================
    [Table("PostingProfiles")]
    public class PostingProfile
    {
        public int Id { get; set; }

        // NULL = system template skeleton, set = tenant override.
        public int? CompanyId { get; set; }

        // FK to ItemGroup. REQUIRED.
        public int ItemGroupId { get; set; }
        public ItemGroup? ItemGroup { get; set; }

        public InventoryTransactionType TransactionType { get; set; } = InventoryTransactionType.Receipt;

        // Optional warehouse override. NULL = applies to ALL warehouses for
        // this CompanyId × ItemGroup × TransactionType.
        public int? WarehouseId { get; set; }
        public WarehouseMaster? Warehouse { get; set; }

        // ---------------------------------------------------------------------
        // POSTING DETAILS — the actual GL hits.
        //
        // Most transaction types only need one side because the other side is
        // a system clearing account or AP/AR (handled elsewhere in the JE
        // assembly). For example:
        //   Receipt:  Dr Inventory (DebitGlAccount)   Cr GRNI (system)
        //   Issue:    Dr COGS (DebitGlAccount)        Cr Inventory (CreditGlAccount)
        //   Scrap:    Dr ScrapExpense (DebitGlAccount) Cr Inventory (CreditGlAccount)
        //
        // The service layer that consumes PostingProfile knows whether
        // DebitGl + CreditGl are both required or only one.
        // ---------------------------------------------------------------------
        public int? DebitGlAccountId { get; set; }
        public int? CreditGlAccountId { get; set; }

        // Optional offset for variance / scrap reasons — e.g. PPV split
        // between MaterialPriceVariance (PRA-5a) and InventoryAdjustment.
        public int? OffsetGlAccountId { get; set; }

        // ---------------------------------------------------------------------
        // PRIORITY — when multiple matching rows exist for the same
        // (CompanyId, ItemGroupId, TransactionType, WarehouseId) tuple
        // (rare but possible during tenant migration), the higher
        // Priority wins.
        // ---------------------------------------------------------------------
        public int Priority { get; set; } = 100;

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // InventoryTransactionType — every inventory-affecting event that needs
    // a GL posting. Maps closely to the SAP MM movement-type catalog +
    // Dynamics 365 inventory-journal types, filtered to what IndustryOS
    // actually emits.
    // =============================================================================
    public enum InventoryTransactionType
    {
        // Inbound from supplier — Dr Inventory, Cr GRNI/AP.
        Receipt = 0,

        // Issue to production — Dr WIP, Cr RM inventory.
        IssueToProduction = 1,

        // Issue to cost (consumable/MRO use) — Dr Expense, Cr Inventory.
        IssueToExpense = 2,

        // Outbound to customer — Dr COGS, Cr FG inventory.
        Sale = 3,

        // Return from customer — reverse of Sale.
        CustomerReturn = 4,

        // Return to vendor — reverse of Receipt.
        VendorReturn = 5,

        // Internal transfer between warehouses or bins.
        Transfer = 6,

        // Adjustment (cycle count, write-off).
        Adjustment = 7,

        // Scrap event — Dr Scrap, Cr Inventory.
        Scrap = 8,

        // Rework consumption — Dr Rework, Cr Inventory.
        Rework = 9,

        // Production completion — Dr FG, Cr WIP (or WipToFgClearing).
        ProductionComplete = 10,

        // Subcontract receipt — Dr SubcontractInventory, Cr SubcontractAP.
        SubcontractReceipt = 11,

        // Consignment receipt — NO inventory hit; memo-only.
        ConsignmentReceipt = 12,

        // Consignment consumption — Dr COGS, Cr AP-Consignment.
        ConsignmentConsumption = 13,

        // Revaluation / standard cost change — Dr/Cr Inventory, Cr/Dr Variance.
        Revaluation = 14,

        // Quality hold release — usually a status change, but some tenants
        // post a movement to a "released stock" account.
        QualityRelease = 15,

        // Capitalize an inventory item into the fixed-asset ledger.
        CapitalizeToAsset = 16,

        Other = 99
    }
}
