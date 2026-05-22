using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Items;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Navigation;

namespace Abs.FixedAssets.Pages.Materials;

// Sprint 12.9 PR #5 — all 11 operational writes refactored to IItemMasterService.
// AppDbContext is retained for read-path projections + tenant-scope guards per
// ADR-025 D1. CHERRY025 analyzer is satisfied via this exempt attribute, NOT
// via the allowlist (file removed from Analyzers/ControlPlaneAllowlist.txt
// 116 → 115 in this PR).
[ControlPlaneExempt("Sprint 12.9 PR #5 — all 11 operational writes refactored to IItemMasterService; remaining AppDbContext use is read-only projections + tenant-scope guards per ADR-025 D1.")]
public class ItemEditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IItemMasterService _itemMaster;
    private readonly IItemRevisionService _revisionService;
    private readonly IItemCrossReferenceService _crossRefService;
    private readonly IItemSourcingService _sourcingService;
    private readonly IItemAlternateService _alternateService;
    private readonly IItemSupersessionService _supersessionService;
    private readonly IItemImageService _imageService;
    private readonly ICatalogMetadataEnrichmentService _enrichmentService;
    private readonly IBuyabilityScoreService _buyabilityService;
    private readonly IEffectiveProcurementService _effectiveProcurementService;
    private readonly ILookupService _lookupService;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;

    public ItemEditModel(AppDbContext db,
        IItemMasterService itemMaster,
        IItemRevisionService revisionService,
        IItemCrossReferenceService crossRefService,
        IItemSourcingService sourcingService,
        IItemAlternateService alternateService,
        IItemSupersessionService supersessionService,
        IItemImageService imageService,
        ICatalogMetadataEnrichmentService enrichmentService,
        IBuyabilityScoreService buyabilityService,
        IEffectiveProcurementService effectiveProcurementService,
        ILookupService lookupService,
        ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
    {
            _moduleGuard = moduleGuard;
        _db = db;
        _itemMaster = itemMaster;
        _revisionService = revisionService;
        _crossRefService = crossRefService;
        _sourcingService = sourcingService;
        _alternateService = alternateService;
        _supersessionService = supersessionService;
        _imageService = imageService;
        _enrichmentService = enrichmentService;
        _buyabilityService = buyabilityService;
        _effectiveProcurementService = effectiveProcurementService;
        _lookupService = lookupService;
        _tenantContext = tenantContext;
    }

    public Item? Item { get; set; }
    public List<SelectListItem> ActiveInactiveOptions { get; set; } = new();
    public List<SelectListItem> TypeOptions { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<SelectListItem> CostMethodOptions { get; set; } = new();
    public List<SelectListItem> TrackingTypeOptions { get; set; } = new();
    public List<SelectListItem> RevisionStatusOptions { get; set; } = new();
    public ItemRevision? CurrentRevision { get; set; }
    public List<ItemRevision> Revisions { get; set; } = new();
    public List<ItemManufacturerPart> ManufacturerParts { get; set; } = new();
    public List<VendorItemPart> VendorParts { get; set; } = new();
    public List<Manufacturer> Manufacturers { get; set; } = new();
    public List<Vendor> Vendors { get; set; } = new();
    // DEF-008 follow-up: operator-set default stocking location for receive
    // cascade (ItemCompanyStocking.DefaultLocationId → Item.DefaultLocationId → null).
    public List<Location> Locations { get; set; } = new();
    public List<ItemApprovedVendor> ApprovedVendors { get; set; } = new();
    public List<ItemAlternate> Alternates { get; set; } = new();
    public ItemSupersession? Supersession { get; set; }
    public ItemSupersession? SupersededBy { get; set; }
    public Item? ResolvedCurrentItem { get; set; }
    public List<Item> AllItems { get; set; } = new();
    public bool IsNew => Item?.Id == 0 || Item == null;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string GetBackUrl() => ReturnUrlHelper.GetBackUrl(ReturnUrl, "/Materials/ItemEdit");

    private async Task<Item?> LoadItemInScopeAsync(int itemId)
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        return await _db.Items.Where(i => i.Id == itemId && visibleIds.Contains(i.CompanyId ?? 0)).FirstOrDefaultAsync();
    }

    // Derived summary fields for UI display (read-only)
    public string? CurrentRevisionCode => CurrentRevision?.RevisionCode;
    public ItemManufacturerPart? PrimaryMpn => ManufacturerParts
        .Where(m => m.IsActive)
        .OrderBy(m => m.Manufacturer?.Code ?? "")
        .ThenBy(m => m.MfrPartNumber)
        .FirstOrDefault();
    public ItemApprovedVendor? PreferredAvl => ApprovedVendors
        .FirstOrDefault(a => a.IsPreferred && a.ApprovalStatus == AvlApprovalStatus.Approved);
    public VendorItemPart? PreferredVpn => PreferredAvl != null 
        ? VendorParts.FirstOrDefault(v => v.VendorId == PreferredAvl.VendorId && v.IsActive)
        : VendorParts.FirstOrDefault(v => v.Preferred && v.IsActive);

    // Catalog link logic - follows premium rules for preferred vendor
    public bool HasPreferredVendor => PreferredAvl != null;
    public bool HasPreferredVpn => PreferredVpn != null;
    public string? PreferredVendorName => PreferredAvl?.Vendor?.Name;
    public string? PreferredVendorCatalogUrl => PreferredVpn?.ProductPageUrl ?? PreferredVpn?.CatalogUrl;
    
    // All vendor parts with catalog URLs (for split button dropdown) - deterministic ordering by vendor name
    public List<VendorItemPart> VendorPartsWithCatalog => VendorParts
        .Where(v => v.IsActive && !string.IsNullOrEmpty(v.ProductPageUrl ?? v.CatalogUrl))
        .OrderBy(v => v.Vendor?.Name ?? "")
        .Take(8)
        .ToList();
    
    // Catalog button state for hero
    public enum CatalogButtonState { None, PreferredWithUrl, PreferredNoUrl, SingleVendor, MultipleVendors }
    public CatalogButtonState GetCatalogState()
    {
        // Only use preferred vendor rules if we have both an AVL entry AND a matching VPN record
        if (HasPreferredVendor && HasPreferredVpn)
        {
            return !string.IsNullOrEmpty(PreferredVendorCatalogUrl) 
                ? CatalogButtonState.PreferredWithUrl 
                : CatalogButtonState.PreferredNoUrl;
        }
        var vendorsWithCatalog = VendorPartsWithCatalog.Count;
        return vendorsWithCatalog switch
        {
            0 => CatalogButtonState.None,
            1 => CatalogButtonState.SingleVendor,
            _ => CatalogButtonState.MultipleVendors
        };
    }
    
    // First vendor with catalog (for single vendor case) - deterministic ordering
    public VendorItemPart? FirstVendorWithCatalog => VendorPartsWithCatalog.FirstOrDefault();
    
    // Image priority: ImagePath > Preferred vendor image > First vendor image > placeholder
    public string? PreferredImageUrl => PreferredVpn?.ExternalImageUrl ?? PreferredVpn?.ImageUrl;
    public string? FirstVendorImageUrl => VendorParts
        .Where(v => v.IsActive && !string.IsNullOrEmpty(v.ExternalImageUrl ?? v.ImageUrl))
        .Select(v => v.ExternalImageUrl ?? v.ImageUrl)
        .FirstOrDefault();

    // Item hero image URL with priority: ImagePath > Preferred vendor image > First vendor image > placeholder
    public string GetItemImageUrl()
    {
        if (!string.IsNullOrEmpty(Item?.ImagePath))
            return Item.ImagePath;
        if (!string.IsNullOrEmpty(PreferredImageUrl))
            return PreferredImageUrl;
        if (!string.IsNullOrEmpty(FirstVendorImageUrl))
            return FirstVendorImageUrl;
        return string.Empty;
    }
    
    // Image source for caption display
    public enum ImageSourceType { Internal, PreferredVendor, OtherVendor, Placeholder }
    public ImageSourceType GetImageSource()
    {
        if (!string.IsNullOrEmpty(Item?.ImagePath))
            return ImageSourceType.Internal;
        if (!string.IsNullOrEmpty(PreferredImageUrl))
            return ImageSourceType.PreferredVendor;
        if (!string.IsNullOrEmpty(FirstVendorImageUrl))
            return ImageSourceType.OtherVendor;
        return ImageSourceType.Placeholder;
    }
    
    public bool IsLabEnvironment => _enrichmentService.IsLabEnvironment();
    
    public BuyabilityResult? BuyabilityScore { get; set; }
    public EffectiveProcurementValues? EffectiveProcurement { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }
    
    [TempData]
    public string? ErrorMessage { get; set; }
    
    // Revision inspector support
    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? RevId { get; set; }
    
    public ItemRevision? SelectedRevision { get; set; }
    
    // Revision Bar properties
    public ItemRevision? DraftRevision => Revisions?.FirstOrDefault(r => r.Status == RevisionStatus.Draft);
    public ItemRevision? CurrentReleasedRevision => Item?.CurrentReleasedRevision;
    public bool HasDraft => DraftRevision != null;
    public bool IsViewingDraft => CurrentRevision?.Status == RevisionStatus.Draft;
    public bool IsViewingHistorical => CurrentRevision != null 
        && CurrentRevision.Status != RevisionStatus.Draft 
        && CurrentRevision.Id != CurrentReleasedRevision?.Id;
    
    [BindProperty(SupportsGet = true)]
    public bool ShowCompare { get; set; }
    
    // Compare data
    public List<FieldDelta> CompareDeltas { get; set; } = new();
    
    public class FieldDelta
    {
        public string FieldName { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
            if (!await _moduleGuard.IsModuleEnabledAsync("inventory"))
                return RedirectToPage("/ModuleDisabled", new { module = "Materials" });

        await LoadLookupsAsync();

        var visibleIds = _tenantContext.VisibleCompanyIds;

        // DEF-008 follow-up: load location options for the Default Location
        // picker. Scoped to tenant-visible companies + global rows (CompanyId
        // null). Sorted by display name.
        Locations = await _db.Locations
            .Where(l => l.CompanyId == null || visibleIds.Contains(l.CompanyId ?? 0))
            .OrderBy(l => l.Name)
            .ToListAsync();

        if (id.HasValue && id.Value > 0)
        {
            Item = await _db.Items
                .Include(i => i.CurrentReleasedRevision)
                .Where(i => visibleIds.Contains(i.CompanyId ?? 0))
                .FirstOrDefaultAsync(i => i.Id == id.Value);

            if (Item == null)
            {
                return NotFound();
            }

            CurrentRevision = Item.CurrentReleasedRevision;
            Revisions = await _revisionService.GetRevisionsForItemAsync(Item.Id);
            ManufacturerParts = await _crossRefService.GetMpnsForItemAsync(Item.Id);
            VendorParts = await _crossRefService.GetVpnsForItemAsync(Item.Id);
            ApprovedVendors = await _sourcingService.GetApprovedVendorsAsync(Item.Id);
            Alternates = await _alternateService.GetAlternatesAsync(Item.Id);
            Supersession = await _supersessionService.GetSupersessionAsync(Item.Id);
            SupersededBy = await _supersessionService.GetSupersededByAsync(Item.Id);
            ResolvedCurrentItem = await _supersessionService.ResolveCurrentItemAsync(Item.Id);
            
            BuyabilityScore = await _buyabilityService.CalculateScoreWithDetailsAsync(Item.Id);
            EffectiveProcurement = await _effectiveProcurementService.GetEffectiveValuesAsync(Item.Id);
            
            // Load selected revision for inspector if RevId query param is present
            if (RevId.HasValue && RevId.Value > 0)
            {
                SelectedRevision = Revisions.FirstOrDefault(r => r.Id == RevId.Value);
            }
            
            // Compute compare deltas if ShowCompare is true and there's a draft
            if (ShowCompare && HasDraft && CurrentReleasedRevision != null)
            {
                ComputeCompareDeltas(CurrentReleasedRevision, DraftRevision!);
            }
        }
        else
        {
            Item = new Item { IsActive = true, StockUOM = "EA", Type = ItemType.Part };
            BuyabilityScore = new BuyabilityResult();
            EffectiveProcurement = new EffectiveProcurementValues();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveItemAsync(
        int? id, string partNumber, int? typeLookupValueId, string description, string? extendedDescription, string stockUom, bool isActive,
        int? leadTimeDays, decimal? minOrderQty, decimal? orderMultiple, string? purchaseUom, decimal? packQty, StockPolicy stockPolicy,
        decimal? lastPrice, string? currencyCode, DateTime? priceEffectiveDate, bool contractFlag, string? contractRef,
        int? statusLookupValueId, int? costMethodLookupValueId, int? trackingTypeLookupValueId,
        decimal? standardCost,
        int? defaultLocationId)
    {
        if (string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(stockUom))
        {
            ErrorMessage = "Part Number, Description, and UoM are required.";
            return RedirectToPage(new { id });
        }

        if (id.HasValue && id.Value > 0)
        {
            var result = await _itemMaster.UpdateItemAsync(new UpdateItemRequest(
                ItemId: id.Value,
                PartNumber: partNumber,
                TypeLookupValueId: typeLookupValueId,
                Description: description,
                ExtendedDescription: extendedDescription,
                StockUom: stockUom,
                IsActive: isActive,
                LeadTimeDays: leadTimeDays,
                MinOrderQty: minOrderQty,
                OrderMultiple: orderMultiple,
                PurchaseUom: purchaseUom,
                PackQty: packQty,
                StockPolicy: stockPolicy,
                LastPrice: lastPrice,
                CurrencyCode: currencyCode,
                PriceEffectiveDate: priceEffectiveDate,
                ContractFlag: contractFlag,
                ContractRef: contractRef,
                StatusLookupValueId: statusLookupValueId,
                CostMethodLookupValueId: costMethodLookupValueId,
                TrackingTypeLookupValueId: trackingTypeLookupValueId,
                StandardCost: standardCost,
                DefaultLocationId: defaultLocationId), HttpContext.RequestAborted);

            if (result.IsFailure)
            {
                if (string.Equals(result.Error, "Item not found in scope.", StringComparison.Ordinal))
                    return NotFound();
                ErrorMessage = result.Error;
                return RedirectToPage(new { id });
            }

            SuccessMessage = "Item updated successfully.";
            return RedirectToPage(new { id = result.Value!.Id });
        }
        else
        {
            var result = await _itemMaster.CreateItemAsync(new CreateItemRequest(
                PartNumber: partNumber,
                TypeLookupValueId: typeLookupValueId,
                Description: description,
                ExtendedDescription: extendedDescription,
                StockUom: stockUom,
                IsActive: isActive,
                LeadTimeDays: leadTimeDays,
                MinOrderQty: minOrderQty,
                OrderMultiple: orderMultiple,
                PurchaseUom: purchaseUom,
                PackQty: packQty,
                StockPolicy: stockPolicy,
                LastPrice: lastPrice,
                CurrencyCode: currencyCode,
                PriceEffectiveDate: priceEffectiveDate,
                ContractFlag: contractFlag,
                ContractRef: contractRef,
                StatusLookupValueId: statusLookupValueId,
                CostMethodLookupValueId: costMethodLookupValueId,
                TrackingTypeLookupValueId: trackingTypeLookupValueId,
                StandardCost: standardCost,
                DefaultLocationId: defaultLocationId), HttpContext.RequestAborted);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return RedirectToPage();
            }

            SuccessMessage = "Item created successfully.";
            return RedirectToPage(new { id = result.Value!.Id });
        }
    }

    public async Task<IActionResult> OnPostCreateDraftAsync(int itemId)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            var draft = await _revisionService.CreateDraftFromItemAsync(itemId, "New draft revision", User.Identity?.Name);
            SuccessMessage = $"Draft revision {draft.RevisionCode} created.";
            // Navigate to Revisions tab with new draft selected in inspector
            return RedirectToPage(new { id = itemId, tab = "revisions", revId = draft.Id });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage(new { id = itemId, tab = "revisions" });
        }
    }

    public async Task<IActionResult> OnPostReleaseDraftAsync(int revisionId, string? changeReason)
    {
        try
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var revision = await _db.ItemRevisions.Include(r => r.Item).Where(r => r.Id == revisionId && r.Item != null && visibleIds.Contains(r.Item.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (revision == null)
            {
                ErrorMessage = "Revision not found.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(changeReason))
            {
                ErrorMessage = "Change reason is required to release a revision.";
                return RedirectToPage(new { id = revision.ItemId, tab = "revisions", revId = revisionId });
            }

            var released = await _revisionService.ReleaseRevisionAsync(revisionId, User.Identity?.Name, changeReason);
            SuccessMessage = $"Revision {released.RevisionCode} released successfully.";
            return RedirectToPage(new { id = revision.ItemId, tab = "revisions", revId = released.Id });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage();
        }
    }
    
    public async Task<IActionResult> OnPostSaveRevisionAsync(int revisionId, string revisionCode, string name, string? description, string? changeReason)
    {
        try
        {
            var result = await _itemMaster.SaveRevisionAsync(
                new SaveRevisionRequest(revisionId, revisionCode, name, description, changeReason),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return RedirectToPage();
            }
            var revision = result.Value!;
            SuccessMessage = $"Revision {revision.RevisionCode} saved.";
            return RedirectToPage(new { id = revision.ItemId, tab = "revisions", revId = revisionId });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage();
        }
    }
    
    public async Task<IActionResult> OnPostObsoleteRevisionAsync(int revisionId)
    {
        try
        {
            var result = await _itemMaster.ObsoleteRevisionAsync(revisionId, HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return RedirectToPage();
            }
            var revision = result.Value!;
            SuccessMessage = $"Revision {revision.RevisionCode} marked obsolete.";
            return RedirectToPage(new { id = revision.ItemId, tab = "revisions" });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostDeleteRevisionAsync(int revisionId)
    {
        try
        {
            var result = await _itemMaster.DeleteDraftRevisionAsync(revisionId, HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return RedirectToPage();
            }
            SuccessMessage = "Draft revision deleted.";
            return RedirectToPage(new { id = result.Value, tab = "revisions" });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostAddMpnAsync(int itemId, int manufacturerId, string mfrPartNumber, string? mpnDescription)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            var mpn = await _crossRefService.AddMpnAsync(itemId, manufacturerId, mfrPartNumber, mpnDescription, User.Identity?.Name);
            SuccessMessage = $"MPN {mpn.MfrPartNumber} added successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostAddVpnAsync(int itemId, int vendorId, string vendorPartNumber, int? mpnId, string? catalogUrl, string? datasheetUrl, string? externalImageUrl)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            var vpn = await _crossRefService.AddVpnAsync(itemId, vendorId, vendorPartNumber, mpnId, User.Identity?.Name);
            // Update catalog/image URLs if provided — via service per ADR-025.
            var urlResult = await _itemMaster.SetVpnUrlsAsync(
                new SetVpnUrlsRequest(vpn.Id, catalogUrl, datasheetUrl, externalImageUrl),
                HttpContext.RequestAborted);
            if (urlResult.IsFailure)
            {
                ErrorMessage = urlResult.Error;
            }
            else
            {
                SuccessMessage = $"VPN {vpn.VendorPartNumber} added successfully.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId, tab = "vpns" });
    }

    private static string? ValidateHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            return url;
        return null;
    }

    public async Task<IActionResult> OnPostAddApprovedVendorAsync(int itemId, int vendorId, int statusLookupValueId, bool isPreferred, string? notes)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            var resolvedStatus = RevisionStatus.Draft;
            int? resolvedStatusLvId = statusLookupValueId > 0 ? statusLookupValueId : (int?)null;
            var statusLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, statusLookupValueId);
            if (statusLv != null)
            {
                resolvedStatusLvId = statusLv.Id;
                if (int.TryParse(statusLv.Code, out var enumVal))
                    resolvedStatus = (RevisionStatus)enumVal;
            }

            var avlStatus = (AvlApprovalStatus)(int)resolvedStatus;
            await _sourcingService.SetApprovedVendorAsync(itemId, vendorId, avlStatus, isPreferred, notes);
            SuccessMessage = "Approved vendor added successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostRemoveApprovedVendorAsync(int itemId, int vendorId)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            await _sourcingService.RemoveApprovedVendorAsync(itemId, vendorId);
            SuccessMessage = "Approved vendor removed.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostSetPreferredVendorAsync(int itemId, int vendorId)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            await _sourcingService.SetPreferredVendorAsync(itemId, vendorId);
            SuccessMessage = "Preferred vendor updated.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostAddAlternateAsync(int itemId, int alternateItemId, AlternateType alternateType, int rank, string? reason, bool isApproved)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            await _alternateService.AddAlternateAsync(itemId, alternateItemId, alternateType, rank, reason, isApproved);
            SuccessMessage = "Alternate item added successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostRemoveAlternateAsync(int itemId, int alternateItemId)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            await _alternateService.RemoveAlternateAsync(itemId, alternateItemId);
            SuccessMessage = "Alternate item removed.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostSetSupersessionAsync(int itemId, int newItemId, DateTime? effectiveFromUtc, string? reason)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            await _supersessionService.SetSupersessionAsync(itemId, newItemId, effectiveFromUtc, reason);
            SuccessMessage = "Supersession set successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostRemoveSupersessionAsync(int itemId)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        try
        {
            await _supersessionService.RemoveSupersessionAsync(itemId);
            SuccessMessage = "Supersession removed.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostUploadImageAsync(int itemId, IFormFile imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            ErrorMessage = "Please select an image file to upload.";
            return RedirectToPage(new { id = itemId });
        }

        var visibleIds = _tenantContext.VisibleCompanyIds;
        var item = await _db.Items.Where(i => i.Id == itemId && visibleIds.Contains(i.CompanyId ?? 0)).FirstOrDefaultAsync();
        if (item == null)
            return NotFound();

        _imageService.DeleteImage(item.ImagePath);

        var newPath = await _imageService.UploadImageAsync(itemId, imageFile);
        if (string.IsNullOrEmpty(newPath))
        {
            ErrorMessage = "Failed to upload image. Please ensure the file is a valid image (JPG, PNG, WebP) under 10MB.";
            return RedirectToPage(new { id = itemId });
        }

        var setResult = await _itemMaster.SetItemImagePathAsync(itemId, newPath, HttpContext.RequestAborted);
        if (setResult.IsFailure)
        {
            ErrorMessage = setResult.Error;
            return RedirectToPage(new { id = itemId });
        }
        SuccessMessage = "Image uploaded successfully.";
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostRemoveImageAsync(int itemId)
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        var item = await _db.Items.Where(i => i.Id == itemId && visibleIds.Contains(i.CompanyId ?? 0)).FirstOrDefaultAsync();
        if (item == null)
            return NotFound();

        _imageService.DeleteImage(item.ImagePath);
        var clearResult = await _itemMaster.ClearItemImagePathAsync(itemId, HttpContext.RequestAborted);
        if (clearResult.IsFailure)
        {
            ErrorMessage = clearResult.Error;
            return RedirectToPage(new { id = itemId });
        }
        SuccessMessage = "Image removed.";
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostEnrichVpnAsync(int itemId, int vpnId)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        if (!_enrichmentService.IsLabEnvironment())
        {
            ErrorMessage = "Catalog enrichment is only available in LAB/Development environment.";
            return RedirectToPage(new { id = itemId });
        }

        var vpn = await _db.Set<VendorItemPart>().Where(v => v.Id == vpnId && v.ItemId == itemId).FirstOrDefaultAsync();
        if (vpn == null)
            return NotFound();

        var catalogUrl = vpn.CatalogUrl ?? vpn.ProductPageUrl;
        if (string.IsNullOrEmpty(catalogUrl))
        {
            ErrorMessage = "No catalog URL to enrich from. Please enter a catalog URL first.";
            return RedirectToPage(new { id = itemId });
        }

        var enrich = await _enrichmentService.EnrichFromUrlAsync(catalogUrl);

        var applyResult = await _itemMaster.ApplyVpnEnrichmentAsync(
            new ApplyVpnEnrichmentRequest(
                VpnId: vpnId,
                Status: enrich.Status,
                CanonicalUrl: enrich.CanonicalUrl,
                ImageUrl: enrich.ImageUrl,
                Mpn: enrich.Mpn,
                Sku: enrich.Sku),
            HttpContext.RequestAborted);

        if (applyResult.IsFailure)
        {
            ErrorMessage = applyResult.Error;
            return RedirectToPage(new { id = itemId });
        }

        if (enrich.Status == "Success")
            SuccessMessage = "Catalog metadata enriched successfully.";
        else if (enrich.Status == "NoMetadata")
            SuccessMessage = "Enrichment complete but no structured metadata found on the page.";
        else
            ErrorMessage = $"Enrichment failed: {enrich.ErrorMessage ?? enrich.Status}";

        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostUpdateVpnCatalogAsync(int itemId, int vpnId, string? catalogUrl, string? imageUrl)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        var result = await _itemMaster.UpdateVpnCatalogInfoAsync(
            new UpdateVpnCatalogRequest(itemId, vpnId, catalogUrl, imageUrl),
            HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            if (string.Equals(result.Error, "VendorItemPart not found.", StringComparison.Ordinal))
                return NotFound();
            ErrorMessage = result.Error;
            return RedirectToPage(new { id = itemId });
        }
        SuccessMessage = "Vendor part catalog info updated.";
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostCopyExternalImageAsync(int itemId, int vpnId)
    {
        var result = await _itemMaster.SetItemExternalImageFromVpnAsync(itemId, vpnId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            if (string.Equals(result.Error, "Item or VendorItemPart not found.", StringComparison.Ordinal))
                return NotFound();
            ErrorMessage = result.Error;
            return RedirectToPage(new { id = itemId });
        }
        SuccessMessage = "External image URL set as item fallback image.";
        return RedirectToPage(new { id = itemId });
    }

    private async Task LoadLookupsAsync()
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        Manufacturers = await _db.Manufacturers.OrderBy(m => m.Name).ToListAsync();
        Vendors = await _db.Vendors.Where(v => v.IsActive && visibleIds.Contains(v.CompanyId ?? 0)).OrderBy(v => v.Name).ToListAsync();
        AllItems = await _db.Items.Where(i => i.IsActive && visibleIds.Contains(i.CompanyId ?? 0)).OrderBy(i => i.PartNumber).ToListAsync();

        var isActive = Item?.IsActive ?? true;
        ActiveInactiveOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ActiveInactive", isActive ? "true" : "false", "");
        TypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ItemType", Item?.TypeLookupValueId, "");
        StatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ItemStatus", Item?.StatusLookupValueId, "");
        CostMethodOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CostMethod", Item?.CostMethodLookupValueId, "");
        TrackingTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "TrackingType", Item?.TrackingTypeLookupValueId, "");
        RevisionStatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RevisionStatus", null, "");
    }
    
    private void ComputeCompareDeltas(ItemRevision current, ItemRevision draft)
    {
        CompareDeltas.Clear();
        
        void AddDelta(string fieldName, string? oldVal, string? newVal)
        {
            if (oldVal != newVal)
            {
                CompareDeltas.Add(new FieldDelta 
                { 
                    FieldName = fieldName, 
                    OldValue = string.IsNullOrEmpty(oldVal) ? "(empty)" : oldVal, 
                    NewValue = string.IsNullOrEmpty(newVal) ? "(empty)" : newVal 
                });
            }
        }
        
        AddDelta("Name", current.Name, draft.Name);
        AddDelta("Description", current.Description, draft.Description);
        AddDelta("Change Reason", current.ChangeReason, draft.ChangeReason);
        
        if (Item != null)
        {
            AddDelta("UOM", Item.StockUOM, Item.StockUOM);
            AddDelta("Lead Time (Days)", Item.LeadTimeDays.ToString(), Item.LeadTimeDays.ToString());
            AddDelta("Min Order Qty", Item.MinOrderQty?.ToString("0.##") ?? "", Item.MinOrderQty?.ToString("0.##") ?? "");
            AddDelta("Order Multiple", Item.OrderMultiple?.ToString("0.##") ?? "", Item.OrderMultiple?.ToString("0.##") ?? "");
            AddDelta("Pack Qty", Item.PackQty?.ToString("0.##") ?? "", Item.PackQty?.ToString("0.##") ?? "");
            AddDelta("Stock Policy", Item.StockPolicy.ToString(), Item.StockPolicy.ToString());
        }
    }
}
