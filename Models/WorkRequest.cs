using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

public enum WorkRequestStatus
{
    New = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3,
    ConvertedToWO = 4,
    Cancelled = 5
}

public enum WorkRequestPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
    Emergency = 4
}

[Table("WorkRequests")]
public class WorkRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Request Number")]
    public string RequestNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    [Display(Name = "Description")]
    public string RequestText { get; set; } = string.Empty;

    public WorkRequestStatus Status { get; set; } = WorkRequestStatus.New;

    public WorkRequestPriority Priority { get; set; } = WorkRequestPriority.Medium;

    public int? SiteId { get; set; }
    [Display(Name = "Site")]
    public Site? Site { get; set; }

    public int? LocationId { get; set; }
    [Display(Name = "Location")]
    public Location? Location { get; set; }

    public int? AssetId { get; set; }
    [Display(Name = "Asset")]
    public Asset? Asset { get; set; }

    [StringLength(100)]
    [Display(Name = "Requested By")]
    public string? RequestedBy { get; set; }

    [Display(Name = "Requested At")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    [Display(Name = "Contact Phone")]
    public string? ContactPhone { get; set; }

    [StringLength(100)]
    [Display(Name = "Contact Email")]
    public string? ContactEmail { get; set; }

    [StringLength(500)]
    [Display(Name = "Attachments")]
    public string? AttachmentPaths { get; set; }

    public int? GeneratedWorkOrderId { get; set; }
    [Display(Name = "Work Order")]
    public MaintenanceEvent? GeneratedWorkOrder { get; set; }

    [Display(Name = "AI Assisted")]
    public bool IsAIAssisted { get; set; } = false;

    [StringLength(50)]
    [Display(Name = "AI Confidence")]
    public string? AIConfidence { get; set; }

    [StringLength(2000)]
    [Display(Name = "AI Explanation")]
    public string? AIExplanation { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
}
