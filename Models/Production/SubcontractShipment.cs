// Sprint 15.2 PR-6 (2026-05-28) — SubcontractShipment + SubcontractShipmentLine.
//
// THE PHYSICAL WIP-TO-VENDOR SHIPMENT.
//
// SubcontractOperation tracks the routing op + supplier + service item + lifecycle.
// SubcontractDemand binds the two ProductionSupplyDemand rows (service + WIP).
// SubcontractShipment is the *physical event*: WIP leaves our dock and travels
// to the vendor's location. One shipment can carry multiple lines (parts +
// lots + serials + revisions) and pulls cost off our inventory while transit
// liability lives in vendor WIP.
//
// Per Dean's spec §5 step 5 ("Ship WIP to vendor") and §14 (vendor WIP statuses).
//
// LIFECYCLE: 7 states. Draft → Picked → Staged → InTransit → DeliveredToVendor
// → Reconciled → Cancelled.
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §5 §6D §14
//   - docs/research/purchasing-cascade-design-2026-05-28.md PR-6
//   - SubcontractOperation: the routing-op record
//   - VendorWipTransaction: the inventory-grain WIP movement record (PR-5)

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// Physical-shipment lifecycle for the WIP-to-vendor flow.
    /// </summary>
    public enum SubcontractShipmentLifecycle
    {
        /// <summary>Header created, lines being added.</summary>
        Draft = 0,
        /// <summary>Pick complete — WIP gathered at staging.</summary>
        Picked = 1,
        /// <summary>Staged at dock awaiting carrier.</summary>
        Staged = 2,
        /// <summary>Carrier picked up — material en route to vendor.</summary>
        InTransit = 3,
        /// <summary>Vendor confirmed receipt at their dock.</summary>
        DeliveredToVendor = 4,
        /// <summary>Cost + quantity reconciled against subcontract op.</summary>
        Reconciled = 5,
        /// <summary>Shipment cancelled before delivery.</summary>
        Cancelled = 6,
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SubcontractShipment — the header for one physical WIP shipment
    // ═══════════════════════════════════════════════════════════════════════

    public class SubcontractShipment
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── Shipment identity ─────────────────────────

        /// <summary>Tenant-unique shipment number (e.g., SCSHP-2026-00041).</summary>
        [Required, StringLength(48)]
        [Display(Name = "Shipment Number")]
        public string ShipmentNumber { get; set; } = string.Empty;

        // ──────────────────────── Subcontract context ───────────────────────

        /// <summary>The subcontract operation this WIP is being sent for.</summary>
        [Required]
        public int SubcontractOperationId { get; set; }
        public SubcontractOperation? SubcontractOperation { get; set; }

        /// <summary>Denormalized PRO id for fast filtering on Cockpit + queues.</summary>
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>Denormalized op sequence.</summary>
        public int OperationSequence { get; set; }

        /// <summary>The dual-demand binding (links to the WIP movement demand).</summary>
        public int? SubcontractDemandId { get; set; }
        public SubcontractDemand? SubcontractDemand { get; set; }

        /// <summary>The subcontract PO line that paid for the service.</summary>
        public int? ServicePurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? ServicePurchaseOrderLine { get; set; }

        // ──────────────────────── Supplier + locations ──────────────────────

        [Required]
        public int SupplierId { get; set; }
        public Vendor? Supplier { get; set; }

        /// <summary>Vendor location WIP travels TO (the processing plant).</summary>
        public int? VendorLocationId { get; set; }
        public VendorLocation? VendorLocation { get; set; }

        /// <summary>Internal location WIP ships FROM.</summary>
        public int? ShipFromLocationId { get; set; }
        public Location? ShipFromLocation { get; set; }

        [StringLength(120)]
        [Display(Name = "Vendor WIP Location")]
        public string? VendorWipLocationCode { get; set; }

        // ──────────────────────── Carrier + transit ─────────────────────────

        [StringLength(96)]
        [Display(Name = "Carrier")]
        public string? Carrier { get; set; }

        [StringLength(96)]
        [Display(Name = "Shipping Method")]
        public string? ShippingMethod { get; set; }

        [StringLength(96)]
        [Display(Name = "Tracking Number")]
        public string? TrackingNumber { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Freight Cost")]
        public decimal? FreightCost { get; set; }

        [StringLength(8)]
        [Display(Name = "Freight Currency")]
        public string? FreightCurrency { get; set; }

        // ──────────────────────── Dates ─────────────────────────────────────

        [Display(Name = "Required Ship Date")]
        public DateTime? RequiredShipDate { get; set; }

        [Display(Name = "Actual Ship Date")]
        public DateTime? ActualShipDate { get; set; }

        [Display(Name = "Expected Delivery Date")]
        public DateTime? ExpectedDeliveryDate { get; set; }

        [Display(Name = "Actual Delivery Date")]
        public DateTime? ActualDeliveryDate { get; set; }

        // ──────────────────────── Lifecycle ─────────────────────────────────

        public SubcontractShipmentLifecycle Status { get; set; } = SubcontractShipmentLifecycle.Draft;

        [Display(Name = "Cert Required")]
        public bool CertRequired { get; set; }

        [StringLength(4000)]
        [Display(Name = "Packing Instructions")]
        public string? PackingInstructions { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public ICollection<SubcontractShipmentLine> Lines { get; set; } = new List<SubcontractShipmentLine>();

        public byte[]? RowVersion { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SubcontractShipmentLine — one physical lot/serial within a shipment
    // ═══════════════════════════════════════════════════════════════════════

    public class SubcontractShipmentLine
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        [Required]
        public int SubcontractShipmentId { get; set; }
        public SubcontractShipment? SubcontractShipment { get; set; }

        /// <summary>1-based line number within the shipment.</summary>
        [Display(Name = "Line #")]
        public int LineNumber { get; set; }

        // ──────────────────────── Physical item identity ────────────────────

        /// <summary>The WIP item being shipped (the in-process part).</summary>
        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(64)]
        [Display(Name = "Part Number")]
        public string? PartNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(32)]
        [Display(Name = "Drawing Revision")]
        public string? DrawingRevision { get; set; }

        [StringLength(64)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(64)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        // ──────────────────────── Quantities ────────────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Shipped")]
        public decimal QuantityShipped { get; set; }

        [Required, StringLength(16)]
        public string Uom { get; set; } = "EA";

        /// <summary>Frozen cost basis per UOM at the moment of shipment.
        /// Used to value vendor WIP and to back-out cost if the shipment
        /// is cancelled before delivery.</summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Unit Cost Snapshot")]
        public decimal? UnitCostSnapshot { get; set; }

        // ──────────────────────── Downstream linkage ────────────────────────

        /// <summary>VendorWipTransaction created for this line at delivery
        /// time (FK back to inventory-grain ledger).</summary>
        public int? VendorWipTransactionId { get; set; }
        public VendorWipTransaction? VendorWipTransaction { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
