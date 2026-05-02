using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("WebhookSubscriptions")]
public class WebhookSubscription
{
    [Key]
    public int Id { get; set; }

    public int CompanyId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Url { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string EventTypesCsv { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Secret { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? LastDeliveryAt { get; set; }

    public int ConsecutiveFailures { get; set; } = 0;

    public int SuccessCountLifetime { get; set; } = 0;

    public int FailureCountLifetime { get; set; } = 0;

    public int MaxConsecutiveFailures { get; set; } = 25;

    public DateTime? DisabledAt { get; set; }

    [StringLength(500)]
    public string? DisabledReason { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    public ICollection<WebhookDeliveryLog>? DeliveryLogs { get; set; }

    public double GetSuccessRate()
    {
        var total = SuccessCountLifetime + FailureCountLifetime;
        return total > 0 ? (double)SuccessCountLifetime / total * 100 : 100;
    }

    public string[] GetEventTypes()
    {
        if (string.IsNullOrWhiteSpace(EventTypesCsv))
            return Array.Empty<string>();
        return EventTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void SetEventTypes(IEnumerable<string> eventTypes)
    {
        EventTypesCsv = string.Join(",", eventTypes);
    }

    public bool SubscribesToEvent(string eventType)
    {
        var types = GetEventTypes();
        return types.Length == 0 || types.Contains(eventType, StringComparer.OrdinalIgnoreCase) || types.Contains("*");
    }
}
