using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-7 — SerialMaster (per-unit serial number traceability).
    //
    // Each row = one physical unit of a serial-tracked SKU. Lives separately
    // from inventory snapshot rows so the serial CARD (full lifecycle history
    // + RMA history + warranty status) survives even when the unit moves
    // between bins or leaves inventory entirely (sold, scrapped, refurbished).
    //
    // Per-unit serials are required for:
    //   - AS9102 FAI parts (ABS aerospace baseline already in place)
    //   - Medical devices (UDI compliance)
    //   - Capital equipment / asset tags
    //   - High-value electronics
    //   - Subassembly genealogy (PR #5f SerialGenealogy will hang off this)
    //
    // CROSS-DOMAIN BRIDGE: a SerialMaster row may also map to an EAM Asset
    // row when the unit is sold/transferred to a maintenance program. The
    // AssetId FK is the bridge (NULL for most inventory serials).
    //
    // OPERATIONAL DATA — CompanyId is NOT NULL (serials are always tenant-
    // specific; no system templates).
    //
    // UNIQUE: (CompanyId, ItemId, SerialNumber). Same serial across two
    // different SKUs is allowed in theory; same serial twice for the same
    // SKU is impossible by definition.
    //
    // AUTHORITY:
    //   - docs/adr/ADR-019-wms-posting-profile-pattern.md
    //   - docs/research/master-files-baseline-2026-05-24.md §6.4
    //   - memory: reference_bic_entity_checklist.md
    // =============================================================================
    [Table("SerialMasters")]
    public class SerialMaster
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        // FK to Item (existing entity in Models/Item.cs).
        public int ItemId { get; set; }

        // The unique serial number for this unit. Format is tenant-specific;
        // some industries use 10-digit numeric, some use alphanumeric, some
        // use barcoded UDIs.
        [Required, StringLength(64)]
        public string SerialNumber { get; set; } = string.Empty;

        // Optional FK to LotMaster — for items that are both lot- AND
        // serial-tracked (typical medical / aerospace).
        public int? LotId { get; set; }
        public LotMaster? Lot { get; set; }

        // ---------------------------------------------------------------------
        // CURRENT LOCATION — where the unit physically lives RIGHT NOW.
        // These fields move as the unit moves; the unit's full history lives
        // in the (future) chain-of-custody graph.
        // ---------------------------------------------------------------------
        public int? CurrentWarehouseId { get; set; }
        public WarehouseMaster? CurrentWarehouse { get; set; }

        public int? CurrentBinId { get; set; }
        public BinMaster? CurrentBin { get; set; }

        // ---------------------------------------------------------------------
        // LIFECYCLE STATUS.
        // ---------------------------------------------------------------------

        public SerialLifecycleStatus LifecycleStatus { get; set; } = SerialLifecycleStatus.New;

        // True the moment LifecycleStatus moves to Sold/Scrapped — unit no
        // longer in our inventory. Kept separate from LifecycleStatus so we
        // can filter quickly without enumerating multiple terminal states.
        public bool IsOutOfInventory { get; set; } = false;

        // ---------------------------------------------------------------------
        // OPTIONAL EAM-BRIDGE — when a serial-tracked unit ends up under a
        // maintenance program (delivered industrial machine, fleet vehicle),
        // it gets an Asset row in the EAM hierarchy. NULL for typical
        // inventory serials.
        // ---------------------------------------------------------------------
        public int? AssetId { get; set; }

        // ---------------------------------------------------------------------
        // DATES.
        // ---------------------------------------------------------------------

        public DateTime? ManufactureDate { get; set; }
        public DateTime? ReceiptDate { get; set; }
        public DateTime? ShipDate { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }

        // ---------------------------------------------------------------------
        // SOURCE / DESTINATION — useful for RMA + warranty lookups.
        // ---------------------------------------------------------------------
        public int? OriginVendorId { get; set; }
        public int? CurrentCustomerId { get; set; }

        // Source receipt + order references (NULL until first transaction).
        public int? OriginReceiptId { get; set; }
        public int? OriginProductionOrderId { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    public enum SerialLifecycleStatus
    {
        // Newly created (planned-but-not-physically-received) — typical for
        // pre-printed serial label workflows.
        New = 0,

        // Physically present, in-process / not yet released for issue.
        InProcess = 1,

        // On hand and available for allocation.
        OnHand = 2,

        // Allocated to a sales order / production order — physically here
        // but reserved.
        Allocated = 3,

        // Shipped to customer.
        Sold = 4,

        // In transit between two of our warehouses.
        InTransit = 5,

        // Returned by customer — awaiting disposition.
        Returned = 6,

        // Sent to a repair / refurbishment workflow.
        InRepair = 7,

        // Refurbished and back on hand.
        Refurbished = 8,

        // Scrapped / written off.
        Scrapped = 9,

        // Transferred out of inventory into an Asset (EAM bridge).
        TransferredToAsset = 10
    }
}
