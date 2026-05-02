using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("AuditLogs")]
public class AuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    [StringLength(100)]
    public string? Username { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

[Table("PeriodLocks")]
public class PeriodLock
{
    [Key]
    public int Id { get; set; }

    public int Period { get; set; }

    public DateTime LockedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? LockedBy { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public bool IsLocked { get; set; } = true;
}
