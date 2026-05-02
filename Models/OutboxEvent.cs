using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

public enum OutboxEventStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    DeadLetter = 3
}

[Table("OutboxEvents")]
public class OutboxEvent
{
    [Key]
    public int Id { get; set; }

    public int? TenantId { get; set; }

    public int CompanyId { get; set; }

    public int? SiteId { get; set; }

    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    public string PayloadJson { get; set; } = "{}";

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public OutboxEventStatus Status { get; set; } = OutboxEventStatus.Pending;

    public int AttemptCount { get; set; } = 0;

    public DateTime? NextAttemptAt { get; set; }

    [StringLength(1000)]
    public string? LastError { get; set; }

    public DateTime? SentAt { get; set; }

    [StringLength(100)]
    public string? CorrelationId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    [ForeignKey(nameof(SiteId))]
    public Site? Site { get; set; }
}
