using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("WebhookDeliveryLogs")]
public class WebhookDeliveryLog
{
    [Key]
    public int Id { get; set; }

    public int WebhookSubscriptionId { get; set; }

    public int OutboxEventId { get; set; }

    public int AttemptNumber { get; set; }

    public int? ResponseStatusCode { get; set; }

    public int DurationMs { get; set; }

    [StringLength(1000)]
    public string? Error { get; set; }

    public string? PayloadSent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(WebhookSubscriptionId))]
    public WebhookSubscription? WebhookSubscription { get; set; }

    [ForeignKey(nameof(OutboxEventId))]
    public OutboxEvent? OutboxEvent { get; set; }
}
