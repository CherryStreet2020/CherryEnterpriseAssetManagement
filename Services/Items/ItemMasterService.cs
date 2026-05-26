// ADR-025 D5 — ItemMasterService implementation (Sprint 12.9 PR #5).
//
// Closes the 11 raw SaveChangesAsync writes that previously lived in
// Pages/Materials/ItemEdit.cshtml.cs. Pure CRUD on Item / ItemRevision /
// VendorItemPart — no embedded JournalEntry, no inventory movement, no
// outbox publishing.
//
// Pattern matches PurchasingService (PR #4) — single AppDbContext +
// ITenantContext + ILookupService dependency footprint. Each method
// returns a Result<T> envelope; expected failures (validation, not-found,
// guard violations) become Result.Failure; unexpected errors propagate
// as exceptions per ADR-014 D2.

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Items;

public sealed class ItemMasterService : IItemMasterService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILookupService _lookupService;
    private readonly Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService _chainOfCustody;
    private readonly ILogger<ItemMasterService> _logger;

    public ItemMasterService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILookupService lookupService,
        Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
        ILogger<ItemMasterService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
        _chainOfCustody = chainOfCustody;
        _logger = logger;
    }

    // ========================================================================
    // Item header CRUD
    // ========================================================================

    public async Task<Result<Item>> UpdateItemAsync(UpdateItemRequest request, CancellationToken ct)
    {
        if (request is null) return Result.Failure<Item>("request is required");
        if (string.IsNullOrWhiteSpace(request.PartNumber)) return Result.Failure<Item>("PartNumber is required");
        if (string.IsNullOrWhiteSpace(request.Description)) return Result.Failure<Item>("Description is required");
        if (string.IsNullOrWhiteSpace(request.StockUom)) return Result.Failure<Item>("StockUom is required");

        var visibleIds = _tenantContext.VisibleCompanyIds;
        var existing = await _db.Items
            .Where(i => i.Id == request.ItemId && visibleIds.Contains(i.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (existing is null) return Result.Failure<Item>("Item not found in scope.");

        existing.Description = request.Description;
        existing.ExtendedDescription = request.ExtendedDescription;
        existing.Type = await ResolveItemTypeAsync(request.TypeLookupValueId, existing.Type);
        existing.TypeLookupValueId = request.TypeLookupValueId;
        existing.Status = await ResolveItemStatusAsync(request.StatusLookupValueId, existing.Status);
        existing.StatusLookupValueId = request.StatusLookupValueId;
        existing.CostMethod = await ResolveCostMethodAsync(request.CostMethodLookupValueId, existing.CostMethod);
        existing.CostMethodLookupValueId = request.CostMethodLookupValueId;
        existing.TrackingType = await ResolveTrackingTypeAsync(request.TrackingTypeLookupValueId, existing.TrackingType);
        existing.TrackingTypeLookupValueId = request.TrackingTypeLookupValueId;
        existing.StockUOM = request.StockUom;
        existing.IsActive = request.IsActive;

        existing.LeadTimeDays = request.LeadTimeDays ?? existing.LeadTimeDays;
        existing.MinOrderQty = request.MinOrderQty;
        existing.OrderMultiple = request.OrderMultiple;
        existing.PurchaseUOM = request.PurchaseUom ?? existing.PurchaseUOM;
        existing.PackQty = request.PackQty;
        existing.StockPolicy = request.StockPolicy;
        existing.LastPrice = request.LastPrice;
        existing.CurrencyCode = request.CurrencyCode;
        existing.PriceEffectiveDate = request.PriceEffectiveDate;
        existing.ContractFlag = request.ContractFlag;
        existing.ContractRef = request.ContractRef;
        existing.DefaultLocationId = request.DefaultLocationId;
        if (request.StandardCost.HasValue)
            existing.StandardCost = request.StandardCost.Value;

        // B6 PR-FS-1 — ItemGroup classification. Allow null preserve when caller
        // omits; otherwise update. ItemGroup existence + tenant-scope validation
        // is enforced by the FK at the DB level (SET NULL on ItemGroup delete).
        if (request.ItemGroupId.HasValue)
            existing.ItemGroupId = request.ItemGroupId.Value;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated item {ItemId} ({PartNumber}).", existing.Id, existing.PartNumber);
        return Result.Success(existing);
    }

    public async Task<Result<Item>> CreateItemAsync(CreateItemRequest request, CancellationToken ct)
    {
        if (request is null) return Result.Failure<Item>("request is required");
        if (string.IsNullOrWhiteSpace(request.PartNumber)) return Result.Failure<Item>("PartNumber is required");
        if (string.IsNullOrWhiteSpace(request.Description)) return Result.Failure<Item>("Description is required");
        if (string.IsNullOrWhiteSpace(request.StockUom)) return Result.Failure<Item>("StockUom is required");

        var visibleIds = _tenantContext.VisibleCompanyIds;
        var dup = await _db.Items
            .Where(i => visibleIds.Contains(i.CompanyId ?? 0))
            .AnyAsync(i => i.PartNumber.ToLower() == request.PartNumber.ToLower(), ct);
        if (dup) return Result.Failure<Item>("An item with this Part Number already exists.");

        var resolvedType = await ResolveItemTypeAsync(request.TypeLookupValueId, ItemType.Part);
        var resolvedStatus = await ResolveItemStatusAsync(request.StatusLookupValueId, ItemStatus.Active);
        var resolvedCostMethod = await ResolveCostMethodAsync(request.CostMethodLookupValueId, CostMethod.Average);
        var resolvedTracking = await ResolveTrackingTypeAsync(request.TrackingTypeLookupValueId, TrackingType.None);

        // B6 PR-FS-1 — ItemGroup gate. When Source=Internal (i.e. the operator
        // is creating the Item natively in the app rather than importing from an
        // ExternalERP), ItemGroupId MUST be supplied. The PostingProfile cascade
        // (PRA-7) cannot resolve without it, and we never want to land an
        // unclassified Item via the native create path going forward.
        //
        // ExternalERP imports remain allowed to skip ItemGroupId — the legacy
        // 151 Items on dev are pre-FS-1 and grandfathered as nullable.
        if (request.Source == ItemMasterSource.Internal && !request.ItemGroupId.HasValue)
        {
            return Result.Failure<Item>(
                "ItemGroupId is required when creating an Item with Source=Internal. " +
                "Classify the Item into one of the seeded ItemGroups (RAW / WIP / FG / " +
                "CONSUMABLE / SUBASSY / SUBCONTR / TOOLING / SPAREPART / PACKAGING / " +
                "ASSET / SERVICE / AS9102-FG) before retrying.");
        }

        var newItem = new Item
        {
            PartNumber = request.PartNumber,
            Description = request.Description,
            ExtendedDescription = request.ExtendedDescription,
            Type = resolvedType,
            TypeLookupValueId = request.TypeLookupValueId,
            Status = resolvedStatus,
            StatusLookupValueId = request.StatusLookupValueId,
            CostMethod = resolvedCostMethod,
            CostMethodLookupValueId = request.CostMethodLookupValueId,
            TrackingType = resolvedTracking,
            TrackingTypeLookupValueId = request.TrackingTypeLookupValueId,
            StockUOM = request.StockUom,
            IsActive = request.IsActive,
            LeadTimeDays = request.LeadTimeDays ?? 0,
            MinOrderQty = request.MinOrderQty,
            OrderMultiple = request.OrderMultiple,
            PurchaseUOM = request.PurchaseUom ?? "EA",
            PackQty = request.PackQty,
            StockPolicy = request.StockPolicy,
            LastPrice = request.LastPrice,
            CurrencyCode = request.CurrencyCode ?? "USD",
            PriceEffectiveDate = request.PriceEffectiveDate,
            ContractFlag = request.ContractFlag,
            ContractRef = request.ContractRef,
            StandardCost = request.StandardCost ?? 0m,
            DefaultLocationId = request.DefaultLocationId,
            ItemGroupId = request.ItemGroupId,            // B6 PR-FS-1
            Source = request.Source,                       // B6 PR-FS-1 — explicit source
            CompanyId = _tenantContext.CompanyId
        };

        _db.Items.Add(newItem);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created item {ItemId} ({PartNumber}) classified into ItemGroup {ItemGroupId}.",
            newItem.Id, newItem.PartNumber, newItem.ItemGroupId);
        return Result.Success(newItem);
    }

    // ========================================================================
    // Revision metadata writes
    // ========================================================================

    public async Task<Result<ItemRevision>> SaveRevisionAsync(SaveRevisionRequest request, CancellationToken ct)
    {
        if (request is null) return Result.Failure<ItemRevision>("request is required");

        var visibleIds = _tenantContext.VisibleCompanyIds;
        var revision = await _db.ItemRevisions
            .Include(r => r.Item)
            .Where(r => r.Id == request.RevisionId && r.Item != null && visibleIds.Contains(r.Item!.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (revision is null) return Result.Failure<ItemRevision>("Revision not found.");

        if (revision.Status != RevisionStatus.Draft)
            return Result.Failure<ItemRevision>("Only draft revisions can be edited.");

        revision.RevisionCode = request.RevisionCode;
        revision.Name = request.Name;
        revision.Description = request.Description;
        revision.ChangeReason = request.ChangeReason;

        await _db.SaveChangesAsync(ct);

        // Sprint 12D PR #3.4 / ADR-022 §D5 — chain-of-custody graph emission.
        //
        // Closes the PR #3.x arc — all 5 Sprint 12.9 services now emit chain
        // edges. Voice query "what's the revision history of this item?"
        // walks downstream from Item through REVISION_OF to find every
        // released revision in lineage order (the revision's RevisionCode
        // becomes the node label so the cytoscape.js viz can render the
        // version timeline directly).
        //
        // Edge:
        //   Item --REVISION_OF--> ItemRevision
        //
        // Failure isolation: same try/catch + LogWarning pattern.
        try
        {
            await _chainOfCustody.RecordEdgeAsync(
                new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                    FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Item,
                    FromEntityId: revision.ItemId,
                    FromLabel:    revision.Item?.PartNumber ?? $"Item-{revision.ItemId}",
                    ToNodeType:   "ItemRevision",
                    ToEntityId:   revision.Id,
                    ToLabel:      revision.RevisionCode ?? $"Rev-{revision.Id}",
                    EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.RevisionOf),
                ct);
        }
        catch (Exception chainEx)
        {
            _logger.LogWarning(chainEx,
                "ItemMasterService.SaveRevisionAsync: chain-of-custody emit failed for item {ItemId} revision {RevisionId}. Revision save committed; chain can be rebuilt via backfill.",
                revision.ItemId, revision.Id);
        }

        return Result.Success(revision);
    }

    public async Task<Result<ItemRevision>> ObsoleteRevisionAsync(int revisionId, CancellationToken ct)
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        var revision = await _db.ItemRevisions
            .Include(r => r.Item)
            .Where(r => r.Id == revisionId && r.Item != null && visibleIds.Contains(r.Item!.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (revision is null) return Result.Failure<ItemRevision>("Revision not found.");

        if (revision.Status == RevisionStatus.Draft)
            return Result.Failure<ItemRevision>("Cannot obsolete a draft. Delete it instead.");

        if (revision.Item?.CurrentReleasedRevisionId == revisionId)
            return Result.Failure<ItemRevision>("Cannot obsolete the current released revision. Release a new revision first.");

        revision.Status = RevisionStatus.Obsolete;

        // Keep the FK column in sync with the legacy enum write.
        var obsoleteLv = await _lookupService.GetValueByCodeAsync(
            _tenantContext.TenantId, _tenantContext.CompanyId,
            "RevisionStatus", ((int)RevisionStatus.Obsolete).ToString());
        if (obsoleteLv is not null)
            revision.StatusLookupValueId = obsoleteLv.Id;

        revision.ObsoletedAtUtc = DateTime.UtcNow;
        revision.EffectiveToUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(revision);
    }

    public async Task<Result<int>> DeleteDraftRevisionAsync(int revisionId, CancellationToken ct)
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        var revision = await _db.ItemRevisions
            .Include(r => r.Item)
            .Where(r => r.Id == revisionId && r.Item != null && visibleIds.Contains(r.Item!.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (revision is null) return Result.Failure<int>("Revision not found.");

        if (revision.Status != RevisionStatus.Draft)
            return Result.Failure<int>("Only draft revisions can be deleted.");

        var itemId = revision.ItemId;
        _db.ItemRevisions.Remove(revision);
        await _db.SaveChangesAsync(ct);
        return Result.Success(itemId);
    }

    // ========================================================================
    // Vendor-part URL writes (after AddVpn via IItemCrossReferenceService)
    // ========================================================================

    public async Task<Result<VendorItemPart>> SetVpnUrlsAsync(SetVpnUrlsRequest request, CancellationToken ct)
    {
        if (request is null) return Result.Failure<VendorItemPart>("request is required");

        var vpn = await _db.Set<VendorItemPart>()
            .Where(v => v.Id == request.VpnId)
            .FirstOrDefaultAsync(ct);
        if (vpn is null) return Result.Failure<VendorItemPart>("VendorItemPart not found.");

        // PageModel only writes when at least one URL is non-empty — preserve exact behavior.
        if (string.IsNullOrEmpty(request.CatalogUrl) &&
            string.IsNullOrEmpty(request.DatasheetUrl) &&
            string.IsNullOrEmpty(request.ExternalImageUrl))
        {
            return Result.Success(vpn);
        }

        vpn.CatalogUrl = ValidateHttpsUrl(request.CatalogUrl);
        vpn.DatasheetUrl = ValidateHttpsUrl(request.DatasheetUrl);
        vpn.ExternalImageUrl = ValidateHttpsUrl(request.ExternalImageUrl);

        await _db.SaveChangesAsync(ct);
        return Result.Success(vpn);
    }

    public async Task<Result<VendorItemPart>> UpdateVpnCatalogInfoAsync(UpdateVpnCatalogRequest request, CancellationToken ct)
    {
        if (request is null) return Result.Failure<VendorItemPart>("request is required");

        var vpn = await _db.Set<VendorItemPart>()
            .Where(v => v.Id == request.VpnId && v.ItemId == request.ItemId)
            .FirstOrDefaultAsync(ct);
        if (vpn is null) return Result.Failure<VendorItemPart>("VendorItemPart not found.");

        vpn.CatalogUrl = ValidateHttpsUrl(request.CatalogUrl);
        if (!string.IsNullOrEmpty(request.ImageUrl))
            vpn.ExternalImageUrl = ValidateHttpsUrl(request.ImageUrl);
        vpn.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(vpn);
    }

    public async Task<Result<VendorItemPart>> ApplyVpnEnrichmentAsync(ApplyVpnEnrichmentRequest request, CancellationToken ct)
    {
        if (request is null) return Result.Failure<VendorItemPart>("request is required");

        var vpn = await _db.Set<VendorItemPart>()
            .Where(v => v.Id == request.VpnId)
            .FirstOrDefaultAsync(ct);
        if (vpn is null) return Result.Failure<VendorItemPart>("VendorItemPart not found.");

        vpn.LastEnrichedUtc = DateTime.UtcNow;
        vpn.LastEnrichStatus = request.Status;

        if (request.Status == "Success" || request.Status == "NoMetadata")
        {
            if (!string.IsNullOrEmpty(request.CanonicalUrl))
                vpn.CatalogUrl = request.CanonicalUrl;
            if (!string.IsNullOrEmpty(request.ImageUrl))
                vpn.ExternalImageUrl = request.ImageUrl;
            if (!string.IsNullOrEmpty(request.Mpn))
                vpn.ExtractedMpn = request.Mpn;
            if (!string.IsNullOrEmpty(request.Sku))
                vpn.ExtractedSku = request.Sku;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(vpn);
    }

    // ========================================================================
    // Item image writes
    // ========================================================================

    public async Task<Result<Item>> SetItemImagePathAsync(int itemId, string newPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPath)) return Result.Failure<Item>("newPath is required");

        var visibleIds = _tenantContext.VisibleCompanyIds;
        var item = await _db.Items
            .Where(i => i.Id == itemId && visibleIds.Contains(i.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (item is null) return Result.Failure<Item>("Item not found in scope.");

        item.ImagePath = newPath;
        await _db.SaveChangesAsync(ct);
        return Result.Success(item);
    }

    public async Task<Result<Item>> ClearItemImagePathAsync(int itemId, CancellationToken ct)
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        var item = await _db.Items
            .Where(i => i.Id == itemId && visibleIds.Contains(i.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (item is null) return Result.Failure<Item>("Item not found in scope.");

        item.ImagePath = null;
        await _db.SaveChangesAsync(ct);
        return Result.Success(item);
    }

    public async Task<Result<Item>> SetItemExternalImageFromVpnAsync(int itemId, int vpnId, CancellationToken ct)
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        var item = await _db.Items
            .Where(i => i.Id == itemId && visibleIds.Contains(i.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        var vpn = await _db.Set<VendorItemPart>()
            .Where(v => v.Id == vpnId && v.ItemId == itemId)
            .FirstOrDefaultAsync(ct);
        if (item is null || vpn is null) return Result.Failure<Item>("Item or VendorItemPart not found.");

        var externalUrl = vpn.ExternalImageUrl ?? vpn.ImageUrl;
        if (string.IsNullOrEmpty(externalUrl))
            return Result.Failure<Item>("No external image URL available to copy.");

        item.ExternalImageUrl = externalUrl;
        await _db.SaveChangesAsync(ct);
        return Result.Success(item);
    }

    // ========================================================================
    // Private helpers
    // ========================================================================

    private async Task<ItemType> ResolveItemTypeAsync(int? lookupValueId, ItemType fallback)
    {
        if (!lookupValueId.HasValue) return fallback;
        var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, lookupValueId.Value);
        if (lv != null && int.TryParse(lv.Code, out var enumVal))
            return (ItemType)enumVal;
        return fallback;
    }

    private async Task<ItemStatus> ResolveItemStatusAsync(int? lookupValueId, ItemStatus fallback)
    {
        if (!lookupValueId.HasValue) return fallback;
        var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, lookupValueId.Value);
        if (lv != null && int.TryParse(lv.Code, out var enumVal))
            return (ItemStatus)enumVal;
        return fallback;
    }

    private async Task<CostMethod> ResolveCostMethodAsync(int? lookupValueId, CostMethod fallback)
    {
        if (!lookupValueId.HasValue) return fallback;
        var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, lookupValueId.Value);
        if (lv != null && int.TryParse(lv.Code, out var enumVal))
            return (CostMethod)enumVal;
        return fallback;
    }

    private async Task<TrackingType> ResolveTrackingTypeAsync(int? lookupValueId, TrackingType fallback)
    {
        if (!lookupValueId.HasValue) return fallback;
        var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, lookupValueId.Value);
        if (lv != null && int.TryParse(lv.Code, out var enumVal))
            return (TrackingType)enumVal;
        return fallback;
    }

    private static string? ValidateHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            return url;
        return null;
    }
}
