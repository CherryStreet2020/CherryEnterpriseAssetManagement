using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public enum POStatus
    {
        Draft = 0,
        PendingApproval = 1,
        Approved = 2,
        Sent = 3,
        PartiallyReceived = 4,
        Received = 5,
        Invoiced = 6,
        Closed = 7,
        Cancelled = 8
    }

    public enum POType
    {
        Standard = 0,
        Blanket = 1,
        Emergency = 2,
        Contract = 3
    }

    public class PurchaseOrder
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "PO Number")]
        public string PONumber { get; set; } = string.Empty;

        public POType POType { get; set; } = POType.Standard;

        public int? POTypeLookupValueId { get; set; }
        public LookupValue? POTypeLookupValue { get; set; }

        public POStatus Status { get; set; } = POStatus.Draft;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        [Required]
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; } = null!;

        [Required]
        [Display(Name = "Order Date")]
        [DataType(DataType.Date)]
        public DateTime OrderDate { get; set; } = DateTime.Today;

        [Display(Name = "Required Date")]
        [DataType(DataType.Date)]
        public DateTime? RequiredDate { get; set; }

        [Display(Name = "Promise Date")]
        [DataType(DataType.Date)]
        public DateTime? PromiseDate { get; set; }

        [Display(Name = "Ship To Site")]
        public int? ShipToSiteId { get; set; }
        public Site? ShipToSite { get; set; }

        [Display(Name = "Ship To Location")]
        public int? DefaultShipToLocationId { get; set; }
        public Location? DefaultShipToLocation { get; set; }

        [StringLength(200)]
        [Display(Name = "Ship To Address")]
        public string? ShipToAddress { get; set; }

        [Display(Name = "Bill To Site")]
        public int? BillToSiteId { get; set; }
        public Site? BillToSite { get; set; }

        [StringLength(200)]
        [Display(Name = "Bill To Address")]
        public string? BillToAddress { get; set; }

        [Required, StringLength(3)]
        public string Currency { get; set; } = "USD";

        [Display(Name = "Subtotal")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Display(Name = "Tax Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Shipping")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingAmount { get; set; }

        [Display(Name = "Total")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(500)]
        [Display(Name = "Internal Notes")]
        public string? InternalNotes { get; set; }

        public int? WorkOrderId { get; set; }
        [Display(Name = "Work Order")]
        public WorkOrder? WorkOrder { get; set; }

        public int? CipProjectId { get; set; }
        [Display(Name = "CIP Project")]
        public CipProject? CipProject { get; set; }

        // ----- Theme B9 Wave 4 PR-10 — Customer-project pegging -----
        // A PO raised for a customer engagement pegs to that project (and,
        // optionally, a WBS phase) so committed spend rolls up to the project.
        // Nullable header FK + partial index (the dominant ERP pattern, and the
        // shipped ProductionOrder.CustomerProjectId precedent — NOT a link table).
        // The vast majority of POs are not project-linked, so the index is
        // filtered (WHERE CustomerProjectId IS NOT NULL) to stay cheap.
        public int? CustomerProjectId { get; set; }
        [Display(Name = "Customer Project")]
        public Abs.FixedAssets.Models.Projects.CustomerProject? CustomerProject { get; set; }

        public int? ProjectPhaseId { get; set; }
        [Display(Name = "Project Phase")]
        public Abs.FixedAssets.Models.Projects.ProjectPhase? ProjectPhase { get; set; }

        public int? RequestedById { get; set; }
        [Display(Name = "Requested By")]
        public User? RequestedBy { get; set; }

        public int? ApprovedById { get; set; }
        [Display(Name = "Approved By")]
        public User? ApprovedBy { get; set; }

        [Display(Name = "Approved Date")]
        public DateTime? ApprovedAt { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
        public ICollection<GoodsReceipt> Receipts { get; set; } = new List<GoodsReceipt>();

        // S1-8 / S2-8: optimistic concurrency via PG xmin. See Asset.RowVersion
        // and Data/XminRowVersionExtensions.cs. Mapped via fluent API in
        // AppDbContext.OnModelCreating; xmin is a system column so no DDL.
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    public class PurchaseOrderLine
    {
        public int Id { get; set; }

        public int PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        public int LineNumber { get; set; }

        // Non-Item Master support - allows ordering items not in Item Master
        [Display(Name = "Non-Item Master")]
        public bool IsNonItemMaster { get; set; } = false;

        // Item Master reference (nullable for non-item master parts)
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Part Number")]
        public string? PartNumber { get; set; }

        // Part number cross-referencing
        [StringLength(50)]
        [Display(Name = "Mfr Part #")]
        public string? ManufacturerPartNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Vendor Part #")]
        public string? VendorPartNumber { get; set; }

        [StringLength(10)]
        [Display(Name = "Rev")]
        public string? Revision { get; set; }

        // Category for non-item master parts (financial tracking)
        [Display(Name = "Expense Category")]
        public int? ExpenseCategoryId { get; set; }
        public ItemCategory? ExpenseCategory { get; set; }

        [StringLength(20)]
        [Display(Name = "Unit of Measure")]
        public string UOM { get; set; } = "EA";

        [Display(Name = "Quantity Ordered")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityOrdered { get; set; }

        [Display(Name = "Quantity Received")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReceived { get; set; }

        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Line Total")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        public int? GlAccountId { get; set; }
        [Display(Name = "GL Account")]
        public GlAccount? GlAccount { get; set; }

        public int? CostCenterId { get; set; }
        [Display(Name = "Cost Center")]
        public CostCenter? CostCenter { get; set; }

        public int? AssetId { get; set; }
        public Asset? Asset { get; set; }

        [Display(Name = "Ship To Location")]
        public int? ShipToLocationId { get; set; }
        public Location? ShipToLocation { get; set; }

        [Display(Name = "Required Date")]
        [DataType(DataType.Date)]
        public DateTime? RequiredDate { get; set; }

        public int? CipProjectId { get; set; }
        [Display(Name = "CIP Project")]
        public CipProject? CipProject { get; set; }

        public bool IsReceived { get; set; } = false;
        public bool IsClosed { get; set; } = false;

        [StringLength(500)]
        public string? Notes { get; set; }

        public ICollection<PurchaseOrderRelease> Releases { get; set; } = new List<PurchaseOrderRelease>();

        // ===== Sprint 15.1 PR-3 — PRO/demand linkage fields =================
        // For buy-to-job PO lines, these fields denormalize the linkage from
        // the (preferred) PurchaseOrderLineDemandLink rows so simple queries
        // don't have to join. When a PO line is consolidated across multiple
        // demands, these fields hold the PRIMARY/dominant assignment and the
        // full breakdown lives in the link table.

        /// <summary>
        /// Primary Production Order this PO line serves (null for stocking buys).
        /// For consolidated lines this is the dominant PRO; full breakdown via
        /// PurchaseOrderLineDemandLink rows.
        /// </summary>
        [Display(Name = "Production Order")]
        public int? ProductionOrderId { get; set; }
        public Abs.FixedAssets.Models.Production.ProductionOrder? ProductionOrder { get; set; }

        /// <summary>Primary BOM line this PO line satisfies.</summary>
        [Display(Name = "BOM Line")]
        public int? BomLineId { get; set; }
        public Abs.FixedAssets.Models.Production.ProductionMaterialStructure? BomLine { get; set; }

        /// <summary>Routing operation sequence this PO line serves (frozen).</summary>
        [Display(Name = "Operation Sequence")]
        public int? OperationSequence { get; set; }

        /// <summary>Project Id (when buying to a project).</summary>
        [Display(Name = "Project Id")]
        public int? ProjectId { get; set; }

        /// <summary>
        /// True when this PO line bypasses inventory and posts cost directly
        /// to the linked PRO BOM line at receipt. Mirrors the
        /// GoodsReceiptLine.IsDirectToJob flag at the PO side.
        /// </summary>
        [Display(Name = "Direct to Job")]
        public bool IsDirectToJob { get; set; } = false;

        /// <summary>
        /// True when this PO line is for a subcontract operation (the service
        /// purchase part of the two-demand subcontract pattern from §9).
        /// </summary>
        [Display(Name = "Subcontract Line")]
        public bool IsSubcontract { get; set; } = false;

        /// <summary>
        /// Demand link collection — many demands can be served by this PO line
        /// (consolidation) and one demand can be drawn from many PO lines.
        /// </summary>
        public ICollection<PurchaseOrderLineDemandLink> DemandLinks { get; set; }
            = new List<PurchaseOrderLineDemandLink>();
    }

    public enum ReleaseStatus
    {
        Open = 0,
        PartiallyReceived = 1,
        Received = 2,
        Cancelled = 3
    }

    public class PurchaseOrderRelease
    {
        public int Id { get; set; }

        public int PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        public int ReleaseNumber { get; set; }

        [Display(Name = "Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Display(Name = "Quantity Received")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReceived { get; set; }

        [Display(Name = "Ship To")]
        public int? ShipToLocationId { get; set; }
        public Location? ShipToLocation { get; set; }

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        public ReleaseStatus Status { get; set; } = ReleaseStatus.Open;

        [StringLength(200)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
