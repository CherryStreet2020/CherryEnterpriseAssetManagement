// ADR-025 D5 — IItemMasterService (Sprint 12.9 PR #5).
//
// Centralizes write paths off Pages/Materials/ItemEdit.cshtml.cs (11 direct
// SaveChangesAsync calls per the 2026-05-20 audit, 3rd-worst offender).
//
// Unlike IWorkOrderService, this surface has zero embedded JournalEntry
// construction or inventory movement — Item Master writes are pure CRUD on
// Item + ItemRevision + VendorItemPart. No outbox events, no period guards,
// no posting orchestration.
//
// De-risks Sprint 7-9 (Item Master Expansion + Multi-Dim Inventory + 11-tab
// ItemEdit rewrite) which will build on this contract.
//
// Sibling services already in place that this surface complements
// (NOT replaces):
//   • IItemRevisionService     — Draft creation + Release lifecycle (already a service).
//   • IItemCrossReferenceService — MPN/VPN Add (already a service).
//   • IItemImageService        — Image bytes + disk path (already a service).
//   • IItemSourcingService     — Approved-vendor + preferred-vendor (already a service).
//   • IItemAlternateService    — Alternate-part wiring (already a service).
//   • IItemSupersessionService — Item-to-item supersession (already a service).
//
// IItemMasterService picks up the 11 raw AppDbContext writes that previously
// lived in the PageModel — Item header CRUD, revision metadata edits,
// post-AddVpn URL writes, image-path writes, and enrichment-result writes.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Services.Items;

/// <summary>
/// Domain service for Item Master mutations (Sprint 12.9 PR #5).
/// New PageModel callers should inject <see cref="IItemMasterService"/>
/// instead of AppDbContext for any Item / ItemRevision / VendorItemPart
/// mutation not already covered by an existing item-domain service.
/// </summary>
public interface IItemMasterService
{
    // === Item header CRUD ===

    /// <summary>Update an existing Item's header + procurement properties.</summary>
    Task<Result<Item>> UpdateItemAsync(UpdateItemRequest request, CancellationToken ct);

    /// <summary>Create a new Item with dup-PartNumber guard.</summary>
    Task<Result<Item>> CreateItemAsync(CreateItemRequest request, CancellationToken ct);

    // === Revision metadata (Draft-only edits; lifecycle stays in IItemRevisionService) ===

    /// <summary>Save edits to a Draft revision's code / name / description / change reason.</summary>
    Task<Result<ItemRevision>> SaveRevisionAsync(SaveRevisionRequest request, CancellationToken ct);

    /// <summary>
    /// Mark a non-Draft revision Obsolete. Syncs <c>StatusLookupValueId</c> via
    /// <see cref="Lookups.ILookupService"/> + sets <c>ObsoletedAtUtc</c> and
    /// <c>EffectiveToUtc</c> to <see cref="System.DateTime.UtcNow"/>. Rejects the
    /// current released revision (a new one must be released first).
    /// </summary>
    Task<Result<ItemRevision>> ObsoleteRevisionAsync(int revisionId, CancellationToken ct);

    /// <summary>Delete a Draft revision; returns the parent ItemId for redirect.</summary>
    Task<Result<int>> DeleteDraftRevisionAsync(int revisionId, CancellationToken ct);

    // === Vendor-Part URL writes (after AddVpn via IItemCrossReferenceService) ===

    /// <summary>
    /// Set CatalogUrl / DatasheetUrl / ExternalImageUrl on a freshly-added VPN.
    /// No-op if all three URLs are null/empty (matches legacy PageModel behavior).
    /// HTTPS-only URLs are kept; non-HTTPS or malformed are nulled.
    /// </summary>
    Task<Result<VendorItemPart>> SetVpnUrlsAsync(SetVpnUrlsRequest request, CancellationToken ct);

    /// <summary>
    /// Update a VPN's CatalogUrl + optional ExternalImageUrl. Sets
    /// <c>UpdatedAtUtc</c> to <see cref="System.DateTime.UtcNow"/>.
    /// </summary>
    Task<Result<VendorItemPart>> UpdateVpnCatalogInfoAsync(UpdateVpnCatalogRequest request, CancellationToken ct);

