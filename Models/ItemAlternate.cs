using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

public enum AlternateType
{
    Substitute,
    Equivalent,
    Upgrade,
    Downgrade
}

public class ItemAlternate
{
    public int Id { get; set; }
    
    [Required]
    public int TenantId { get; set; }
    [ForeignKey("TenantId")]
    public Tenant? Tenant { get; set; }
    
    [Required]
    public int ItemId { get; set; }
    [ForeignKey("ItemId")]
    public Item? Item { get; set; }
    
    [Required]
    public int AlternateItemId { get; set; }
    [ForeignKey("AlternateItemId")]
    public Item? AlternateItem { get; set; }
    
    public AlternateType AlternateType { get; set; } = AlternateType.Substitute;
    
    public int Rank { get; set; } = 1;
    
    [MaxLength(500)]
    public string? Reason { get; set; }
    
    public bool IsApproved { get; set; } = true;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public int? CreatedByUserId { get; set; }
    [ForeignKey("CreatedByUserId")]
    public User? CreatedByUser { get; set; }
}
