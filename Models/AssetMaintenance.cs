using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class MaintenanceEvent
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Preventative;

        public int? TypeLookupValueId { get; set; }
        public LookupValue? TypeLookupValue { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime ScheduledDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        public int? PriorityLookupValueId { get; set; }
        public LookupValue? PriorityLookupValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LaborCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PartsCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaterialsCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OutsideVendorCost { get; set; }

        [StringLength(100)]
        public string? Vendor { get; set; }

        [StringLength(100)]
        public string? TechnicianName { get; set; }

        public int? TechnicianId { get; set; }
        public Technician? Technician { get; set; }

        [StringLength(50)]
        public string? WorkOrderNumber { get; set; }

        [StringLength(50)]
        public string? PurchaseOrderNumber { get; set; }

        public decimal? DowntimeHours { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LaborHours { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OvertimeHours { get; set; }

        public WorkOrderApprovalStatus ApprovalStatus { get; set; } = WorkOrderApprovalStatus.NotRequired;

        public int? CipProjectId { get; set; }
        public CipProject? CipProject { get; set; }

        // S1-2: explicit FK linkage to the PM occurrence and template-asset
        // assignment that generated this WO. Replaces the brittle
        // CustomField1 = "PMTA:N" string hack that conflated PMOccurrence.Id
        // with PMTemplateAsset.Id (different tables, different namespaces) —
        // see docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md.
        public int? PMOccurrenceId { get; set; }
        public PMOccurrence? PMOccurrence { get; set; }

        public int? PMTemplateAssetId { get; set; }
        public PMTemplateAsset? PMTemplateAsset { get; set; }

        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public int? RequestedById { get; set; }
        public User? RequestedBy { get; set; }

        public DateTime? RequestedAt { get; set; }

        [StringLength(500)]
        public string? FailureCode { get; set; }

        [StringLength(500)]
        public string? RootCause { get; set; }

        [StringLength(500)]
        public string? CorrectiveAction { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(1000)]
        public string? Resolution { get; set; }

        public int? RecurrenceIntervalDays { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NextScheduledDate { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CompletedBy { get; set; }

        [StringLength(2000)]
        public string? ResolutionSummary { get; set; }

        [StringLength(2000)]
        public string? LessonsLearned { get; set; }

        public DateTime? ClosedAt { get; set; }

        [StringLength(100)]
        public string? ClosedBy { get; set; }

        public DateTime? StartedAt { get; set; }

        [StringLength(100)]
        public string? StartedBy { get; set; }

        [StringLength(500)]
        public string? HoldReason { get; set; }

        public string? CustomField1 { get; set; }
        public string? CustomField2 { get; set; }
        public string? CustomField3 { get; set; }
        public string? CustomField4 { get; set; }
        public string? CustomField5 { get; set; }
        public string? CustomField6 { get; set; }
        public string? CustomField7 { get; set; }
        public string? CustomField8 { get; set; }
        public string? CustomField9 { get; set; }
        public string? CustomField10 { get; set; }

        public ICollection<WorkOrderOperation>? Operations { get; set; }
    }

    public class MaintenanceSchedule
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Preventative;

        public RecurrenceType Recurrence { get; set; } = RecurrenceType.Monthly;

        public int IntervalValue { get; set; } = 1;

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? LastGeneratedDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NextDueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [StringLength(100)]
        public string? AssignedVendor { get; set; }

        public bool IsActive { get; set; } = true;

        public int LeadTimeDays { get; set; } = 7;
    }

    public enum MaintenanceType
    {
        Preventative = 0,
        Corrective = 1,
        Predictive = 2,
        Emergency = 3,
        Inspection = 4,
        Calibration = 5,
        Upgrade = 6,
        Other = 7
    }

    public enum MaintenanceStatus
    {
        Scheduled = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3,
        Overdue = 4,
        OnHold = 5
    }

    public enum MaintenancePriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum RecurrenceType
    {
        Daily = 0,
        Weekly = 1,
        BiWeekly = 2,
        Monthly = 3,
        Quarterly = 4,
        SemiAnnually = 5,
        Annually = 6,
        Custom = 7
    }

    public enum WorkOrderApprovalStatus
    {
        NotRequired = 0,
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }
}
