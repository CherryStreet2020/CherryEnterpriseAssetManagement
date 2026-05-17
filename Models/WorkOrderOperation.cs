using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public enum OperationStatus
    {
        Pending = 0,
        Ready = 1,
        InProgress = 2,
        OnHold = 3,
        Completed = 4,
        Cancelled = 5
    }

    public enum OperationType
    {
        Mechanical = 0,
        Electrical = 1,
        Hydraulic = 2,
        Pneumatic = 3,
        Lubrication = 4,
        Inspection = 5,
        Calibration = 6,
        Cleaning = 7,
        Adjustment = 8,
        Replacement = 9,
        Testing = 10,
        Documentation = 11,
        SafetyCheck = 12,
        Other = 99
    }

    public class WorkOrderOperation
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Operation #")]
        public string OperationNumber { get; set; } = string.Empty;

        public int Sequence { get; set; } = 10;

        public OperationType Type { get; set; } = OperationType.Mechanical;
        public int? TypeLookupValueId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(2000)]
        public string? Instructions { get; set; }

        public OperationStatus Status { get; set; } = OperationStatus.Pending;
        public int? StatusLookupValueId { get; set; }

        public int? AssignedTechnicianId { get; set; }
        public Technician? AssignedTechnician { get; set; }

        public int? CraftId { get; set; }
        public Craft? Craft { get; set; }

        [Display(Name = "Planned Hours")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal PlannedHours { get; set; } = 0;

        [Display(Name = "Actual Hours")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ActualHours { get; set; } = 0;

        [Display(Name = "Planned Start Date")]
        [DataType(DataType.Date)]
        public DateTime? PlannedStartDate { get; set; }

        [Display(Name = "Planned End Date")]
        [DataType(DataType.Date)]
        public DateTime? PlannedEndDate { get; set; }

        [Display(Name = "Actual Start Date")]
        [DataType(DataType.DateTime)]
        public DateTime? ActualStartDate { get; set; }

        [Display(Name = "Actual End Date")]
        [DataType(DataType.DateTime)]
        public DateTime? ActualEndDate { get; set; }

        [Display(Name = "Requires Shutdown")]
        public bool RequiresShutdown { get; set; } = false;

        [Display(Name = "Requires LOTO")]
        public bool RequiresLOTO { get; set; } = false;

        [StringLength(100)]
        [Display(Name = "LOTO Procedure ID")]
        public string? LOTOProcedureId { get; set; }

        [Display(Name = "Confined Space Entry")]
        public bool RequiresConfinedSpaceEntry { get; set; } = false;

        [Display(Name = "Hot Work Permit")]
        public bool RequiresHotWorkPermit { get; set; } = false;

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? CompletedBy { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public ICollection<WorkOrderOperationLabor>? LaborEntries { get; set; }
        public ICollection<WorkOrderOperationTool>? Tools { get; set; }
        public ICollection<WorkOrderOperationPart>? Parts { get; set; }
    }

    public class WorkOrderOperationLabor
    {
        public int Id { get; set; }

        public int WorkOrderOperationId { get; set; }
        public WorkOrderOperation? WorkOrderOperation { get; set; }

        public int? TechnicianId { get; set; }
        public Technician? Technician { get; set; }

        public int? CraftId { get; set; }
        public Craft? Craft { get; set; }

        public int? LaborTypeId { get; set; }
        public LaborType? LaborType { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Work Date")]
        public DateTime WorkDate { get; set; } = DateTime.UtcNow.Date;

        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan? StartTime { get; set; }

        [Display(Name = "End Time")]
        [DataType(DataType.Time)]
        public TimeSpan? EndTime { get; set; }

        [Display(Name = "Hours")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Hours { get; set; } = 0;

        [Display(Name = "Hourly Rate")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; } = 0;

        [Display(Name = "Total Cost")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalCost => Hours * HourlyRate;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }

    public class WorkOrderOperationTool
    {
        public int Id { get; set; }

        public int WorkOrderOperationId { get; set; }
        public WorkOrderOperation? WorkOrderOperation { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tool Name")]
        public string ToolName { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Tool ID/Asset Tag")]
        public string? ToolAssetTag { get; set; }

        public int? ToolAssetId { get; set; }
        public Asset? ToolAsset { get; set; }

        [Display(Name = "Quantity Required")]
        public int QuantityRequired { get; set; } = 1;

        [Display(Name = "Quantity Used")]
        public int QuantityUsed { get; set; } = 0;

        [Display(Name = "Checked Out")]
        public bool IsCheckedOut { get; set; } = false;

        [Display(Name = "Checked Out At")]
        public DateTime? CheckedOutAt { get; set; }

        [Display(Name = "Returned At")]
        public DateTime? ReturnedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Checked Out By")]
        public string? CheckedOutBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkOrderOperationPart
    {
        public int Id { get; set; }

        public int WorkOrderOperationId { get; set; }
        public WorkOrderOperation? WorkOrderOperation { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Display(Name = "Quantity Planned")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityPlanned { get; set; } = 0;

        [Display(Name = "Quantity Issued")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityIssued { get; set; } = 0;

        [Display(Name = "Quantity Used")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityUsed { get; set; } = 0;

        [Display(Name = "Quantity Returned")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReturned { get; set; } = 0;

        [Display(Name = "Unit Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitCost { get; set; }

        [Display(Name = "Total Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost => QuantityUsed * UnitCost;

        public int? IssuedFromLocationId { get; set; }
        [Display(Name = "Issued From")]
        public Location? IssuedFromLocation { get; set; }

        [StringLength(50)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(50)]
        [Display(Name = "Issued By")]
        public string? IssuedBy { get; set; }

        public DateTime? IssuedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
