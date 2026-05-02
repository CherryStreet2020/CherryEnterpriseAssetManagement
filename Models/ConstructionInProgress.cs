using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class CipProject
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string ProjectNumber { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public CipProjectStatus Status { get; set; } = CipProjectStatus.Active;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EstimatedCompletionDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ActualCompletionDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BudgetAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCosts { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommittedCosts { get; set; }

        [StringLength(100)]
        public string? ProjectManagerName { get; set; }

        public int? ProjectManagerId { get; set; }
        public ProjectManager? ProjectManager { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        public int? CostCenterId { get; set; }
        public CostCenter? CostCenter { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        public int? DepartmentId { get; set; }
        public Department? DepartmentRef { get; set; }

        [StringLength(50)]
        public string? GlAccount { get; set; }

        public int? GlAccountId { get; set; }
        public GlAccount? GlAccountRef { get; set; }

        public int? ConvertedAssetId { get; set; }
        public Asset? ConvertedAsset { get; set; }

        [DataType(DataType.Date)]
        public DateTime? PlacedInServiceDate { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "CAD";

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public bool IsCapitalized { get; set; }
        public DateTime? CapitalizedAt { get; set; }

        [NotMapped]
        public bool IsLocked => IsCapitalized || Status == CipProjectStatus.Capitalized;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<CipCost>? Costs { get; set; }
        public ICollection<CipBudgetLine>? BudgetLines { get; set; }
        public ICollection<CipCapitalization>? Capitalizations { get; set; }
    }

    public class CipCost
    {
        public int Id { get; set; }

        public int CipProjectId { get; set; }
        public CipProject? Project { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        public CipCostType CostType { get; set; } = CipCostType.Construction;

        public int? CostTypeLookupValueId { get; set; }
        public LookupValue? CostTypeLookupValue { get; set; }

        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? Vendor { get; set; }

        [StringLength(50)]
        public string? InvoiceNumber { get; set; }

        [StringLength(50)]
        public string? PurchaseOrderNumber { get; set; }

        [StringLength(50)]
        public string? GlAccount { get; set; }

        public bool IsCapitalizable { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? EnteredBy { get; set; }

        [StringLength(50)]
        public string? SourceType { get; set; }

        public int? SourceHeaderId { get; set; }
        public int? SourceLineId { get; set; }

        [StringLength(100)]
        public string? SourceDisplayRef { get; set; }

        public int? WorkOrderId { get; set; }
        public MaintenanceEvent? WorkOrder { get; set; }

        public int? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrderRef { get; set; }

        public int? PurchaseOrderLineId { get; set; }
        public PurchaseOrderLine? PurchaseOrderLineRef { get; set; }

        public int? GoodsReceiptId { get; set; }
        public GoodsReceipt? GoodsReceiptRef { get; set; }

        public int? GoodsReceiptLineId { get; set; }
        public GoodsReceiptLine? GoodsReceiptLineRef { get; set; }

        public int? VendorInvoiceId { get; set; }
        public VendorInvoice? VendorInvoiceRef { get; set; }

        public int? VendorInvoiceLineId { get; set; }
        public VendorInvoiceLine? VendorInvoiceLineRef { get; set; }

        public int? JournalEntryId { get; set; }
        public JournalEntry? JournalEntryRef { get; set; }

        public int? VendorId { get; set; }
        public Vendor? VendorRef { get; set; }

        [StringLength(100)]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CipBudgetLine
    {
        public int Id { get; set; }

        public int CipProjectId { get; set; }
        public CipProject? Project { get; set; }

        public int CipCostTypeLookupValueId { get; set; }
        public LookupValue? CostTypeLookupValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BudgetAmount { get; set; }
    }

    public class CipCapitalization
    {
        public int Id { get; set; }

        public int CipProjectId { get; set; }
        public CipProject? Project { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public int? JournalEntryId { get; set; }
        public JournalEntry? JournalEntry { get; set; }

        public DateTime CapitalizedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CapitalizedByUserId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCapitalized { get; set; }

        public ICollection<CipCapitalizationCost>? CostMappings { get; set; }
    }

    public class CipCapitalizationCost
    {
        public int Id { get; set; }

        public int CipCapitalizationId { get; set; }
        public CipCapitalization? Capitalization { get; set; }

        public int CipCostId { get; set; }
        public CipCost? Cost { get; set; }
    }

    public enum CipProjectStatus
    {
        Planned = 0,
        Active = 1,
        OnHold = 2,
        Completed = 3,
        Cancelled = 4,
        Capitalized = 5
    }

    public enum CipCostType
    {
        Construction = 0,
        Engineering = 1,
        Equipment = 2,
        Labor = 3,
        Materials = 4,
        Freight = 5,
        Installation = 6,
        Testing = 7,
        Permits = 8,
        Professional = 9,
        InterestCapitalized = 10,
        Other = 11
    }
}
