using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

public enum AvlApprovalStatus
{
    Approved,
    Conditional,
    Blocked
}

public class ItemApprovedVendor
{
    public int Id { get; set; }
    
    [Required]
    public int TenantId { get; set; }
    [ForeignKey("TenantId")]
    public Tenant? Tenant { get; set; }
    
    public int? CompanyId { get; set; }
    [ForeignKey("CompanyId")]
    public Company? Company { get; set; }
    
    public int? SiteId { get; set; }
    [ForeignKey("SiteId")]
    public Site? Site { get; set; }
    
    [Required]
    public int ItemId { get; set; }
    [ForeignKey("ItemId")]
    public Item? Item { get; set; }
    
    [Required]
    public int VendorId { get; set; }
    [ForeignKey("VendorId")]
    public Vendor? Vendor { get; set; }
    
    public bool IsPreferred { get; set; }
    
    public AvlApprovalStatus ApprovalStatus { get; set; } = AvlApprovalStatus.Approved;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public int? CreatedByUserId { get; set; }
    [ForeignKey("CreatedByUserId")]
    public User? CreatedByUser { get; set; }
}
