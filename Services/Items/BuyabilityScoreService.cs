using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Items;

public interface IBuyabilityScoreService
{
    Task<BuyabilityResult> CalculateScoreAsync(int itemId);
    Task<BuyabilityResult> CalculateScoreWithDetailsAsync(int itemId);
    BuyabilityResult CalculateScore(Item item, int vpnCount = 0, int avlCount = 0, bool hasPreferredVendor = false, bool hasCatalogUrl = false);
}

public class BuyabilityResult
{
    public int Score { get; set; }
    public List<BuyabilityFactor> Factors { get; set; } = new();
    
    public string Tier => Score switch
    {
        >= 90 => "Excellent",
        >= 75 => "Good",
        >= 60 => "Fair",
        >= 40 => "Poor",
        _ => "Incomplete"
    };
    
    public string TierBadgeClass => Tier switch
    {
        "Excellent" => "badge-success",
        "Good" => "badge-info",
        "Fair" => "badge-warning",
        "Poor" => "badge-secondary",
        _ => "badge-danger"
    };
    
    public string Grade => Score switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _ => "F"
    };
    
    public string GradeClass => Grade switch
    {
        "A" => "badge-success",
        "B" => "badge-info",
        "C" => "badge-warning",
        "D" => "badge-secondary",
        _ => "badge-danger"
    };
    
    public Dictionary<string, CategoryBreakdown> Categories { get; set; } = new();
    
    public List<BuyabilityFactor> MissingFactors => Factors.Where(f => !f.IsMet).OrderBy(f => f.Category).ThenBy(f => f.Order).ToList();
    public List<BuyabilityFactor> MetFactors => Factors.Where(f => f.IsMet).OrderBy(f => f.Category).ThenBy(f => f.Order).ToList();
}

public class CategoryBreakdown
{
    public string Name { get; set; } = string.Empty;
    public int MaxPoints { get; set; }
    public int EarnedPoints { get; set; }
    public int Percentage => MaxPoints > 0 ? (EarnedPoints * 100) / MaxPoints : 0;
}

