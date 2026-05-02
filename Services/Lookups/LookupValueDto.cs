using System.Text.Json;

namespace Abs.FixedAssets.Services.Lookups;

public class LookupValueDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public JsonDocument? Metadata { get; set; }

    public bool GetMetadataFlag(string key)
    {
        if (Metadata == null) return false;
        if (Metadata.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.True)
            return true;
        return false;
    }
}
