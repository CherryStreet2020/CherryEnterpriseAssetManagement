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

namespace Abs.FixedAssets.Pages.Materials;

public class ItemsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IItemCrossReferenceService _crossRefService;
    private readonly IBuyabilityScoreService _buyabilityService;
    private readonly IEffectiveProcurementService _effectiveProcurementService;
    private readonly ILookupService _lookupService;
    private readonly IModuleGuardService _moduleGuard;
    private readonly ITenantContext _tenantContext;
    private const int PageSize = 25;

    public ItemsModel(AppDbContext db, IItemCrossReferenceService crossRefService, 
        IBuyabilityScoreService buyabilityService, IEffectiveProcurementService effectiveProcurementService,
        ILookupService lookupService, IModuleGuardService moduleGuard, ITenantContext tenantContext)
    {
        _db = db;
        _crossRefService = crossRefService;
        _buyabilityService = buyabilityService;
        _effectiveProcurementService = effectiveProcurementService;
        _lookupService = lookupService;
        _moduleGuard = moduleGuard;
        _tenantContext = tenantContext;
    }

    public List<Item> Items { get; set; } = new();
    public List<Vendor> Vendors { get; set; } = new();
    public List<SelectListItem> StatusFilterOptions { get; set; } = new();
    public List<SelectListItem> TypeFilterOptions { get; set; } = new();
    public List<ItemResolutionResult>? SearchResults { get; set; }
    
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int WithRevisionsCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public bool HasMorePages { get; set; }
    
    private Dictionary<int, int> VpnCounts { get; set; } = new();
    private Dictionary<int, int> MpnCounts { get; set; } = new();
    private Dictionary<int, string?> PreferredVendorNames { get; set; } = new();
    private Dictionary<int, string?> PreferredImageUrls { get; set; } = new();
    private Dictionary<int, string?> PreferredCatalogUrls { get; set; } = new();
    public Dictionary<int, BuyabilityResult> BuyabilityScores { get; set; } = new();
    public Dictionary<int, EffectiveProcurementValues> EffectiveValues { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? VendorIdFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? SortBy { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? SortDir { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public new int Page { get; set; } = 1;
    
    [TempData]
    public string? SuccessMessage { get; set; }
    
    public bool HasFilters => !string.IsNullOrEmpty(SearchQuery) || VendorIdFilter.HasValue || 
                              !string.IsNullOrEmpty(StatusFilter) || !string.IsNullOrEmpty(TypeFilter);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _moduleGuard.IsModuleEnabledAsync("inventory"))
            return RedirectToPage("/ModuleDisabled", new { module = "Materials" });

        CurrentPage = Page > 0 ? Page : 1;

        StatusFilterOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ActiveInactive", StatusFilter, "All Status");
        TypeFilterOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ItemType", TypeFilter, "All Types");

        var visibleIds = _tenantContext.VisibleCompanyIds;

        Vendors = await _db.Vendors
            .Where(v => v.IsActive && visibleIds.Contains(v.CompanyId ?? 0))
            .OrderBy(v => v.Name)
            .ThenBy(v => v.Id)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults = await _crossRefService.SearchItemsAsync(SearchQuery, VendorIdFilter, 50);
        }

        var baseQuery = _db.Items.Where(i => visibleIds.Contains(i.CompanyId ?? 0));
        
        TotalCount = await baseQuery.CountAsync();
        ActiveCount = await baseQuery.CountAsync(i => i.IsActive);
        WithRevisionsCount = await baseQuery.CountAsync(i => i.CurrentReleasedRevisionId.HasValue);

        var query = _db.Items
            .Include(i => i.CurrentReleasedRevision)
            .Include(i => i.PrimaryVendor)
            .Where(i => visibleIds.Contains(i.CompanyId ?? 0));
        
        if (StatusFilter == "Active")
            query = query.Where(i => i.IsActive);
        else if (StatusFilter == "Inactive")
            query = query.Where(i => !i.IsActive);
        
        if (!string.IsNullOrEmpty(TypeFilter) && Enum.TryParse<ItemType>(TypeFilter, out var type))
            query = query.Where(i => i.Type == type);
        
        if (VendorIdFilter.HasValue)
            query = query.Where(i => i.PrimaryVendorId == VendorIdFilter);

        query = ApplySorting(query);

        var totalFilteredCount = await query.CountAsync();
        HasMorePages = (CurrentPage * PageSize) < totalFilteredCount;

        Items = await query
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        await HydrateListDataAsync();

        return Page();
    }

    private IQueryable<Item> ApplySorting(IQueryable<Item> query)
    {
        var sortDir = SortDir?.ToLower() == "desc";
        
        return SortBy?.ToLower() switch
        {
            "description" => sortDir 
                ? query.OrderByDescending(i => i.Description).ThenBy(i => i.PartNumber).ThenBy(i => i.Id)
                : query.OrderBy(i => i.Description).ThenBy(i => i.PartNumber).ThenBy(i => i.Id),
            "type" => sortDir
                ? query.OrderByDescending(i => i.Type).ThenBy(i => i.PartNumber).ThenBy(i => i.Id)
                : query.OrderBy(i => i.Type).ThenBy(i => i.PartNumber).ThenBy(i => i.Id),
            "status" => sortDir
                ? query.OrderByDescending(i => i.IsActive).ThenBy(i => i.PartNumber).ThenBy(i => i.Id)
                : query.OrderBy(i => i.IsActive).ThenBy(i => i.PartNumber).ThenBy(i => i.Id),
            _ => query.OrderBy(i => i.PartNumber).ThenBy(i => i.Id)
        };
    }

    private async Task HydrateListDataAsync()
    {
        if (!Items.Any()) return;
        
        var itemIds = Items.Select(i => i.Id).ToList();
        
        var vpnData = await _db.VendorItemParts
            .Where(v => itemIds.Contains(v.ItemId) && v.IsActive)
            .GroupBy(v => v.ItemId)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToListAsync();
        VpnCounts = vpnData.ToDictionary(x => x.ItemId, x => x.Count);
        
        var mpnData = await _db.ItemManufacturerParts
            .Where(m => itemIds.Contains(m.ItemId) && m.IsActive)
            .GroupBy(m => m.ItemId)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToListAsync();
        MpnCounts = mpnData.ToDictionary(x => x.ItemId, x => x.Count);
        
        var preferredVendorData = await _db.ItemApprovedVendors
            .Where(a => itemIds.Contains(a.ItemId) && a.IsPreferred && a.ApprovalStatus == AvlApprovalStatus.Approved)
            .Include(a => a.Vendor)
            .Select(a => new { a.ItemId, VendorName = a.Vendor != null ? a.Vendor.Name : null })
            .ToListAsync();
        PreferredVendorNames = preferredVendorData.ToDictionary(x => x.ItemId, x => x.VendorName);

        // Load preferred/first available catalog and image URLs
        var vendorPartsWithUrls = await _db.VendorItemParts
            .Where(v => itemIds.Contains(v.ItemId) && v.IsActive && (v.ImageUrl != null || v.ProductPageUrl != null))
            .Select(v => new { v.ItemId, v.Preferred, v.ImageUrl, v.ProductPageUrl })
            .ToListAsync();
        
        foreach (var itemId in itemIds)
        {
            var partsForItem = vendorPartsWithUrls.Where(v => v.ItemId == itemId).ToList();
            var preferred = partsForItem.FirstOrDefault(v => v.Preferred);
            var first = partsForItem.FirstOrDefault();
            
            PreferredImageUrls[itemId] = preferred?.ImageUrl ?? first?.ImageUrl;
            PreferredCatalogUrls[itemId] = preferred?.ProductPageUrl ?? first?.ProductPageUrl;
        }
        
        EffectiveValues = await _effectiveProcurementService.GetEffectiveValuesForItemsAsync(itemIds);
        
        var avlData = await _db.ItemApprovedVendors
            .Where(a => itemIds.Contains(a.ItemId) && a.ApprovalStatus == AvlApprovalStatus.Approved)
            .GroupBy(a => a.ItemId)
            .Select(g => new { ItemId = g.Key, Count = g.Count(), HasPreferred = g.Any(a => a.IsPreferred) })
            .ToListAsync();
        var avlCounts = avlData.ToDictionary(x => x.ItemId, x => x.Count);
        var hasPreferredMap = avlData.ToDictionary(x => x.ItemId, x => x.HasPreferred);
        
        var catalogData = await _db.VendorItemParts
            .Where(v => itemIds.Contains(v.ItemId) && v.IsActive && !string.IsNullOrEmpty(v.ProductPageUrl))
            .Select(v => v.ItemId)
            .Distinct()
            .ToListAsync();
        var hasCatalogMap = catalogData.ToHashSet();
        
        foreach (var item in Items)
        {
            var vpnCount = VpnCounts.GetValueOrDefault(item.Id);
            var avlCount = avlCounts.GetValueOrDefault(item.Id);
            var hasPreferred = hasPreferredMap.GetValueOrDefault(item.Id);
            var hasCatalog = hasCatalogMap.Contains(item.Id);
            BuyabilityScores[item.Id] = _buyabilityService.CalculateScore(item, vpnCount, avlCount, hasPreferred, hasCatalog);
        }
    }

    public int GetVpnCount(int itemId)
    {
        return VpnCounts.TryGetValue(itemId, out var count) ? count : 0;
    }
    
    public int GetMpnCount(int itemId)
    {
        return MpnCounts.TryGetValue(itemId, out var count) ? count : 0;
    }
    
    public string? GetPreferredVendorName(int itemId)
    {
        return PreferredVendorNames.TryGetValue(itemId, out var name) ? name : null;
    }
    
    public string? GetPreferredImageUrl(int itemId)
    {
        return PreferredImageUrls.TryGetValue(itemId, out var url) ? url : null;
    }
    
    public string? GetPreferredCatalogUrl(int itemId)
    {
        return PreferredCatalogUrls.TryGetValue(itemId, out var url) ? url : null;
    }
    
    public BuyabilityResult? GetBuyabilityScore(int itemId)
    {
        return BuyabilityScores.TryGetValue(itemId, out var score) ? score : null;
    }
    
    public EffectiveProcurementValues? GetEffectiveValues(int itemId)
    {
        return EffectiveValues.TryGetValue(itemId, out var values) ? values : null;
    }
    
    public string GetPageUrl(int page)
    {
        var queryParams = new List<string>();
        
        if (!string.IsNullOrEmpty(SearchQuery))
            queryParams.Add($"search={Uri.EscapeDataString(SearchQuery)}");
        if (!string.IsNullOrEmpty(StatusFilter))
            queryParams.Add($"statusFilter={Uri.EscapeDataString(StatusFilter)}");
        if (!string.IsNullOrEmpty(TypeFilter))
            queryParams.Add($"typeFilter={Uri.EscapeDataString(TypeFilter)}");
        if (VendorIdFilter.HasValue)
            queryParams.Add($"vendorId={VendorIdFilter}");
        if (!string.IsNullOrEmpty(SortBy))
            queryParams.Add($"sortBy={Uri.EscapeDataString(SortBy)}");
        if (!string.IsNullOrEmpty(SortDir))
            queryParams.Add($"sortDir={Uri.EscapeDataString(SortDir)}");
        
        queryParams.Add($"page={page}");
        
        return "/Materials/Items?" + string.Join("&", queryParams);
    }
}
