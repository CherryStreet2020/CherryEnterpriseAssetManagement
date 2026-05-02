using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public enum PMTriggerType
    {
        Calendar = 0,
        Meter = 1,
        CalendarOrMeter = 2,
        Manual = 3
    }

    public enum MeterType
    {
        Hours = 0,
        Miles = 1,
        Kilometers = 2,
        Cycles = 3,
        Units = 4,
        Gallons = 5,
        Liters = 6,
        Custom = 7
    }

    public enum PMPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public class PMTemplate
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Template Code")]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Preventative;

        public PMPriority Priority { get; set; } = PMPriority.Medium;

        public PMTriggerType TriggerType { get; set; } = PMTriggerType.Calendar;

        public RecurrenceType CalendarInterval { get; set; } = RecurrenceType.Monthly;

        [Display(Name = "Calendar Interval Value")]
        public int CalendarIntervalValue { get; set; } = 1;

        public MeterType? MeterType { get; set; }

        [Display(Name = "Meter Interval")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MeterInterval { get; set; }

        [Display(Name = "Estimated Duration (Hours)")]
        [Column(TypeName = "decimal(8,2)")]
        public decimal EstimatedHours { get; set; } = 1;

        [Display(Name = "Estimated Labor Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedLaborCost { get; set; }

        [Display(Name = "Estimated Parts Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedPartsCost { get; set; }

        [Display(Name = "Estimated Total Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedTotalCost { get; set; }

        [Display(Name = "Requires Shutdown")]
        public bool RequiresShutdown { get; set; } = false;

        [Display(Name = "Requires Lockout/Tagout")]
        public bool RequiresLOTO { get; set; } = false;

        [Display(Name = "Requires Confined Space Entry")]
        public bool RequiresConfinedSpaceEntry { get; set; } = false;

        [Display(Name = "Skill Level Required")]
        [StringLength(50)]
        public string? SkillLevel { get; set; }

        [Display(Name = "Craft/Trade")]
        [StringLength(50)]
        public string? Craft { get; set; }

        [Display(Name = "Procedure")]
        public string? Procedure { get; set; }

        [Display(Name = "Safety Instructions")]
        public string? SafetyInstructions { get; set; }

        [Display(Name = "Tools Required")]
        public string? ToolsRequired { get; set; }

        [Display(Name = "Reference Documents")]
        public string? ReferenceDocuments { get; set; }

        public int? AssetCategoryId { get; set; }
        [Display(Name = "Asset Category")]
        public AssetCategory? AssetCategory { get; set; }

        public int? ManufacturerId { get; set; }
        [Display(Name = "Manufacturer")]
        public Manufacturer? Manufacturer { get; set; }

        [StringLength(100)]
        [Display(Name = "Model Pattern")]
        public string? ModelPattern { get; set; }

        [Display(Name = "OEM Recommended")]
        public bool IsOEMRecommended { get; set; } = false;

        [StringLength(100)]
        [Display(Name = "OEM Reference")]
        public string? OEMReference { get; set; }

        [Display(Name = "Regulatory Required")]
        public bool IsRegulatoryRequired { get; set; } = false;

        [StringLength(100)]
        [Display(Name = "Regulatory Reference")]
        public string? RegulatoryReference { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        public int? CurrentReleasedRevisionId { get; set; }
        [Display(Name = "Current Revision")]
        public Revisions.PMTemplateRevision? CurrentReleasedRevision { get; set; }

        public ICollection<PMTemplateItem>? Items { get; set; }
        public ICollection<PMTemplateAsset>? Assets { get; set; }
        public ICollection<Revisions.PMTemplateRevision>? Revisions { get; set; }
    }

    public class PMTemplateItem
    {
        public int Id { get; set; }

        public int PMTemplateId { get; set; }
        public PMTemplate? PMTemplate { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Display(Name = "Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; } = 1;

        [Display(Name = "Is Required")]
        public bool IsRequired { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        public int Sequence { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class PMTemplateAsset
    {
        public int Id { get; set; }

        public int PMTemplateId { get; set; }
        public PMTemplate? PMTemplate { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [Display(Name = "Override Calendar Interval")]
        public RecurrenceType? OverrideCalendarInterval { get; set; }

        [Display(Name = "Override Calendar Value")]
        public int? OverrideCalendarValue { get; set; }

        [Display(Name = "Override Meter Interval")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? OverrideMeterInterval { get; set; }

        [Display(Name = "Last Completed Date")]
        public DateTime? LastCompletedDate { get; set; }

        [Display(Name = "Last Meter Reading")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? LastMeterReading { get; set; }

        [Display(Name = "Next Due Date")]
        public DateTime? NextDueDate { get; set; }

        [Display(Name = "Next Due Meter")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? NextDueMeter { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class MeterReading
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public MeterType MeterType { get; set; } = MeterType.Hours;

        [StringLength(50)]
        [Display(Name = "Meter Name")]
        public string? MeterName { get; set; }

        [Display(Name = "Reading")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Reading { get; set; }

        [Display(Name = "Previous Reading")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PreviousReading { get; set; }

        [Display(Name = "Delta")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Delta => PreviousReading.HasValue ? Reading - PreviousReading.Value : null;

        [Display(Name = "Reading Date")]
        public DateTime ReadingDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        [Display(Name = "Recorded By")]
        public string? RecordedBy { get; set; }

        [StringLength(50)]
        [Display(Name = "Source")]
        public string? Source { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsEstimated { get; set; } = false;

        public bool IsRollover { get; set; } = false;

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Kit
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Kit Number")]
        public string KitNumber { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int? CategoryId { get; set; }
        [Display(Name = "Item Category")]
        public ItemCategory? Category { get; set; }

        [Display(Name = "Total Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        public bool IsActive { get; set; } = true;

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<KitItem>? Items { get; set; }
    }

    public class KitItem
    {
        public int Id { get; set; }

        public int KitId { get; set; }
        public Kit? Kit { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Display(Name = "Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; } = 1;

        [StringLength(200)]
        public string? Notes { get; set; }

        public int Sequence { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkOrderPart
    {
        public int Id { get; set; }

        public int MaintenanceEventId { get; set; }
        public MaintenanceEvent? MaintenanceEvent { get; set; }

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

        public DateTime? IssuedDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
