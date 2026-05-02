using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("IntegrationMappings")]
public class IntegrationMapping
{
    [Key]
    public int Id { get; set; }

    public int IntegrationEndpointId { get; set; }

    [Required]
    [StringLength(50)]
    public string MappingType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ExternalId { get; set; } = string.Empty;

    public int? InternalId { get; set; }

    [StringLength(200)]
    public string? InternalCode { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [ForeignKey(nameof(IntegrationEndpointId))]
    public IntegrationEndpoint? IntegrationEndpoint { get; set; }
}

public static class IntegrationMappingType
{
    public const string Site = "Site";
    public const string Asset = "Asset";
    public const string GlAccount = "GlAccount";
    public const string Vendor = "Vendor";
    public const string Location = "Location";
}
