using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public enum RequisitionStatus
    {
        Draft = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Converted = 4,
        Cancelled = 5,
        ExportedToERP = 6
    }

    public enum RequisitionPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3,
        Emergency = 4
    }

    public enum RequisitionSource
    {
        Manual = 0,
        AutoReorder = 1,
        WorkOrder = 2,
        PMSchedule = 3,
        StockoutAlert = 4,
        SafetyStock = 5
    }

    public class PurchaseRequisition
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Requisition #")]
        public string RequisitionNumber { get; set; } = string.Empty;

        public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        public RequisitionPriority Priority { get; set; } = RequisitionPriority.Normal;
        public int? PriorityLookupValueId { get; set; }
        public LookupValue? PriorityLookupValue { get; set; }

        public RequisitionSource Source { get; set; } = RequisitionSource.Manual;

        [Display(Name = "Requisition Date")]
        public DateTime RequisitionDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Required Date")]
        public DateTime? RequiredDate { get; set; }

        [StringLength(100)]
        [Display(Name = "Requestor")]
        public string? Requestor { get; set; }

        public int? RequestorId { get; set; }

        [StringLength(100)]
        [Display(Name = "Department")]
        public string? Department { get; set; }

        public int? DepartmentId { get; set; }

        [StringLength(100)]
        [Display(Name = "Buyer")]
        public string? Buyer { get; set; }

        public int? BuyerId { get; set; }

        public int? SuggestedVendorId { get; set; }
        [Display(Name = "Suggested Vendor")]
        public Vendor? SuggestedVendor { get; set; }

        [Display(Name = "Total Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(500)]
        [Display(Name = "Justification")]
        public string? Justification { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Deliver To Site")]
        public int? DeliverToSiteId { get; set; }
        public Site? DeliverToSite { get; set; }

        [Display(Name = "Deliver To Location")]
        public int? DeliverToLocationId { get; set; }
        public Location? DeliverToLocation { get; set; }

        [StringLength(100)]
        [Display(Name = "Deliver To")]
        public string? DeliverTo { get; set; }

        [StringLength(200)]
        [Display(Name = "Delivery Address")]
        public string? DeliveryAddress { get; set; }

        [StringLength(50)]
        [Display(Name = "Work Order Ref")]
        public string? WorkOrderReference { get; set; }

        public int? WorkOrderId { get; set; }

        [StringLength(50)]
        [Display(Name = "PM Schedule Ref")]
        public string? PMScheduleReference { get; set; }

        [Display(Name = "Approved By")]
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved Date")]
        public DateTime? ApprovedDate { get; set; }

        [StringLength(500)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        [Display(Name = "Converted to PO")]
        public int? ConvertedToPOId { get; set; }
        public PurchaseOrder? ConvertedToPO { get; set; }

        [Display(Name = "Converted Date")]
        public DateTime? ConvertedDate { get; set; }

        [Display(Name = "Exported to ERP")]
        public bool ExportedToERP { get; set; } = false;

        [Display(Name = "ERP Export Date")]
        public DateTime? ERPExportDate { get; set; }

        [StringLength(50)]
        [Display(Name = "ERP Reference")]
        public string? ERPReference { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        public ICollection<PurchaseRequisitionLine>? Lines { get; set; }
    }

    public class PurchaseRequisitionLine
    {
        public int Id { get; set; }

        public int RequisitionId { get; set; }
        public PurchaseRequisition? Requisition { get; set; }

        public int LineNumber { get; set; }

        // Non-Item Master support - allows ordering items not in Item Master
        [Display(Name = "Non-Item Master")]
        public bool IsNonItemMaster { get; set; } = false;

        // Item Master reference (nullable for non-item master parts)
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(50)]
        [Display(Name = "Part Number")]
        public string? PartNumber { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

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

        [Display(Name = "Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [StringLength(20)]
        [Display(Name = "UOM")]
        public string UOM { get; set; } = "EA";

        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Extended Price")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtendedPrice => Quantity * UnitPrice;

        public int? SuggestedVendorId { get; set; }
        [Display(Name = "Suggested Vendor")]
        public Vendor? SuggestedVendor { get; set; }

        [Display(Name = "Current Stock")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? CurrentStock { get; set; }

        [Display(Name = "Reorder Point")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ReorderPoint { get; set; }

        [Display(Name = "Required Date")]
        public DateTime? RequiredDate { get; set; }

        [StringLength(100)]
        [Display(Name = "Deliver To")]
        public string? DeliverTo { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }

        [Display(Name = "GL Account")]
        public int? GlAccountId { get; set; }
        public GlAccount? GlAccount { get; set; }

        [Display(Name = "Cost Center")]
        public int? CostCenterId { get; set; }
        public CostCenter? CostCenter { get; set; }

        public int? CipProjectId { get; set; }
        [Display(Name = "CIP Project")]
        public CipProject? CipProject { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ReorderAlert
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Display(Name = "Alert Type")]
        public RequisitionSource AlertType { get; set; }

        [Display(Name = "Current Stock")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal CurrentStock { get; set; }

        [Display(Name = "Reorder Point")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReorderPoint { get; set; }

        [Display(Name = "Safety Stock")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal SafetyStock { get; set; }

        [Display(Name = "Suggested Qty")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal SuggestedQuantity { get; set; }

        [Display(Name = "Is Acknowledged")]
        public bool IsAcknowledged { get; set; } = false;

        [Display(Name = "Acknowledged By")]
        [StringLength(100)]
        public string? AcknowledgedBy { get; set; }

        [Display(Name = "Acknowledged Date")]
        public DateTime? AcknowledgedDate { get; set; }

        [Display(Name = "Requisition Created")]
        public int? RequisitionId { get; set; }
        public PurchaseRequisition? Requisition { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
