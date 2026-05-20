using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Abs.FixedAssets.Models;
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
    /// Source text for an Item. ADR-021 Appendix A shape, adapted to
    /// real column names (PartNumber, ExtendedDescription, UOM). The
    /// headline is PartNumber + Description so the embedder anchors on
    /// the part identifier as the dominant signal. The Revision string
    /// (when present) lets "find rev B of part 100123" queries surface.
    /// </summary>
    public static string ForItem(Item item)
    {
        var sb = new StringBuilder();
        sb.Append(item.PartNumber).Append(" | ").Append(item.Description).Append('\n');
        if (!string.IsNullOrWhiteSpace(item.ExtendedDescription))
        {
            sb.Append(item.ExtendedDescription).Append('\n');
        }
        sb.Append("UoM: ").Append(item.UOM);
        if (!string.IsNullOrWhiteSpace(item.StockUOM) && item.StockUOM != item.UOM.ToString())
        {
            sb.Append(" | Stock: ").Append(item.StockUOM);
        }
        sb.Append('\n');
        sb.Append("Type: ").Append(item.Type)
          .Append(" | Status: ").Append(item.Status).Append('\n');
        if (!string.IsNullOrWhiteSpace(item.Revision))
        {
            sb.Append("Revision: ").Append(item.Revision).Append('\n');
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Source text for a Vendor. ADR-021 Appendix A shape, adapted to
    /// real column names (State not StateOrProvince; Notes serves as
    /// the description field). Code + Name on the headline so "find
    /// NUCOR" or "find Nucor Steel" both anchor identically. LegalName
    /// adds the second name people might search by ("BAE Systems plc"
    /// vs "BAE"). Vendor address surfaces "find suppliers in Texas"
    /// queries.
    /// </summary>
    public static string ForVendor(Vendor vendor)
    {
        var sb = new StringBuilder();
        sb.Append(vendor.Code).Append(" | ").Append(vendor.Name).Append('\n');
        if (!string.IsNullOrWhiteSpace(vendor.LegalName) && vendor.LegalName != vendor.Name)
        {
            sb.Append("Legal: ").Append(vendor.LegalName).Append('\n');
        }
        if (!string.IsNullOrWhiteSpace(vendor.Notes))
        {
            sb.Append(vendor.Notes).Append('\n');
        }
        sb.Append("Type: ").Append(vendor.VendorType)
          .Append(" | Status: ").Append(vendor.Status).Append('\n');
        var locParts = new[] { vendor.City, vendor.State, vendor.Country }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        if (locParts.Length > 0)
        {
            sb.Append("Address: ").Append(string.Join(", ", locParts)).Append('\n');
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Source text for a WorkOrder. ADR-021 Appendix A shape, adapted
    /// to real columns (WorkOrderNumber nullable → fall back to "WO-{Id}";
    /// no separate Title column → Description IS the title). Classification
    /// + Type both surface so "show me CIP work orders" and "show me
    /// preventative work orders" both retrieve correctly.
    /// </summary>
    public static string ForWorkOrder(WorkOrder workOrder)
    {
        var sb = new StringBuilder();
        var num = string.IsNullOrWhiteSpace(workOrder.WorkOrderNumber)
            ? $"WO-{workOrder.Id}"
            : workOrder.WorkOrderNumber;
        sb.Append(num).Append(" | ").Append(workOrder.Description).Append('\n');
        sb.Append("Classification: ").Append(workOrder.Classification)
          .Append(" | Type: ").Append(workOrder.Type).Append('\n');
        sb.Append("Status: ").Append(workOrder.Status)
          .Append(" | Priority: ").Append(workOrder.Priority).Append('\n');
        if (!string.IsNullOrWhiteSpace(workOrder.Vendor))
        {
            sb.Append("Outside Vendor: ").Append(workOrder.Vendor).Append('\n');
        }
        if (!string.IsNullOrWhiteSpace(workOrder.TechnicianName))
        {
            sb.Append("Technician: ").Append(workOrder.TechnicianName).Append('\n');
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Source text for an AuditLog row's AI command utterance. Raw
    /// utterance — no enrichment per ADR-021 Appendix A. Callers MUST
    /// pre-filter to AuditLog rows with non-null + non-empty
    /// AiCommandText (the bulk-backfill query does that). Returning an
    /// empty string here is a contract violation — EnqueueAsync's
    /// IsNullOrWhiteSpace short-circuit catches it but the caller
    /// should never let it happen.
    /// </summary>
    public static string ForAiCommand(string? aiCommandText)
    {
        return (aiCommandText ?? string.Empty).Trim();
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
