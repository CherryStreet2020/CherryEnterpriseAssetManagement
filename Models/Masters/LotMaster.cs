using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-7 — LotMaster (batch/lot traceability).
    //
    // Replaces the flat `LotNumber` string column on StockReceipt + various
    // production tables. Lot is a first-class entity because it carries
    // shape that a string can't:
    //   - Item FK         — which SKU the lot represents
    //   - SupplierLotNumber — vendor's own lot id (often differs from ours)
    //   - ManufactureDate / ExpiryDate — shelf-life + FEFO sorting
    //   - CoaFileRef — supplier Certificate of Analysis attachment
    //   - Status — Active / Quarantine / OnHold / Released / Expired / Scrapped
    //   - ParentLotId — sub-lot / parent-lot genealogy (lot splits at picking)
    //
    // OPERATIONAL DATA — CompanyId is NOT NULL (lots are always tenant-
    // specific; no system templates apply). LocationId is OPTIONAL (lot
    // is logical; bin-level location lives on the inventory snapshot row,
    // not the lot definition).
    //
    // UNIQUE: (CompanyId, ItemId, LotNumber). Same lot number across two
    // different SKUs is allowed.
    //
    // PR #5f genealogy (deferred) will hang off ParentLotId + lot consumption
    // events to build the full lot family tree (LotGenealogy in MES cascade).
    //
    // AUTHORITY:
    //   - docs/adr/ADR-019-wms-posting-profile-pattern.md
    //   - docs/research/master-files-baseline-2026-05-24.md §6.4
    //   - memory: reference_bic_entity_checklist.md
    // =============================================================================
    [Table("LotMasters")]
    public class LotMaster
    {
        public int Id { get; set; }

        // Tenant-owned operational data — never NULL.
        public int CompanyId { get; set; }

        // FK to Item (existing entity in Models/Item.cs).
        public int ItemId { get; set; }

        // Our internal lot identifier (e.g. "LOT-2026-00012345"). Often
        // auto-generated on receipt; tenant-configurable format.
        [Required, StringLength(64)]
        public string LotNumber { get; set; } = string.Empty;

        // Supplier's lot number — for traceability back to source. Often
        // printed on the supplier's CoA / packing slip.
        [StringLength(64)]
        public string? SupplierLotNumber { get; set; }

        // Vendor FK (optional) — which vendor shipped the lot.
        public int? VendorId { get; set; }

        // ---------------------------------------------------------------------
        // SHELF-LIFE / FEFO sorting fields.
        // ---------------------------------------------------------------------

        public DateTime? ManufactureDate { get; set; }

        public DateTime? ReceiptDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        // "Best by" / "Use by" — softer than ExpiryDate for some industries.
        public DateTime? BestByDate { get; set; }

        // Days remaining warning threshold — drives the lot-aging KPI band.
        public int? ShelfLifeWarningDays { get; set; }

        // ---------------------------------------------------------------------
        // DOCUMENTATION refs.
        // ---------------------------------------------------------------------

        // Storage path / URI to the supplier Certificate of Analysis PDF.
        // Format is opaque to the model — service layer decides whether it's
        // an S3 key, a Box link, an Egnyte URL, etc.
        [StringLength(500)]
        public string? CoaFileRef { get; set; }

        // Free-form notes (supplier quality info, inspection findings).
        [StringLength(2000)]
        public string? Notes { get; set; }

        // ---------------------------------------------------------------------
        // GENEALOGY — parent lot for split lots. NULL for received-from-vendor
        // lots; set for lots produced by splitting a parent at pick or production.
        // ---------------------------------------------------------------------
        public int? ParentLotId { get; set; }
        public LotMaster? ParentLot { get; set; }

        public LotStatus Status { get; set; } = LotStatus.Active;

        // Quantity received (the original received amount; current on-hand
        // lives on inventory snapshot rows tied to (Lot, Bin)).
        [Column(TypeName = "numeric(18,4)")]
        public decimal? OriginalQuantity { get; set; }

        // FK to UnitOfMeasureMaster (PRA-4). NULL means inherit from Item.
        public int? UomId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    public enum LotStatus
    {
        // Available for allocation.
        Active = 0,

        // Failed quality inspection — held pending disposition.
        Quarantine = 1,

        // On hold (regulatory, supplier dispute, customer hold).
        OnHold = 2,

        // Passed inspection — released to be allocated. Most lots that
        // were Quarantined and passed transition to Released; Active is
        // the receipt-time default for non-inspected lots.
        Released = 3,

        // Past expiry date — system flag; cannot allocate.
        Expired = 4,

        // Written off / scrapped.
        Scrapped = 5,

        // Fully consumed (zero on-hand) — kept for historical traceability.
        Consumed = 6
    }
}