public class BuyabilityFactor
{
    public string Name { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool IsMet { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? FixLink { get; set; }
    public string? FixTab { get; set; }
}

public class BuyabilityScoreService : IBuyabilityScoreService
{
    private readonly AppDbContext _db;

    public BuyabilityScoreService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BuyabilityResult> CalculateScoreAsync(int itemId)
    {
        var item = await _db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
        {
            return new BuyabilityResult { Score = 0 };
        }

        var vpnCount = await _db.VendorItemParts.CountAsync(v => v.ItemId == itemId && v.IsActive);
        var avlCount = await _db.ItemApprovedVendors.CountAsync(a => a.ItemId == itemId && a.ApprovalStatus == AvlApprovalStatus.Approved);

        return CalculateScore(item, vpnCount, avlCount);
    }

    public async Task<BuyabilityResult> CalculateScoreWithDetailsAsync(int itemId)
    {
        var item = await _db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
        {
            return new BuyabilityResult { Score = 0 };
        }

        var vpnCount = await _db.VendorItemParts.CountAsync(v => v.ItemId == itemId && v.IsActive);
        var avlCount = await _db.ItemApprovedVendors.CountAsync(a => a.ItemId == itemId && a.ApprovalStatus == AvlApprovalStatus.Approved);
        var hasPreferred = await _db.ItemApprovedVendors.AnyAsync(a => a.ItemId == itemId && a.IsPreferred);
        var hasCatalog = await _db.VendorItemParts.AnyAsync(v => v.ItemId == itemId && v.IsActive && !string.IsNullOrEmpty(v.ProductPageUrl));

        return CalculateScore(item, vpnCount, avlCount, hasPreferred, hasCatalog);
    }

    public BuyabilityResult CalculateScore(Item item, int vpnCount = 0, int avlCount = 0, bool hasPreferredVendor = false, bool hasCatalogUrl = false)
    {
        var result = new BuyabilityResult();
        int order = 0;

        // Core Identification (25 points max) - Category 1
        AddFactor(result, "Part Number", 10, !string.IsNullOrWhiteSpace(item.PartNumber), 
            "Item has a valid part number", "Core ID", ++order, null, "Basics");
        AddFactor(result, "Description", 10, !string.IsNullOrWhiteSpace(item.Description), 
            "Item has a description", "Core ID", ++order, null, "Basics");
        AddFactor(result, "Unit of Measure", 5, !string.IsNullOrWhiteSpace(item.StockUOM), 
            "Unit of measure is defined", "Core ID", ++order, null, "Basics");

        // Sourcing (25 points max) - Category 2
        AddFactor(result, "Has Vendor Part", 10, vpnCount > 0, 
            "At least one vendor part number exists", "Sourcing", ++order, null, "Vendor Parts");
        AddFactor(result, "On AVL", 10, avlCount > 0, 
            "Vendor is on Approved Vendor List", "Sourcing", ++order, null, "Approved Vendors");
        AddFactor(result, "Preferred Vendor", 5, hasPreferredVendor, 
            "A preferred vendor is designated", "Sourcing", ++order, null, "Approved Vendors");

        // Procurement Fields (25 points max) - Category 3
        AddFactor(result, "Lead Time Defined", 5, item.LeadTimeDays > 0, 
            "Lead time is specified", "Procurement", ++order, "LeadTimeDays", "Basics");
        AddFactor(result, "MOQ/Order Multiple", 5, item.MinOrderQty.HasValue || item.OrderMultiple.HasValue, 
            "Ordering constraints are defined", "Procurement", ++order, "MinOrderQty", "Basics");
        AddFactor(result, "Last Price", 5, item.LastPrice.HasValue, 
            "Reference price is available", "Procurement", ++order, "LastPrice", "Basics");
        AddFactor(result, "Stock Policy Set", 5, item.StockPolicy != StockPolicy.Stock, 
            "Stock policy is explicitly set", "Procurement", ++order, "StockPolicy", "Basics");
        AddFactor(result, "Catalog Link", 5, hasCatalogUrl, 
            "Vendor catalog URL is available", "Procurement", ++order, null, "Vendor Parts");

        // Inventory Planning (15 points max) - Category 4
        AddFactor(result, "Reorder Point", 5, item.ReorderPoint > 0, 
            "Reorder point is configured", "Planning", ++order, "ReorderPoint", "Basics");
        AddFactor(result, "Safety Stock", 5, item.SafetyStock > 0, 
            "Safety stock level is set", "Planning", ++order, "SafetyStock", "Basics");
        AddFactor(result, "ABC Classification", 5, item.ABCClass != ABCClassification.Unclassified, 
            "ABC classification is assigned", "Planning", ++order, "ABCClass", "Basics");

        // Compliance & Metadata (15 points max) - Category 5
        AddFactor(result, "Active Status", 5, item.IsActive, 
            "Item is active and purchasable", "Compliance", ++order, null, "Basics");
        AddFactor(result, "Has Category", 5, item.CategoryId.HasValue, 
            "Item is categorized", "Compliance", ++order, "CategoryId", "Basics");
        AddFactor(result, "On Contract", 5, item.ContractFlag, 
            "Item is covered by a purchasing contract", "Compliance", ++order, "ContractFlag", "Basics");

        result.Score = Math.Min(100, result.Factors.Where(f => f.IsMet).Sum(f => f.Points));

        result.Categories = result.Factors
            .GroupBy(f => f.Category)
            .ToDictionary(
                g => g.Key,
                g => new CategoryBreakdown
                {
                    Name = g.Key,
                    MaxPoints = g.Sum(f => f.Points),
                    EarnedPoints = g.Where(f => f.IsMet).Sum(f => f.Points)
                }
            );

        return result;
    }

    private void AddFactor(BuyabilityResult result, string name, int points, bool isMet, 
        string description, string category, int order, string? fixLink, string? fixTab)
    {
        result.Factors.Add(new BuyabilityFactor
        {
            Name = name,
            Points = points,
            IsMet = isMet,
            Description = description,
            Category = category,
            Order = order,
            FixLink = fixLink,
            FixTab = fixTab
        });
    }
}
