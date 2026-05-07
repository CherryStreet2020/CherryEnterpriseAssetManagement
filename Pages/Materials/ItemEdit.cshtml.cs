using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Items;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Navigation;

namespace Abs.FixedAssets.Pages.Materials;

public class ItemEditModel : PageModel
{
    private readonly AppDbContext _db;
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
        decimal? standardCost)
    {
        if (string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(stockUom))
        {
            ErrorMessage = "Part Number, Description, and UoM are required.";
            return RedirectToPage(new { id });
        }

        var resolvedType = ItemType.Part;
        if (typeLookupValueId.HasValue)
        {
            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, typeLookupValueId.Value);
            if (lv != null && int.TryParse(lv.Code, out var enumVal))
                resolvedType = (ItemType)enumVal;
        }

        var resolvedStatus = ItemStatus.Active;
        if (statusLookupValueId.HasValue)
        {
            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, statusLookupValueId.Value);
            if (lv != null && int.TryParse(lv.Code, out var enumVal))
                resolvedStatus = (ItemStatus)enumVal;
        }

        var resolvedCostMethod = CostMethod.Average;
        if (costMethodLookupValueId.HasValue)
        {
            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, costMethodLookupValueId.Value);
            if (lv != null && int.TryParse(lv.Code, out var enumVal))
                resolvedCostMethod = (CostMethod)enumVal;
        }

        var resolvedTrackingType = TrackingType.None;
        if (trackingTypeLookupValueId.HasValue)
        {
            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, trackingTypeLookupValueId.Value);
            if (lv != null && int.TryParse(lv.Code, out var enumVal))
                resolvedTrackingType = (TrackingType)enumVal;
        }

        var visibleIds = _tenantContext.VisibleCompanyIds;

        if (id.HasValue && id.Value > 0)
        {
            var existing = await _db.Items.Where(i => i.Id == id.Value && visibleIds.Contains(i.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (existing == null)
            {
                return NotFound();
            }

            existing.Description = description;
            existing.ExtendedDescription = extendedDescription;
            existing.Type = resolvedType;
            existing.TypeLookupValueId = typeLookupValueId;
            existing.Status = resolvedStatus;
            existing.StatusLookupValueId = statusLookupValueId;
            existing.CostMethod = resolvedCostMethod;
            existing.CostMethodLookupValueId = costMethodLookupValueId;
            existing.TrackingType = resolvedTrackingType;
            existing.TrackingTypeLookupValueId = trackingTypeLookupValueId;
            existing.StockUOM = stockUom;
            existing.IsActive = isActive;
            
            existing.LeadTimeDays = leadTimeDays ?? existing.LeadTimeDays;
            existing.MinOrderQty = minOrderQty;
            existing.OrderMultiple = orderMultiple;
            existing.PurchaseUOM = purchaseUom ?? existing.PurchaseUOM;
            existing.PackQty = packQty;
            existing.StockPolicy = stockPolicy;
            existing.LastPrice = lastPrice;
            existing.CurrencyCode = currencyCode;
            existing.PriceEffectiveDate = priceEffectiveDate;
            existing.ContractFlag = contractFlag;
            existing.ContractRef = contractRef;
            if (standardCost.HasValue)
                existing.StandardCost = standardCost.Value;

            await _db.SaveChangesAsync();
            SuccessMessage = "Item updated successfully.";
            return RedirectToPage(new { id = existing.Id });
        }
        else
        {
            var existingPn = await _db.Items.Where(i => visibleIds.Contains(i.CompanyId ?? 0)).AnyAsync(i => i.PartNumber.ToLower() == partNumber.ToLower());
            if (existingPn)
            {
                ErrorMessage = "An item with this Part Number already exists.";
                return RedirectToPage();
            }

            var newItem = new Item
            {
                PartNumber = partNumber,
                Description = description,
                ExtendedDescription = extendedDescription,
                Type = resolvedType,
                TypeLookupValueId = typeLookupValueId,
                Status = resolvedStatus,
                StatusLookupValueId = statusLookupValueId,
                CostMethod = resolvedCostMethod,
                CostMethodLookupValueId = costMethodLookupValueId,
                TrackingType = resolvedTrackingType,
                TrackingTypeLookupValueId = trackingTypeLookupValueId,
                StockUOM = stockUom,
                IsActive = isActive,
                LeadTimeDays = leadTimeDays ?? 0,
                MinOrderQty = minOrderQty,
                OrderMultiple = orderMultiple,
                PurchaseUOM = purchaseUom!,
                PackQty = packQty,
                StockPolicy = stockPolicy,
                LastPrice = lastPrice,
                CurrencyCode = currencyCode ?? "USD",
                PriceEffectiveDate = priceEffectiveDate,
                ContractFlag = contractFlag,
                ContractRef = contractRef,
                StandardCost = standardCost ?? 0,
                CompanyId = _tenantContext.CompanyId
            };

            _db.Items.Add(newItem);
            await _db.SaveChangesAsync();

            SuccessMessage = "Item created successfully.";
            return RedirectToPage(new { id = newItem.Id });
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
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var revision = await _db.ItemRevisions.Include(r => r.Item).Where(r => r.Id == revisionId && r.Item != null && visibleIds.Contains(r.Item.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (revision == null)
            {
                ErrorMessage = "Revision not found.";
                return RedirectToPage();
            }
            
            if (revision.Status != RevisionStatus.Draft)
            {
                ErrorMessage = "Only draft revisions can be edited.";
                return RedirectToPage(new { id = revision.ItemId, tab = "revisions", revId = revisionId });
            }
            
            revision.RevisionCode = revisionCode;
            revision.Name = name;
            revision.Description = description;
            revision.ChangeReason = changeReason;
            
            await _db.SaveChangesAsync();
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
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var revision = await _db.ItemRevisions
                .Include(r => r.Item)
                .Where(r => r.Id == revisionId && r.Item != null && visibleIds.Contains(r.Item.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (revision == null)
            {
                ErrorMessage = "Revision not found.";
                return RedirectToPage();
            }
            
            if (revision.Status == RevisionStatus.Draft)
            {
                ErrorMessage = "Cannot obsolete a draft. Delete it instead.";
                return RedirectToPage(new { id = revision.ItemId, tab = "revisions", revId = revisionId });
            }
            
            // Guard: cannot obsolete the current released revision
            if (revision.Item?.CurrentReleasedRevisionId == revisionId)
            {
                ErrorMessage = "Cannot obsolete the current released revision. Release a new revision first.";
                return RedirectToPage(new { id = revision.ItemId, tab = "revisions", revId = revisionId });
            }
            
            revision.Status = RevisionStatus.Obsolete;
            // Keep the FK column in sync with the legacy enum write.
            var obsoleteLv = await _lookupService.GetValueByCodeAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId,
                "RevisionStatus", ((int)RevisionStatus.Obsolete).ToString());
            if (obsoleteLv != null)
                revision.StatusLookupValueId = obsoleteLv.Id;

            revision.ObsoletedAtUtc = DateTime.UtcNow;
            revision.EffectiveToUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
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
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var revision = await _db.ItemRevisions.Include(r => r.Item).Where(r => r.Id == revisionId && r.Item != null && visibleIds.Contains(r.Item.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (revision == null)
            {
                ErrorMessage = "Revision not found.";
                return RedirectToPage();
            }
            
            if (revision.Status != RevisionStatus.Draft)
            {
                ErrorMessage = "Only draft revisions can be deleted.";
                return RedirectToPage(new { id = revision.ItemId, tab = "revisions" });
            }
            
            var itemId = revision.ItemId;
            _db.ItemRevisions.Remove(revision);
            await _db.SaveChangesAsync();
            SuccessMessage = "Draft revision deleted.";
            return RedirectToPage(new { id = itemId, tab = "revisions" });
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
            // Update catalog/image URLs if provided
            if (!string.IsNullOrEmpty(catalogUrl) || !string.IsNullOrEmpty(datasheetUrl) || !string.IsNullOrEmpty(externalImageUrl))
            {
                vpn.CatalogUrl = ValidateHttpsUrl(catalogUrl);
                vpn.DatasheetUrl = ValidateHttpsUrl(datasheetUrl);
                vpn.ExternalImageUrl = ValidateHttpsUrl(externalImageUrl);
                await _db.SaveChangesAsync();
            }
            SuccessMessage = $"VPN {vpn.VendorPartNumber} added successfully.";
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

        item.ImagePath = newPath;
        await _db.SaveChangesAsync();
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
        item.ImagePath = null;
        await _db.SaveChangesAsync();
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

        var result = await _enrichmentService.EnrichFromUrlAsync(catalogUrl);
        vpn.LastEnrichedUtc = DateTime.UtcNow;
        vpn.LastEnrichStatus = result.Status;

        if (result.Status == "Success" || result.Status == "NoMetadata")
        {
            if (!string.IsNullOrEmpty(result.CanonicalUrl))
                vpn.CatalogUrl = result.CanonicalUrl;
            if (!string.IsNullOrEmpty(result.ImageUrl))
                vpn.ExternalImageUrl = result.ImageUrl;
            if (!string.IsNullOrEmpty(result.Mpn))
                vpn.ExtractedMpn = result.Mpn;
            if (!string.IsNullOrEmpty(result.Sku))
                vpn.ExtractedSku = result.Sku;
        }

        await _db.SaveChangesAsync();

        if (result.Status == "Success")
            SuccessMessage = "Catalog metadata enriched successfully.";
        else if (result.Status == "NoMetadata")
            SuccessMessage = "Enrichment complete but no structured metadata found on the page.";
        else
            ErrorMessage = $"Enrichment failed: {result.ErrorMessage ?? result.Status}";

        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostUpdateVpnCatalogAsync(int itemId, int vpnId, string? catalogUrl, string? imageUrl)
    {
        if (await LoadItemInScopeAsync(itemId) == null) return NotFound();
        var vpn = await _db.Set<VendorItemPart>().Where(v => v.Id == vpnId && v.ItemId == itemId).FirstOrDefaultAsync();
        if (vpn == null)
            return NotFound();

        vpn.CatalogUrl = ValidateHttpsUrl(catalogUrl);
        if (!string.IsNullOrEmpty(imageUrl))
            vpn.ExternalImageUrl = ValidateHttpsUrl(imageUrl);
        vpn.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        SuccessMessage = "Vendor part catalog info updated.";
        return RedirectToPage(new { id = itemId });
    }

    public async Task<IActionResult> OnPostCopyExternalImageAsync(int itemId, int vpnId)
    {
        var visibleIds = _tenantContext.VisibleCompanyIds;
        var item = await _db.Items.Where(i => i.Id == itemId && visibleIds.Contains(i.CompanyId ?? 0)).FirstOrDefaultAsync();
        var vpn = await _db.Set<VendorItemPart>().Where(v => v.Id == vpnId && v.ItemId == itemId).FirstOrDefaultAsync();
        if (item == null || vpn == null)
            return NotFound();

        var externalUrl = vpn.ExternalImageUrl ?? vpn.ImageUrl;
        if (string.IsNullOrEmpty(externalUrl))
        {
            ErrorMessage = "No external image URL available to copy.";
            return RedirectToPage(new { id = itemId });
        }

        item.ExternalImageUrl = externalUrl;
        await _db.SaveChangesAsync();
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
