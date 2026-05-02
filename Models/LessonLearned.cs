using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models;

public class LessonLearned
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public int? SiteId { get; set; }
    public Site? Site { get; set; }

    public int? AssetCategoryId { get; set; }
    public AssetCategory? AssetCategory { get; set; }

    [StringLength(500)]
    public string? Tags { get; set; }

    [Required, StringLength(4000)]
    public string Text { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Title { get; set; }

    public int? SourceWorkOrderId { get; set; }
    public MaintenanceEvent? SourceWorkOrder { get; set; }

    [StringLength(100)]
    public string? FailureCode { get; set; }

    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
