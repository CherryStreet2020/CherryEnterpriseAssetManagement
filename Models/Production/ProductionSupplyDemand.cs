// Sprint 15.1 PR-2 (2026-05-28) — ProductionSupplyDemand entity.
//
// THE UNIFIED DEMAND RECORD.
//
// Every supply requirement from a Production Order is materialized as a row
// in this table — whether the supply will come from a buy, a make, a transfer,
// inventory, floorstock, customer-supplied stock, consigned stock, or subcontract.
//
// One Production Order generates N ProductionSupplyDemand rows: typically one
// per BOM line (material demand) + one per routing operation that needs a tooling/
// resource/external service (operation demand). Buyers, planners, MRP, supply
// netting, and the Purchasing Control Center all read from this table.
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §6A
//   - docs/research/purchasing-cascade-design-2026-05-28.md PR-2
//   - feedback_b6_go_big_2026_05_26.md — BIC architecture, no shortcuts
//
// LOCKS APPLIED:
//   - Tenant trio (CompanyId required, denormalized from ProductionOrder)
//   - xmin concurrency (MapXminRowVersion in AppDbContext)
//   - All enum fields have HasDefaultValue in AppDbContext
//   - NULL-safe partial UNIQUE on (ProductionOrderId, BomLineId, OperationSequence, Sequence)
//   - All nullable FK columns get nav properties + HasOne config

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// The 12 supply policies from Dean's spec §8. Drives how Purchasing/MRP
    /// resolves each demand line into actual supply records.
    /// </summary>
    public enum SupplyPolicy
    {
        /// <summary>Check inventory first, reserve, buy shortage only.</summary>
        InventoryFirstThenBuy = 0,
        /// <summary>Create PO linked to job/BOM line (default for ETO/MTO).</summary>
        BuyDirectToJob = 1,
        /// <summary>Create PO tied to required operation/date.</summary>
        BuyDirectToOperation = 2,
        /// <summary>Ship/buy directly to subcontract supplier site.</summary>
        BuyToVendorLocation = 3,
        /// <summary>Create child work order to make this material.</summary>
        MakeDirectToJob = 4,
        /// <summary>Use stock or open existing supply job.</summary>
        MakeToStockReserve = 5,
        /// <summary>Create transfer demand from another warehouse.</summary>
        TransferFromWarehouse = 6,
        /// <summary>Reallocate material from another job.</summary>
        TransferFromJob = 7,
        /// <summary>Consume from floorstock / kanban — no demand record consumed.</summary>
        Floorstock = 8,
        /// <summary>Track expected customer receipt — customer ships material.</summary>
        CustomerSupplied = 9,
        /// <summary>Consume supplier-owned consigned stock.</summary>
        Consigned = 10,
        /// <summary>No automatic action — buyer must select source.</summary>
        ManualBuyerDecision = 11,
    }

    /// <summary>
    /// Demand-side classification. Where did this demand come from?
    /// </summary>
    public enum DemandSourceType
    {
        /// <summary>Generated from a BOM material line (most common).</summary>
        BomLine = 0,
        /// <summary>Generated from a routing operation (tooling, resource, external service).</summary>
        RoutingOperation = 1,
        /// <summary>Subcontract operation — service + WIP movement.</summary>
        Subcontract = 2,
        /// <summary>Project-level expense (consulting, freight, setup).</summary>
        ProjectExpense = 3,
        /// <summary>Tooling demand (special tool, gage, fixture).</summary>
        ToolingRequirement = 4,
        /// <summary>Manually created demand — buyer added it.</summary>
        Manual = 5,
    }

    /// <summary>
    /// Source resolution status — has a supply source been determined?
    /// </summary>
    public enum DemandSourceStatus
    {
        /// <summary>No source determined yet — needs buyer/planner action.</summary>
        NotDetermined = 0,
        /// <summary>Source resolved via auto rules (policy + AVL + stock).</summary>
        AutoResolved = 1,
        /// <summary>Source manually overridden by buyer/planner.</summary>
        ManualOverride = 2,
        /// <summary>Source locked — no changes allowed (close-to-completion).</summary>
        Locked = 3,
    }

    /// <summary>
    /// Supply progress — what's the state of the linked supply record?
    /// </summary>
    public enum DemandSupplyStatus
    {
        /// <summary>No supply record exists yet (no PO, no WO, no reservation).</summary>
        NotSupplied = 0,
        /// <summary>Supply created but not yet committed (PO draft, WO not released).</summary>
        Planned = 1,
        /// <summary>Supply committed (PO sent, WO released, reservation made).</summary>
        Committed = 2,
        /// <summary>Supply partially fulfilled (partial receipt, partial transfer).</summary>
        PartiallyFulfilled = 3,
        /// <summary>Supply fully fulfilled — material on hand or at PRO.</summary>
        FullyFulfilled = 4,
        /// <summary>Supply at vendor (subcontract WIP).</summary>
        AtVendor = 5,
        /// <summary>Supply in incoming inspection.</summary>
        InInspection = 6,
        /// <summary>Supply closed — demand satisfied, no further action.</summary>
        Closed = 7,
        /// <summary>Supply cancelled — needs re-resolution.</summary>
        Cancelled = 8,
    }

    /// <summary>
    /// Shortage classification — is this demand at risk?
    /// </summary>
    public enum DemandShortageStatus
    {
        /// <summary>No shortage — supply available by need date.</summary>
        NoShortage = 0,
        /// <summary>Warning — supply tight but expected on time.</summary>
        Warning = 1,
        /// <summary>Critical — supply at risk of being late.</summary>
        Critical = 2,
        /// <summary>Late — supply confirmed past need date.</summary>
        Late = 3,
        /// <summary>Short — quantity insufficient (under-supply confirmed).</summary>
        Short = 4,
        /// <summary>On hold — supply blocked (inspection, quality, credit).</summary>
        OnHold = 5,
    }

    /// <summary>
    /// Cost engagement status — has cost been committed/actualized?
    /// </summary>
    public enum DemandCostStatus
    {
        /// <summary>No cost committed yet (no PO, no WO).</summary>
        NotCommitted = 0,
        /// <summary>Cost committed via PO/WO/reservation but not actualized.</summary>
        Committed = 1,
        /// <summary>Cost actualized (receipt posted, material issued).</summary>
        Actualized = 2,
        /// <summary>Variance pending (PPV, invoice variance not yet settled).</summary>
        VariancePending = 3,
        /// <summary>Cost closed (variance settled, PRO closed).</summary>
        Closed = 4,
    }

    /// <summary>
    /// Alert level for buyer/planner UI surfacing.
    /// </summary>
    public enum DemandAlertStatus
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Critical = 3,
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ProductionSupplyDemand — THE unified demand record
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The unified demand record. One row per supply requirement from a Production
    /// Order. Read by buyers, planners, MRP, Purchasing CC, and supply netting service.
    /// </summary>
    public class ProductionSupplyDemand
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── Demand number (UX) ────────────────────────

        [Required, StringLength(48)]
        [Display(Name = "Demand Number")]
        public string DemandNumber { get; set; } = string.Empty;

        // ──────────────────────── Hierarchy / identity ──────────────────────

        [Required]
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>FK to the BOM line (frozen snapshot) when demand is material-driven.</summary>
        public int? BomLineId { get; set; }
        public ProductionMaterialStructure? BomLine { get; set; }

        /// <summary>Routing operation sequence the demand pertains to (denormalized for fast filtering).</summary>
        public int? OperationSequence { get; set; }

        /// <summary>Project linkage (for project-driven jobs).</summary>
        public int? ProjectId { get; set; }
        public CipProject? Project { get; set; }

        /// <summary>WBS element (within project).</summary>
        [StringLength(64)]
        public string? WbsElement { get; set; }

        /// <summary>Customer this demand ultimately serves (for traceability).</summary>
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        /// <summary>Sales order driving this PRO. Stored as scalar until SalesOrder entity lands.</summary>
        public int? SalesOrderId { get; set; }
        [StringLength(48)]
        public string? SalesOrderNumber { get; set; }

        /// <summary>Parent demand — for child WO demands that roll up to a parent PRO demand.</summary>
        public int? ParentDemandId { get; set; }
        public ProductionSupplyDemand? ParentDemand { get; set; }

        // ──────────────────────── Item identity (frozen) ────────────────────

        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(120)]
        public string? PartNumber { get; set; }

        [StringLength(32)]
        public string? Revision { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(16)]
        public string? Uom { get; set; }

        // ──────────────────────── Quantity ──────────────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        public decimal RequiredQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ReservedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal SuppliedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ReceivedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal RemainingQuantity { get; set; }

        // ──────────────────────── Dates ─────────────────────────────────────

        [Display(Name = "Required Date")]
        public DateTime? RequiredDate { get; set; }

        [Display(Name = "Operation Start Date")]
        public DateTime? RequiredOperationStartDate { get; set; }

        [Display(Name = "Operation Completion Date")]
        public DateTime? RequiredOperationCompletionDate { get; set; }

        [Display(Name = "Need-By Date")]
        public DateTime? NeedByDate { get; set; }

        [Display(Name = "On-Dock Date")]
        public DateTime? OnDockDate { get; set; }

        // ──────────────────────── Source policy ─────────────────────────────

        public DemandSourceType SourceType { get; set; } = DemandSourceType.BomLine;

        public SupplyPolicy SupplyPolicy { get; set; } = SupplyPolicy.BuyDirectToJob;

        /// <summary>
        /// The supply rule identifier — references ItemSourcingRule when available.
        /// </summary>
        public int? SourcingRuleId { get; set; }

        /// <summary>Buyer assigned to resolve this demand (User FK).</summary>
        public int? BuyerUserId { get; set; }
        public User? BuyerUser { get; set; }

        /// <summary>Planner who created/owns this demand (User FK).</summary>
        public int? PlannerUserId { get; set; }
        public User? PlannerUser { get; set; }

        // ──────────────────────── Vendor / location ─────────────────────────

        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        [StringLength(64)]
        public string? VendorSiteCode { get; set; }

        public int? WorkCenterId { get; set; }
        public WorkCenter? WorkCenter { get; set; }

        [StringLength(64)]
        public string? VendorResource { get; set; }

        public int? WarehouseId { get; set; }
        public Abs.FixedAssets.Models.Masters.WarehouseMaster? Warehouse { get; set; }

        public int? BinLocationId { get; set; }
        public Location? BinLocation { get; set; }

        /// <summary>
        /// Vendor-side WIP location (for subcontract). Wired into VendorLocation in PR-5.
        /// Stored as string for now to avoid a forward-FK; the relationship comes in PR-5.
        /// </summary>
        [StringLength(120)]
        public string? VendorWipLocation { get; set; }

        // ──────────────────────── Compliance flags ──────────────────────────

        public bool InspectionRequired { get; set; }

        public bool CertRequired { get; set; }

        [StringLength(32)]
        [Display(Name = "Drawing/Spec Revision")]
        public string? DrawingSpecRevision { get; set; }

        public bool CustomerOwned { get; set; }

        public bool Consigned { get; set; }

        public bool ItarOrExportControlled { get; set; }

        // ──────────────────────── Status quartet ────────────────────────────

        public DemandSourceStatus SourceStatus { get; set; } = DemandSourceStatus.NotDetermined;

        public DemandSupplyStatus SupplyStatus { get; set; } = DemandSupplyStatus.NotSupplied;

        public DemandShortageStatus ShortageStatus { get; set; } = DemandShortageStatus.NoShortage;

        public DemandCostStatus CostStatus { get; set; } = DemandCostStatus.NotCommitted;

        public DemandAlertStatus AlertStatus { get; set; } = DemandAlertStatus.None;

        // ──────────────────────── Linked supply records ─────────────────────

        public int? LinkedPurchaseOrderId { get; set; }
        public PurchaseOrder? LinkedPurchaseOrder { get; set; }

        public int? LinkedPurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? LinkedPurchaseOrderLine { get; set; }

        /// <summary>FK to child production order (when MakeDirectToJob policy fires).</summary>
        public int? LinkedChildProductionOrderId { get; set; }
        public ProductionOrder? LinkedChildProductionOrder { get; set; }

        /// <summary>Inventory reservation Id (TransferOrder/ReservationId — string until those land).</summary>
        [StringLength(64)]
        public string? LinkedInventoryReservation { get; set; }

        /// <summary>Transfer order Id (string until TransferOrder entity ships).</summary>
        [StringLength(64)]
        public string? LinkedTransferOrder { get; set; }

        /// <summary>Subcontract shipment Id (string until SubcontractShipment ships in PR-6).</summary>
        [StringLength(64)]
        public string? LinkedSubcontractShipment { get; set; }

        public int? LinkedGoodsReceiptId { get; set; }
        public GoodsReceipt? LinkedGoodsReceipt { get; set; }

        public int? LinkedVendorInvoiceId { get; set; }
        public VendorInvoice? LinkedVendorInvoice { get; set; }

        // ──────────────────────── Tail ──────────────────────────────────────

        [StringLength(2000)]
        public string? Notes { get; set; }

        [Display(Name = "Last Refreshed")]
        public DateTime? LastRefreshedUtc { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        // Allocations — M:M between this demand and supply records
        public ICollection<ProductionSupplyAllocation> Allocations { get; set; }
            = new List<ProductionSupplyAllocation>();

        // xmin concurrency token — MapXminRowVersion in AppDbContext
        public byte[]? RowVersion { get; set; }
    }
}
