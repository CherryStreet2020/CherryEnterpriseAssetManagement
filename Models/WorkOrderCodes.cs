using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class WorkOrderType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public WorkOrderCategory Category { get; set; } = WorkOrderCategory.Corrective;

        public bool RequiresApproval { get; set; } = false;

        public decimal? ApprovalThreshold { get; set; }

        public int? DefaultPriorityId { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum WorkOrderCategory
    {
        Preventive = 0,
        Corrective = 1,
        Predictive = 2,
        Emergency = 3,
        Project = 4,
        Inspection = 5,
        Calibration = 6,
        Safety = 7
    }

    public class MaintenanceTypeCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public bool IsPreventive { get; set; } = false;

        public bool IsCorrective { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public class FailureCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public int? ParentId { get; set; }

        public FailureCode? Parent { get; set; }

        public ICollection<FailureCode>? Children { get; set; }

        public FailureCategory Category { get; set; } = FailureCategory.Mechanical;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum FailureCategory
    {
        Mechanical = 0,
        Electrical = 1,
        Hydraulic = 2,
        Pneumatic = 3,
        Electronic = 4,
        Software = 5,
        Structural = 6,
        Safety = 7,
        Environmental = 8,
        OperatorError = 9,
        Other = 99
    }

    public class CauseCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public int? ParentId { get; set; }

        public CauseCode? Parent { get; set; }

        public CauseCategory Category { get; set; } = CauseCategory.WearAndTear;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum CauseCategory
    {
        WearAndTear = 0,
        LackOfMaintenance = 1,
        DesignDefect = 2,
        ManufacturingDefect = 3,
        OperatorError = 4,
        EnvironmentalConditions = 5,
        Overloading = 6,
        Contamination = 7,
        Corrosion = 8,
        Fatigue = 9,
        Vibration = 10,
        Unknown = 99
    }

    public class ActionCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public ActionCategory Category { get; set; } = ActionCategory.Repair;

        public bool RequiresParts { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum ActionCategory
    {
        Repair = 0,
        Replace = 1,
        Adjust = 2,
        Clean = 3,
        Lubricate = 4,
        Inspect = 5,
        Test = 6,
        Calibrate = 7,
        Overhaul = 8,
        NoActionRequired = 9,
        DeferredToProject = 10
    }

    public class ProblemCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public ProblemCategory Category { get; set; } = ProblemCategory.Performance;

        public ProblemSeverity DefaultSeverity { get; set; } = ProblemSeverity.Medium;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum ProblemCategory
    {
        Performance = 0,
        Noise = 1,
        Vibration = 2,
        Leak = 3,
        Temperature = 4,
        Electrical = 5,
        Safety = 6,
        Quality = 7,
        Other = 99
    }

    public enum ProblemSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public class PriorityLevel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public int Level { get; set; } = 3;

        public int ResponseTimeHours { get; set; } = 24;

        public int TargetCompletionHours { get; set; } = 72;

        [StringLength(20)]
        public string Color { get; set; } = "#6b7280";

        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }
}
