using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("IntegrationEndpoints")]
public class IntegrationEndpoint
{
    [Key]
    public int Id { get; set; }

    public int? TenantId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string IntegrationKey { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Secret { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string AllowedEventTypesCsv { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? LastEventAt { get; set; }

    public int EventsReceivedCount { get; set; } = 0;

    public int EventsProcessedCount { get; set; } = 0;

    public int EventsFailedCount { get; set; } = 0;

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public ICollection<InboundEvent>? InboundEvents { get; set; }

    public ICollection<IntegrationMapping>? Mappings { get; set; }

    public string[] GetAllowedEventTypes()
    {
        if (string.IsNullOrWhiteSpace(AllowedEventTypesCsv))
            return Array.Empty<string>();
        return AllowedEventTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void SetAllowedEventTypes(IEnumerable<string> eventTypes)
    {
        AllowedEventTypesCsv = string.Join(",", eventTypes);
    }

    public bool AllowsEventType(string eventType)
    {
        var types = GetAllowedEventTypes();
        return types.Length == 0 || types.Contains(eventType, StringComparer.OrdinalIgnoreCase) || types.Contains("*");
    }
}