    /// <summary>
    /// Apply a catalog-enrichment outcome to a VPN. Always stamps
    /// <c>LastEnrichedUtc</c> + <c>LastEnrichStatus</c>; on Success / NoMetadata
    /// also copies CanonicalUrl, ImageUrl, ExtractedMpn, ExtractedSku when present.
    /// </summary>
    Task<Result<VendorItemPart>> ApplyVpnEnrichmentAsync(ApplyVpnEnrichmentRequest request, CancellationToken ct);

    // === Item image writes ===

    /// <summary>Set Item.ImagePath after an upload has been written to disk.</summary>
    Task<Result<Item>> SetItemImagePathAsync(int itemId, string newPath, CancellationToken ct);

    /// <summary>Clear Item.ImagePath (caller is expected to have already deleted bytes via IItemImageService).</summary>
    Task<Result<Item>> ClearItemImagePathAsync(int itemId, CancellationToken ct);

    /// <summary>Copy a VPN's external image URL up to Item.ExternalImageUrl as fallback.</summary>
    Task<Result<Item>> SetItemExternalImageFromVpnAsync(int itemId, int vpnId, CancellationToken ct);
}

// === Request records ===

/// <summary>Update-existing-Item header request. <see cref="ItemId"/> must reference a visible-tenant row.</summary>
public sealed record UpdateItemRequest(
    int ItemId,
    string PartNumber,                  // not written on Update path — included for symmetry with Create
    int? TypeLookupValueId,
    string Description,
    string? ExtendedDescription,
    string StockUom,
    bool IsActive,
    int? LeadTimeDays,
    decimal? MinOrderQty,
    decimal? OrderMultiple,
    string? PurchaseUom,
    decimal? PackQty,
    StockPolicy StockPolicy,
    decimal? LastPrice,
    string? CurrencyCode,
    System.DateTime? PriceEffectiveDate,
    bool ContractFlag,
    string? ContractRef,
    int? StatusLookupValueId,
    int? CostMethodLookupValueId,
    int? TrackingTypeLookupValueId,
    decimal? StandardCost,
    int? DefaultLocationId,
    int? ItemGroupId);                  // B6 PR-FS-1 — ItemGroup classification

/// <summary>Create-new-Item request. Service performs dup-PartNumber guard within tenant scope.</summary>
public sealed record CreateItemRequest(
    string PartNumber,
    int? TypeLookupValueId,
    string Description,
    string? ExtendedDescription,
    string StockUom,
    bool IsActive,
    int? LeadTimeDays,
    decimal? MinOrderQty,
    decimal? OrderMultiple,
    string? PurchaseUom,
    decimal? PackQty,
    StockPolicy StockPolicy,
    decimal? LastPrice,
    string? CurrencyCode,
    System.DateTime? PriceEffectiveDate,
    bool ContractFlag,
    string? ContractRef,
    int? StatusLookupValueId,
    int? CostMethodLookupValueId,
    int? TrackingTypeLookupValueId,
    decimal? StandardCost,
    int? DefaultLocationId,
    int? ItemGroupId,                   // B6 PR-FS-1 — ItemGroup classification
    ItemMasterSource Source = ItemMasterSource.Internal); // Defaults to Internal; rev-control gate triggers when Source=Internal

/// <summary>Save-Draft-revision request.</summary>
public sealed record SaveRevisionRequest(
    int RevisionId,
    string RevisionCode,
    string Name,
    string? Description,
    string? ChangeReason);

/// <summary>Set-VPN-URLs request used post-AddVpn.</summary>
public sealed record SetVpnUrlsRequest(
    int VpnId,
    string? CatalogUrl,
    string? DatasheetUrl,
    string? ExternalImageUrl);

/// <summary>Update-VPN-catalog request from the Catalog tab.</summary>
public sealed record UpdateVpnCatalogRequest(
    int ItemId,
    int VpnId,
    string? CatalogUrl,
    string? ImageUrl);

/// <summary>Apply-VPN-enrichment request — service consumes the IEnrichmentService result here.</summary>
public sealed record ApplyVpnEnrichmentRequest(
    int VpnId,
    string Status,
    string? CanonicalUrl,
    string? ImageUrl,
    string? Mpn,
    string? Sku);
