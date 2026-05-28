// Sprint 15.1 PR-5 (2026-05-28) — VendorWipTransaction entity.
//
// MOVEMENT HISTORY of WIP at vendor. Every physical move (ship to, process,
// return from, inspect, accept, reject, scrap, lose) is one row. Balance
// rows are derived aggregates over this transaction grain.
//
// Per §6D field list + §5 Step 6 lifecycle + §11 receipt scenarios.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// What kind of movement does this transaction record?
    /// Derived from §11 receipt scenarios + §5 Step 6 lifecycle states.
    /// </summary>
    public enum VendorWipTransactionType
    {
        /// <summary>Ship physical material out to vendor.</summary>
        ShipToVendor = 0,
        /// <summary>Confirmed receipt at vendor facility.</summary>
        ConfirmedAtVendor = 1,
        /// <summary>Vendor began processing.</summary>
        VendorProcessingStarted = 2,
        /// <summary>Vendor finished processing.</summary>
        VendorProcessingComplete = 3,
        /// <summary>Vendor placed material on hold.</summary>
        OnHoldAtVendor = 4,
        /// <summary>Material in transit back to us.</summary>
        InTransitBack = 5,
        /// <summary>Material received back at our dock.</summary>
        ReceivedBack = 6,
        /// <summary>Material placed in incoming inspection.</summary>
        InspectionStarted = 7,
        /// <summary>Inspection accepted the material.</summary>
        InspectionAccepted = 8,
        /// <summary>Inspection rejected the material.</summary>
        InspectionRejected = 9,
        /// <summary>Material scrapped at vendor (vendor's process scrap).</summary>
        ScrappedAtVendor = 10,
        /// <summary>Material lost / damaged in transit or at vendor.</summary>
        LostOrDamaged = 11,
        /// <summary>Adjustment / cycle-count correction.</summary>
        Adjustment = 12,
        /// <summary>Return of accepted material to inventory after processing.</summary>
        ReturnedToInventory = 13,
        /// <summary>Transaction reversed (cancellation).</summary>
        Reversed = 14,
    }

    public class VendorWipTransaction
    {
        public int Id { get; set; }

        // ──────────────────────── Tenant trio ───────────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── Identity ──────────────────────────────────

        [Required, StringLength(48)]
        [Display(Name = "Transaction Number")]
        public string TransactionNumber { get; set; } = string.Empty;

        public VendorWipTransactionType TransactionType { get; set; } = VendorWipTransactionType.ShipToVendor;

        // ──────────────────────── Production context ─────────────────────────

        [Required]
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        public int OperationSequence { get; set; }

        public int? SubcontractOperationId { get; set; }
        public SubcontractOperation? SubcontractOperation { get; set; }

        // ──────────────────────── Balance linkage ───────────────────────────

        /// <summary>FK to the VendorWipBalance row this transaction affects.</summary>
        [Required]
        public int VendorWipBalanceId { get; set; }
        public VendorWipBalance? VendorWipBalance { get; set; }

        // ──────────────────────── Supplier + location ───────────────────────

        public int? SupplierId { get; set; }
        public Vendor? Supplier { get; set; }

        public int? VendorLocationId { get; set; }
        public VendorLocation? VendorLocation { get; set; }

        public int? PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        // ──────────────────────── Item identity ─────────────────────────────

        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(50)]
        public string? PartNumber { get; set; }

        [StringLength(32)]
        public string? Revision { get; set; }

        [StringLength(48)]
        public string? LotNumber { get; set; }

        [StringLength(48)]
        public string? SerialNumber { get; set; }

        // ──────────────────────── Movement ──────────────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtendedValue { get; set; }

        [StringLength(16)]
        public string? Uom { get; set; }

        // ──────────────────────── Source / destination ──────────────────────

        [StringLength(120)]
        [Display(Name = "From Location")]
        public string? FromLocationDescription { get; set; }

        [StringLength(120)]
        [Display(Name = "To Location")]
        public string? ToLocationDescription { get; set; }

        [StringLength(48)]
        [Display(Name = "Shipment Document")]
        public string? ShipmentDocument { get; set; }

        [StringLength(48)]
        [Display(Name = "Receipt Document")]
        public string? ReceiptDocument { get; set; }

        // ──────────────────────── Dates ─────────────────────────────────────

        [Display(Name = "Transaction UTC")]
        public DateTime TransactionUtc { get; set; } = DateTime.UtcNow;

        [Display(Name = "Required Return Date")]
        public DateTime? RequiredReturnDate { get; set; }

        // ──────────────────────── Reversal linkage ──────────────────────────

        public int? ReverseOfTransactionId { get; set; }
        public VendorWipTransaction? ReverseOfTransaction { get; set; }

        // ──────────────────────── Reason / notes ────────────────────────────

        [StringLength(128)]
        public string? ReasonCode { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
