using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

public class LookupType
{
    public int Id { get; set; }

    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsSystem { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LookupValue> Values { get; set; } = new List<LookupValue>();
}
