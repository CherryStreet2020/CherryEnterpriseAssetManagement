using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

public enum InboundEventStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2,
    DeadLetter = 3
}

[Table("InboundEvents")]
public class InboundEvent
{
    [Key]
    public int Id { get; set; }

    public int? TenantId { get; set; }

    public int IntegrationEndpointId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ExternalEntityId { get; set; }

    [StringLength(100)]
    public string? CorrelationId { get; set; }

    [StringLength(100)]
    public string? IdempotencyKey { get; set; }

    [Required]
    public string RawBodyJson { get; set; } = "{}";

    public string? HeadersJson { get; set; }

    public InboundEventStatus Status { get; set; } = InboundEventStatus.Pending;

    public int AttemptCount { get; set; } = 0;

    public DateTime? NextAttemptAt { get; set; }

    [StringLength(1000)]
    public string? LastError { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    [ForeignKey(nameof(IntegrationEndpointId))]
    public IntegrationEndpoint? IntegrationEndpoint { get; set; }
}
