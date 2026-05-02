using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Revisions
{
    public class PMTemplateRevision
    {
        public int Id { get; set; }

        public int PMTemplateId { get; set; }
        public PMTemplate? PMTemplate { get; set; }

        [Required, StringLength(10)]
        [Display(Name = "Revision Code")]
        public string RevisionCode { get; set; } = "A";

        public RevisionStatus Status { get; set; } = RevisionStatus.Draft;

        [Display(Name = "Effective From")]
        public DateTime? EffectiveFromUtc { get; set; }

        [Display(Name = "Effective To")]
        public DateTime? EffectiveToUtc { get; set; }

        public int? SupersedesRevisionId { get; set; }
        [Display(Name = "Supersedes")]
        public PMTemplateRevision? SupersedesRevision { get; set; }

        [StringLength(500)]
        [Display(Name = "Change Reason")]
        public string? ChangeReason { get; set; }

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedByUserId { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Preventative;
        public PMPriority Priority { get; set; } = PMPriority.Medium;
        public PMTriggerType TriggerType { get; set; } = PMTriggerType.Calendar;
        public RecurrenceType CalendarInterval { get; set; } = RecurrenceType.Monthly;
        public int CalendarIntervalValue { get; set; } = 1;
        public MeterType? MeterType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MeterInterval { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal EstimatedHours { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedLaborCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedPartsCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedTotalCost { get; set; }

        public bool RequiresShutdown { get; set; } = false;
        public bool RequiresLOTO { get; set; } = false;

        [StringLength(50)]
        public string? SkillLevel { get; set; }

        [StringLength(50)]
        public string? Craft { get; set; }

        public string? Procedure { get; set; }
        public string? SafetyInstructions { get; set; }
        public string? ToolsRequired { get; set; }
        public string? ReferenceDocuments { get; set; }

        public int? AssetCategoryId { get; set; }
        public int? ManufacturerId { get; set; }

        [StringLength(100)]
        public string? ModelPattern { get; set; }

        public bool IsOEMRecommended { get; set; } = false;

        [StringLength(100)]
        public string? OEMReference { get; set; }

        public bool IsRegulatoryRequired { get; set; } = false;

        [StringLength(100)]
        public string? RegulatoryReference { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedByUserId { get; set; }

        public DateTime? ReleasedAtUtc { get; set; }
        public DateTime? ObsoletedAtUtc { get; set; }

        public ICollection<PMTemplateRevisionOperation>? Operations { get; set; }
    }

    public class PMTemplateRevisionOperation
    {
        public int Id { get; set; }

        public int PMTemplateRevisionId { get; set; }
        public PMTemplateRevision? PMTemplateRevision { get; set; }

        public int Sequence { get; set; } = 1;

        [Required, StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(8,2)")]
        [Display(Name = "Estimated Hours")]
        public decimal? EstimatedHours { get; set; }

        [StringLength(50)]
        public string? Craft { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsRequired { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
