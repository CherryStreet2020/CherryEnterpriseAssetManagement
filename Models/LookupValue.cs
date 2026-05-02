using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Abs.FixedAssets.Models;

public class LookupValue
{
    public int Id { get; set; }

    public int LookupTypeId { get; set; }
    public LookupType LookupType { get; set; } = null!;

    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool GetMetadataFlag(string key)
    {
        if (Metadata == null) return false;
        if (Metadata.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.True)
            return true;
        return false;
    }
}
