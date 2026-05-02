using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

public class ItemSupersession
{
    public int Id { get; set; }
    
    [Required]
    public int TenantId { get; set; }
    [ForeignKey("TenantId")]
    public Tenant? Tenant { get; set; }
    
    [Required]
    public int OldItemId { get; set; }
    [ForeignKey("OldItemId")]
    public Item? OldItem { get; set; }
    
    [Required]
    public int NewItemId { get; set; }
    [ForeignKey("NewItemId")]
    public Item? NewItem { get; set; }
    
    public DateTime? EffectiveFromUtc { get; set; }
    
    [MaxLength(500)]
    public string? Reason { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public int? CreatedByUserId { get; set; }
    [ForeignKey("CreatedByUserId")]
    public User? CreatedByUser { get; set; }
}
