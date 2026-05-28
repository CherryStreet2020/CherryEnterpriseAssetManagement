// Sprint 15.1 PR-5 (2026-05-28) — VendorWipBalance entity.
//
// AGGREGATE QUANTITY-AT-VENDOR per (PRO, Operation, Supplier, Part, Revision).
//
// One row per material being tracked at a specific vendor location for a
// specific job/operation. Quantities update on every transaction; the
// balance row is the read-optimized aggregate so dashboards don't have to
// sum the transaction history every time.
//
// Per §6D + §25 KPI hints + §5 Step-6 lifecycle states.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Spec §14 "Inventory statuses at vendor" + §5 Step-6 lifecycle.
    /// </summary>
    public enum VendorWipInventoryStatus
    {
        InTransitToVendor = 0,
        AtVendorAvailable = 1,
        AtVendorAssignedToJob = 2,
        AtVendorInProcess = 3,
        AtVendorAwaitingReturn = 4,
        AtVendorOnHold = 5,
        AtVendorRejected = 6,
        AtVendorScrap = 7,
        InTransitFromVendor = 8,
        ReceivedBack = 9,
        Closed = 10,
    }

    /// <summary>
    /// Who owns this material while at the vendor?
    /// </summary>
    public enum VendorWipOwnership
    {
        Us = 0,
        Customer = 1,
        Supplier = 2,
        Consigned = 3,
    }

    /// <summary>
    /// Cost-system view of this WIP at vendor.
    /// </summary>
    public enum VendorWipValuationStatus
    {
        Valued = 0,
        Unvalued = 1,
        WrittenDown = 2,
        Reserved = 3,
        WrittenOff = 4,
    }

    /// <summary>
    /// Quality classification.
    /// </summary>
    public enum VendorWipQualityStatus
    {
        Unknown = 0,
        Accepted = 1,
        InInspection = 2,
        Hold = 3,
        Rejected = 4,
        Conditional = 5,
    }

    public class VendorWipBalance
    {
        public int Id { get; set; }

        // ──────────────────────── Tenant trio ───────────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── Aggregate key ─────────────────────────────

        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        public int OperationSequence { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public Vendor? Supplier { get; set; }

        public int? VendorLocationId { get; set; }
        public VendorLocation? VendorLocation { get; set; }

        public int? VendorWipWarehouseId { get; set; }
        public WarehouseMaster? VendorWipWarehouse { get; set; }

        [StringLength(120)]
        public string? VendorWipLocationDescription { get; set; }

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

        // ──────────────────────── §6D quantity buckets ──────────────────────

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Shipped (total ship-outs)")]
        public decimal QuantityShipped { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity At Vendor (currently)")]
        public decimal QuantityAtVendor { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Received Back")]
        public decimal QuantityReceivedBack { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Accepted")]
        public decimal QuantityAccepted { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Rejected")]
        public decimal QuantityRejected { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Scrapped at Vendor")]
        public decimal QuantityScrappedAtVendor { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Lost / Damaged")]
        public decimal QuantityLost { get; set; }

        // ──────────────────────── Status quartet ────────────────────────────

        public VendorWipInventoryStatus InventoryStatus { get; set; } = VendorWipInventoryStatus.InTransitToVendor;

        public VendorWipOwnership Ownership { get; set; } = VendorWipOwnership.Us;

        public VendorWipValuationStatus ValuationStatus { get; set; } = VendorWipValuationStatus.Valued;

        public VendorWipQualityStatus QualityStatus { get; set; } = VendorWipQualityStatus.Unknown;

        // ──────────────────────── Cost ──────────────────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Unit Value")]
        public decimal UnitValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Value At Vendor")]
        public decimal TotalValueAtVendor { get; set; }

        // ──────────────────────── Dates ─────────────────────────────────────

        [Display(Name = "First Shipped UTC")]
        public DateTime? FirstShippedUtc { get; set; }

        [Display(Name = "Last Transaction UTC")]
        public DateTime? LastTransactionUtc { get; set; }

        [Display(Name = "Required Return Date")]
        public DateTime? RequiredReturnDate { get; set; }

        [Display(Name = "Aging Days At Vendor")]
        public int AgingDaysAtVendor { get; set; }

        // ──────────────────────── Linkage ───────────────────────────────────

        public int? SubcontractOperationId { get; set; }
        public SubcontractOperation? SubcontractOperation { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        public ICollection<VendorWipTransaction> Transactions { get; set; } = new List<VendorWipTransaction>();

        public byte[]? RowVersion { get; set; }
    }
}
