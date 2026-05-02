using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("CapitalImprovements")]
public class CapitalImprovement
{
    [Key]
    public int Id { get; set; }

    public int AssetId { get; set; }

    public DateTime ImprovementDate { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    [StringLength(200)]
    public string? Vendor { get; set; }

    [StringLength(100)]
    public string? InvoiceNumber { get; set; }

    public int? UsefulLifeExtensionMonths { get; set; }

    public string? Notes { get; set; }

    public bool Capitalized { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [ForeignKey(nameof(AssetId))]
    public Asset? Asset { get; set; }
}
