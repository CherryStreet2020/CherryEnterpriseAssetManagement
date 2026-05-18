using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Admin.StockReceipts;

// Sprint 4 Phase F Wave 1 PR #5 + ADR-015 Migration PR #3 —
// StockReceipt list page.
//
// PR #3 deltas:
//   - Project StockReceiptListRow with profile + 2 facet values
//   - "With Heat #" KPI replaced by "With Required Facets" (profile-aware)
//   - Heat # / Mill columns replaced by dynamic FacetA / FacetB columns
//     sourced from profile.PromotedFacets[0..1]
[Authorize(Policy = "StockReceipt.View")]
public class IndexModel : VoiceReadyPageModel
{
    private readonly IStockReceiptService _svc;

    public IndexModel(IStockReceiptService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)]
    public StockReceiptStatus? StatusFilter { get; set; }

    public IReadOnlyList<StockReceiptListRow> Rows { get; private set; } = Array.Empty<StockReceiptListRow>();
    public int TotalCount { get; private set; }
    public int AvailableCount { get; private set; }
    public int QuarantinedCount { get; private set; }
    public int FullyFacetedCount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var r = await _svc.ListAsync(StatusFilter, HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }
        var receipts = r.Value!;

        var rows = new List<StockReceiptListRow>(receipts.Count);
        var fullyFaceted = 0;

        foreach (var sr in receipts)
        {
            var profile = sr.Profile;
            var facetKeys = ExtractPromotedFacets(profile?.PromotedFacets);
            var attrs = DeserializeAttrs(sr.Attributes);

            var facetA = ReadAttr(attrs, facetKeys.ElementAtOrDefault(0));
            var facetB = ReadAttr(attrs, facetKeys.ElementAtOrDefault(1));

            var facetALabel = HumanizeFacetLabel(facetKeys.ElementAtOrDefault(0));
            var facetBLabel = HumanizeFacetLabel(facetKeys.ElementAtOrDefault(1));

            // "Fully faceted" = every PromotedFacet for this row's profile
            // has a non-empty value in Attributes. Generic across profiles.
            if (facetKeys.Count > 0 && facetKeys.All(k => !string.IsNullOrEmpty(ReadAttr(attrs, k))))
                fullyFaceted++;

            rows.Add(new StockReceiptListRow(
                Id: sr.Id,
                ReceiptNumber: sr.ReceiptNumber,
                Status: sr.Status,
                ProfileCode: profile?.Code ?? "—",
                ItemId: sr.ItemId,
                ItemDescription: sr.Item?.PartNumber ?? sr.Item?.Description,
                MaterialMasterShopCode: sr.MaterialMaster?.ShopCode,
                MaterialMasterAstm: sr.MaterialMaster?.AstmDesignation,
                LotNumber: sr.LotNumber,
                SerialNumber: sr.SerialNumber,
                FacetA: facetA,
                FacetB: facetB,
                FacetALabel: facetALabel,
                FacetBLabel: facetBLabel,
                QuantityReceived: sr.QuantityReceived,
                QuantityRemaining: sr.QuantityRemaining,
                Uom: sr.Uom,
                ReceivedAt: sr.ReceivedAt,
                SourcePoNumber: sr.SourcePoNumber));
        }

        Rows = rows;
        TotalCount = receipts.Count;
        AvailableCount = receipts.Count(r => r.Status == StockReceiptStatus.Available);
        QuarantinedCount = receipts.Count(r => r.Status == StockReceiptStatus.Quarantined);
        FullyFacetedCount = fullyFaceted;
        return Page();
    }

    public override VoiceContextPayload BuildContextPayload()
    {
        var b = base.BuildContextPayload();
        return new VoiceContextPayload
        {
            Route = b.Route,
            UserId = b.UserId,
            Roles = b.Roles,
            TenantId = b.TenantId,
            EntityType = nameof(StockReceipt),
            EntityId = null,
            RelatedIds = b.RelatedIds,
            FocusedField = b.FocusedField,
            Tab = StatusFilter?.ToString() ?? b.Tab,
            BuiltAt = b.BuiltAt,
        };
    }

    private static List<string> ExtractPromotedFacets(string? promotedFacetsJson)
    {
        if (string.IsNullOrWhiteSpace(promotedFacetsJson)) return new List<string>();
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(promotedFacetsJson);
            return arr ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static Dictionary<string, object?> DeserializeAttrs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static string? ReadAttr(IReadOnlyDictionary<string, object?> attrs, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (!attrs.TryGetValue(key, out var v) || v is null) return null;

        if (v is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Null   => null,
                _                    => je.GetRawText(),
            };
        }
        return v.ToString();
    }

    // "heatNumber" -> "Heat Number"; "ndc" -> "Ndc". Cheap human label
    // so we don't need a server-side label map; v2 reads UiFormSpec for
    // the official label.
    private static string HumanizeFacetLabel(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "—";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (i == 0) sb.Append(char.ToUpperInvariant(c));
            else if (char.IsUpper(c)) { sb.Append(' '); sb.Append(c); }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

// Flat DTO projected per row for the Index page. Materialized in
// OnGetAsync so the JSONB read happens once.
public sealed record StockReceiptListRow(
    int Id,
    string ReceiptNumber,
    StockReceiptStatus Status,
    string ProfileCode,
    int ItemId,
    string? ItemDescription,
    string? MaterialMasterShopCode,
    string? MaterialMasterAstm,
    string? LotNumber,
    string? SerialNumber,
    string? FacetA,
    string? FacetB,
    string FacetALabel,
    string FacetBLabel,
    decimal QuantityReceived,
    decimal QuantityRemaining,
    string? Uom,
    DateTime ReceivedAt,
    string? SourcePoNumber);
