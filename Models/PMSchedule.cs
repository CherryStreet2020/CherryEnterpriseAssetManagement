using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Models
{
    public enum PMCadenceType
    {
        IntervalDays = 0,
        Weekly = 1,
        Monthly = 2
    }

    public enum PMOccurrenceStatus
    {
        Created = 0,
        Skipped = 1,
        Error = 2,
        // S1-2: marker for occurrences whose WO has been closed.
        // The scheduler reads this to know which occurrences have been
        // fulfilled (vs. still open / overdue).
        Completed = 3
    }

    [Index(nameof(TenantId), nameof(CompanyId), nameof(SiteId), nameof(PMTemplateId), nameof(Active))]
    public class PMSchedule
    {
        public int Id { get; set; }

        public int? TenantId { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        [Required]
        public int PMTemplateId { get; set; }
        [Display(Name = "PM Template")]
        public PMTemplate? PMTemplate { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Schedule Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool Active { get; set; } = true;

        [Required]
        [Display(Name = "Start Date (UTC)")]
        public DateTime StartDateUtc { get; set; } = DateTime.UtcNow.Date;

        [StringLength(100)]
        [Display(Name = "Time Zone")]
        public string? TimeZoneId { get; set; }

        [Display(Name = "Cadence Type")]
        public PMCadenceType CadenceType { get; set; } = PMCadenceType.IntervalDays;

        [Display(Name = "Interval (Days)")]
        public int? IntervalDays { get; set; } = 30;

        [Display(Name = "Days of Week")]
        public int? DaysOfWeekMask { get; set; }

        [Display(Name = "Day of Month")]
        public int? DayOfMonth { get; set; }

        [Display(Name = "Next Due Date (UTC)")]
        public DateTime? NextDueDateUtc { get; set; }

        [Display(Name = "Lead Days")]
        public int LeadDays { get; set; } = 0;

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public ICollection<PMOccurrence>? Occurrences { get; set; }
    }

    [Index(nameof(TenantId), nameof(CompanyId), nameof(SiteId), nameof(PMTemplateId), nameof(DueDateUtc), IsUnique = true)]
    public class PMOccurrence
    {
        public int Id { get; set; }

        public int? TenantId { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        [Required]
        public int PMScheduleId { get; set; }
        [Display(Name = "PM Schedule")]
        public PMSchedule? PMSchedule { get; set; }

        [Required]
        public int PMTemplateId { get; set; }
        [Display(Name = "PM Template")]
        public PMTemplate? PMTemplate { get; set; }

        [Required]
        [Display(Name = "Due Date (UTC)")]
        public DateTime DueDateUtc { get; set; }

        public int? WorkOrderId { get; set; }
        [Display(Name = "Work Order")]
        public MaintenanceEvent? WorkOrder { get; set; }

        public PMOccurrenceStatus Status { get; set; } = PMOccurrenceStatus.Created;

        [StringLength(500)]
        [Display(Name = "Error Message")]
        public string? ErrorMessage { get; set; }

        [StringLength(50)]
        public string? GeneratedBy { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
