using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 Appendix A — source-text builder per entity type.
//
// ONE source of truth. Whatever this returns is what gets embedded.
// Changes here invalidate prior embeddings (hash differs) and trigger
// a re-embed via the standard hash-skip rule.
//
// PR #1 only embeds ReceiptProfile end-to-end. PR #2 fills in the
// other four entity types. The switch statement gets extended; the
// shape stays.
public static class EmbeddingSourceText
{
    public const string EntityTypeReceiptProfile = "ReceiptProfile";
    public const string EntityTypeItem           = "Item";
    public const string EntityTypeVendor         = "Vendor";
    public const string EntityTypeWorkOrder      = "WorkOrder";
    public const string EntityTypeAiCommand      = "AuditLog.AiCommandText";

    /// <summary>
    /// Source text for a ReceiptProfile. Code + Name on the headline,
    /// Description (if any) on the body, schema field names as a
    /// "required attributes" line so the embedder picks up the
    /// vocabulary of the profile.
    /// </summary>
    public static string ForReceiptProfile(ReceiptProfile profile)
    {
        var sb = new StringBuilder();
        sb.Append(profile.Code).Append(" | ").Append(profile.Name).Append('\n');
        if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            sb.Append(profile.Description).Append('\n');
        }
        var fields = ExtractSchemaPropertyKeys(profile.JsonSchema);
        if (fields.Length > 0)
        {
            sb.Append("Attributes: ").Append(string.Join(", ", fields)).Append('\n');
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// SHA-256 of the source text, lowercase hex. Matches the
    /// `ContentHash` column shape (char(64)).
    /// </summary>
    public static string ComputeHash(string sourceText)
    {
        var bytes = Encoding.UTF8.GetBytes(sourceText);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Best-effort JSON-Schema property-key extraction. Reads the
    /// top-level "properties" object and returns its keys. On parse
    /// failure or absence, returns empty.
    /// </summary>
    private static string[] ExtractSchemaPropertyKeys(string? jsonSchema)
    {
        if (string.IsNullOrWhiteSpace(jsonSchema)) return System.Array.Empty<string>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonSchema);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return System.Array.Empty<string>();
            if (!doc.RootElement.TryGetProperty("properties", out var props) ||
                props.ValueKind != System.Text.Json.JsonValueKind.Object)
                return System.Array.Empty<string>();
            return props.EnumerateObject().Select(p => p.Name).ToArray();
        }
        catch
        {
            return System.Array.Empty<string>();
        }
    }
}
