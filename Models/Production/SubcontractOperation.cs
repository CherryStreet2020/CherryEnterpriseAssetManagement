// Sprint 15.1 PR-4 (2026-05-28) — SubcontractOperation entity.
//
// THE SUBCONTRACT-AWARE ROUTING OPERATION.
//
// Most manufacturing routings have internal operations. Subcontract operations
// are different: the work happens at a supplier. We ship physical WIP out, the
// supplier processes it, we receive it back, and we pay them for the service.
//
// This entity sits alongside the regular routing operation and adds the
// subcontract-specific configuration: which supplier, what service item is
// purchased, how WIP ships, lead times, lifecycle status.
//
// Per Dean's spec §6B (35 canonical fields).
//
// LIFECYCLE: 12 states (§10). NotReady → ReadyToBuy → PoCreated → ReadyToShip
// → ShippedToVendor → AtVendor → PartiallyReceived → InInspection → Rejected
// → ReworkAtVendor → Complete → Closed.
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §6B §9 §10
//   - docs/research/purchasing-cascade-design-2026-05-28.md PR-4
//   - SubcontractDemand: the two-demand pattern (§9) — service purchase + WIP movement

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// Operation-level subcontract lifecycle from spec §10 (12 states).
    /// </summary>
    public enum SubcontractOperationStatus
    {
        /// <summary>Prior operations not yet complete; can't ship WIP yet.</summary>
        NotReady = 0,
        /// <summary>Prior ops complete or scheduled close enough — buyer can create PO.</summary>
        ReadyToBuy = 1,
        /// <summary>Subcontract PO created.</summary>
        PoCreated = 2,
        /// <summary>PO approved + WIP ready — operation can ship.</summary>
        ReadyToShip = 3,
        /// <summary>Physical WIP shipped to vendor.</summary>
        ShippedToVendor = 4,
        /// <summary>WIP confirmed received at vendor; processing begins.</summary>
        AtVendor = 5,
        /// <summary>Vendor returned some of the lot; remainder still out.</summary>
        PartiallyReceived = 6,
        /// <summary>Returned WIP is in incoming inspection.</summary>
        InInspection = 7,
        /// <summary>Inspection failed — material rejected back to vendor.</summary>
        Rejected = 8,
        /// <summary>Vendor reworking — clock restarts but op not at Complete yet.</summary>
        ReworkAtVendor = 9,
        /// <summary>Vendor processing accepted; material moves to next op.</summary>
        Complete = 10,
        /// <summary>Final terminal — op closed in production order.</summary>
        Closed = 11,
    }

    /// <summary>
    /// How the subcontract service is costed.
    /// </summary>
    public enum SubcontractCostMethod
    {
        /// <summary>Fixed unit price from PO line.</summary>
        FixedPriceFromPo = 0,
        /// <summary>Cost tied to actual hours/quantity processed (PO is a price agreement).</summary>
        ActualHoursOrQuantity = 1,
        /// <summary>Cost rate from supplier-managed work-center setup.</summary>
        StandardRate = 2,
        /// <summary>Tiered pricing (volume discount table).</summary>
        TieredVolume = 3,
    }

    /// <summary>
    /// Who pays freight + bears in-transit risk.
    /// </summary>
    public enum FreightResponsibility
    {
        Us = 0,
        Supplier = 1,
        Customer = 2,
        ThirdParty = 3,
    }

    /// <summary>
    /// PO creation status (sub-lifecycle inside operation lifecycle).
    /// </summary>
    public enum SubcontractPoCreationStatus
    {
        NotCreated = 0,
        InProgress = 1,
        Created = 2,
        Approved = 3,
        SentToSupplier = 4,
        SupplierAcknowledged = 5,
        Cancelled = 6,
    }

    /// <summary>
    /// Per-operation shipment status to vendor.
    /// </summary>
    public enum SubcontractShipmentStatus
    {
        NotShipped = 0,
        Picked = 1,
        Staged = 2,
        InTransit = 3,
        Delivered = 4,
        Cancelled = 5,
    }

    /// <summary>
    /// Per-operation receipt-back status from vendor.
    /// </summary>
    public enum SubcontractReceiptStatus
    {
        NotReceived = 0,
        InTransitBack = 1,
        PartiallyReceived = 2,
        FullyReceived = 3,
        Rejected = 4,
        ClosedShort = 5,
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SubcontractOperation — the routing op for outside processing
    // ═══════════════════════════════════════════════════════════════════════

    public class SubcontractOperation
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── Production order context ──────────────────

        [Required]
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>The routing operation sequence this subcontract op applies to.</summary>
        [Required]
        [Display(Name = "Operation Sequence")]
        public int OperationSequence { get; set; }

        // ──────────────────────── §6B: Operation identity ────────────────────

        /// <summary>Op code (e.g., "OP-040-HT" — heat-treat op).</summary>
        [Required, StringLength(32)]
        [Display(Name = "Operation Code")]
        public string OperationCode { get; set; } = string.Empty;

        [Required, StringLength(500)]
        [Display(Name = "Operation Description")]
        public string OperationDescription { get; set; } = string.Empty;

        // ──────────────────────── §6B: Supplier identity ─────────────────────

        public int? SupplierId { get; set; }
        public Vendor? Supplier { get; set; }

        [StringLength(64)]
        [Display(Name = "Supplier Site")]
        public string? SupplierSiteCode { get; set; }

        [StringLength(64)]
        [Display(Name = "Vendor Resource")]
        public string? VendorResource { get; set; }

        // ──────────────────────── §6B: Service identity ──────────────────────

        /// <summary>Item Master "service item" purchased (the outside-processing service item).</summary>
        public int? ServiceItemId { get; set; }
        public Item? ServiceItem { get; set; }

        [StringLength(500)]
        [Display(Name = "Service Description")]
        public string? ServiceDescription { get; set; }

        /// <summary>How service qty derives from material qty: 1:1, per-batch, per-lb, etc.</summary>
        [StringLength(64)]
        [Display(Name = "Service Quantity Rule")]
        public string? ServiceQuantityRule { get; set; }

        [StringLength(16)]
        [Display(Name = "Service UOM")]
        public string? ServiceUom { get; set; }

        // ──────────────────────── §6B: Lead times ────────────────────────────

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Fixed Lead Time (days)")]
        public decimal FixedLeadTimeDays { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Variable Lead Time (days per unit)")]
        public decimal VariableLeadTimeDaysPerUnit { get; set; }

        // ──────────────────────── §6B: Workflow flags ────────────────────────

        [Display(Name = "Ship WIP Required")]
        public bool ShipWipRequired { get; set; } = true;

        [Display(Name = "Generate Subcontract PO")]
        public bool GenerateSubcontractPo { get; set; } = true;

        [Display(Name = "Generate Shipment")]
        public bool GenerateShipment { get; set; } = true;

        /// <summary>Routing-op seq that must be Complete before this op can ReadyToBuy.</summary>
        [Display(Name = "Prior Operation Seq")]
        public int? PriorOperationSequence { get; set; }

        /// <summary>Routing-op seq the WIP returns to after this op.</summary>
        [Display(Name = "Return Operation Seq")]
        public int? ReturnOperationSequence { get; set; }

        // ──────────────────────── §6B: Locations ─────────────────────────────

        public int? VendorWipWarehouseId { get; set; }
        public Abs.FixedAssets.Models.Masters.WarehouseMaster? VendorWipWarehouse { get; set; }

        [StringLength(120)]
        [Display(Name = "Vendor WIP Location")]
        public string? VendorWipLocation { get; set; }

        public int? ShipFromLocationId { get; set; }
        public Location? ShipFromLocation { get; set; }

        public int? ReturnToLocationId { get; set; }
        public Location? ReturnToLocation { get; set; }

        // ──────────────────────── §6B: Compliance flags ──────────────────────

        [Display(Name = "Inspection on Return")]
        public bool InspectionOnReturn { get; set; }

        [Display(Name = "Cert Required")]
        public bool CertRequired { get; set; }

        // ──────────────────────── §6B: Instructions ──────────────────────────

        [StringLength(4000)]
        [Display(Name = "Supplier Instructions")]
        public string? SupplierInstructions { get; set; }

        [StringLength(4000)]
        [Display(Name = "Packaging Instructions")]
        public string? PackagingInstructions { get; set; }

        // ──────────────────────── §6B: Shipping + cost ───────────────────────

        [StringLength(64)]
        [Display(Name = "Shipping Method")]
        public string? ShippingMethod { get; set; }

        public FreightResponsibility FreightResponsibility { get; set; } = FreightResponsibility.Us;

        public SubcontractCostMethod CostMethod { get; set; } = SubcontractCostMethod.FixedPriceFromPo;

        /// <summary>
        /// Unit cost of the vendor service (e.g., $X per part heat-treated).
        /// Drives the service-purchase demand cost on the dual-demand binding.
        /// Codex P2 fix: was previously dropped on create.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Service Unit Cost")]
        public decimal ServiceUnitCost { get; set; }

        /// <summary>
        /// FK to the physical item being shipped out and back. Drives the WIP
        /// movement demand's ItemId. Codex P2 fix: was previously dropped.
        /// </summary>
        [Display(Name = "WIP Item")]
        public int? WipItemId { get; set; }
        public Item? WipItem { get; set; }

        // ──────────────────────── §6B: Status quartet ────────────────────────

        public SubcontractOperationStatus Status { get; set; } = SubcontractOperationStatus.NotReady;

        public SubcontractPoCreationStatus PoCreationStatus { get; set; } = SubcontractPoCreationStatus.NotCreated;

        public SubcontractShipmentStatus ShipmentStatus { get; set; } = SubcontractShipmentStatus.NotShipped;

        public SubcontractReceiptStatus ReceiptStatus { get; set; } = SubcontractReceiptStatus.NotReceived;

        // ──────────────────────── §6B: Completion rules ──────────────────────

        [StringLength(64)]
        [Display(Name = "Operation Completion Rule")]
        public string? OperationCompletionRule { get; set; }

        [StringLength(64)]
        [Display(Name = "Rework Rule")]
        public string? ReworkRule { get; set; }

        [StringLength(64)]
        [Display(Name = "Scrap Rule")]
        public string? ScrapRule { get; set; }

        // ──────────────────────── Linkage to supply records ──────────────────

        /// <summary>Subcontract PO line generated for the service purchase.</summary>
        public int? ServicePurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? ServicePurchaseOrderLine { get; set; }

        // ──────────────────────── Quantity totals ────────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty to Ship")]
        public decimal QuantityToShip { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Shipped")]
        public decimal QuantityShipped { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Received Back")]
        public decimal QuantityReceivedBack { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Accepted")]
        public decimal QuantityAccepted { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Rejected")]
        public decimal QuantityRejected { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Qty Scrapped At Vendor")]
        public decimal QuantityScrappedAtVendor { get; set; }

        // ──────────────────────── Dates ──────────────────────────────────────

        [Display(Name = "Required Ship Date")]
        public DateTime? RequiredShipDate { get; set; }

        [Display(Name = "Required Back Date")]
        public DateTime? RequiredBackDate { get; set; }

        [Display(Name = "Actual Ship Date")]
        public DateTime? ActualShipDate { get; set; }

        [Display(Name = "Actual Back Date")]
        public DateTime? ActualBackDate { get; set; }

        // ──────────────────────── Audit ──────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public ICollection<SubcontractDemand> Demands { get; set; } = new List<SubcontractDemand>();

        public byte[]? RowVersion { get; set; }
    }
}
