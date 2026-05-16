// PR #101: ApiKey is tenant-scoped. Every key binds to the issuing tenant
// (TenantId) and may further narrow to a single company (CompanyId, optional).
// AssetsApiController consults these on every request to scope queries — see
// RequireApiKeyWithTenantScope(). Pre-#101 keys (TenantId == 0) are rejected
// at validation; admins must re-issue them. The previous design comment here
// claimed ApiKey was a "system-level entity"; that was the cross-tenant leak.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("ApiKeys")]
public class ApiKey
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    [StringLength(10)]
    public string KeyPrefix { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? Scopes { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    // Tenant binding. 0 = sentinel meaning "issued before PR #101 tenant
    // scoping was enforced; refuse this key and require re-issue." Any
    // newly minted key carries the issuing admin's TenantId.
    public int TenantId { get; set; }

    // Optional company narrowing. Null = the key is allowed to read every
    // company visible to its tenant. A specific CompanyId restricts the key
    // to that single company.
    public int? CompanyId { get; set; }
}
