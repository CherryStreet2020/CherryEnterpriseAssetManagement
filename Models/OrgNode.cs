using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    [Table("org_node", Schema = "platform")]
    public class OrgNode
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("tenant_code")]
        [StringLength(50)]
        public string TenantCode { get; set; } = "default";

        [Required]
        [Column("node_type")]
        [StringLength(20)]
        public string NodeType { get; set; } = "location";

        [Required]
        [Column("name")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column("code")]
        [StringLength(50)]
        public string? Code { get; set; }

        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public OrgNode? Parent { get; set; }

        public ICollection<OrgNode> Children { get; set; } = new List<OrgNode>();

        [Column("company_id")]
        public int? CompanyId { get; set; }

        [Column("site_id")]
        public int? SiteId { get; set; }

        [Column("location_id")]
        public int? LocationId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
