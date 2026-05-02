using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class InventoryService
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public InventoryService(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<List<InventoryList>> GetAllInventoryListsAsync()
        {
            var companyId = GetCompanyId();
            return await _context.InventoryLists
                .Where(il => _tenantContext.VisibleCompanyIds.Contains(il.CompanyId ?? 0))
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<InventoryList?> GetInventoryListAsync(int id)
        {
            var companyId = GetCompanyId();
            return await _context.InventoryLists
                .Include(x => x.Scans!)
                .ThenInclude(s => s.Asset)
                .Where(x => x.Id == id && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .FirstOrDefaultAsync();
        }

        public async Task<InventoryList> CreateInventoryListAsync(InventoryList list)
        {
            list.CreatedDate = DateTime.UtcNow;
            list.Status = InventoryStatus.Draft;
            list.CompanyId = GetCompanyId();
            list.TenantId = _tenantContext.TenantId;
            _context.InventoryLists.Add(list);
            await _context.SaveChangesAsync();
            return list;
        }

        public async Task<InventoryList> StartInventoryAsync(int listId)
        {
            var companyId = GetCompanyId();
            var list = await _context.InventoryLists
                .Where(x => x.Id == listId && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (list != null)
            {
                list.Status = InventoryStatus.InProgress;
                list.StartedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return list!;
        }

        public async Task<InventoryList> CompleteInventoryAsync(int listId)
        {
            var companyId = GetCompanyId();
            var list = await _context.InventoryLists
                .Where(x => x.Id == listId && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (list != null)
            {
                list.Status = InventoryStatus.Completed;
                list.CompletedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return list!;
        }

        public async Task<InventoryScan> RecordScanAsync(InventoryScan scan)
        {
            var companyId = GetCompanyId();
            scan.ScanDate = DateTime.UtcNow;
            _context.InventoryScans.Add(scan);
            
            var list = await _context.InventoryLists
                .Where(x => x.Id == scan.InventoryListId && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (list != null)
            {
                list.ScannedAssets++;
                if (scan.Result == ScanResult.Found)
                {
                    list.FoundAssets++;
                }
                else if (scan.Result == ScanResult.Missing)
                {
                    list.MissingAssets++;
                }
            }
            
            await _context.SaveChangesAsync();
            return scan;
        }

        public async Task<AssetInventory?> GetAssetInventoryAsync(int assetId)
        {
            var companyId = GetCompanyId();
            return await _context.AssetInventories
                .Include(x => x.Asset)
                .Where(x => x.AssetId == assetId && x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
        }

        public async Task<AssetInventory> CreateOrUpdateAssetInventoryAsync(AssetInventory inventory)
        {
            var companyId = GetCompanyId();
            var assetBelongsToTenant = await _context.Assets
                .AnyAsync(a => a.Id == inventory.AssetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0));
            if (!assetBelongsToTenant)
                throw new InvalidOperationException("Asset not found for this tenant");

            var existing = await _context.AssetInventories
                .FirstOrDefaultAsync(x => x.AssetId == inventory.AssetId);

            if (existing != null)
            {
                existing.BarcodeNumber = inventory.BarcodeNumber;
                existing.BarcodeType = inventory.BarcodeType;
                existing.LastScanDate = inventory.LastScanDate;
                existing.LastScanLocation = inventory.LastScanLocation;
                existing.LastScannedBy = inventory.LastScannedBy;
                existing.Condition = inventory.Condition;
                existing.ConditionNotes = inventory.ConditionNotes;
                existing.PhotoPath = inventory.PhotoPath;
                existing.IsReconciled = inventory.IsReconciled;
                existing.LastReconciledDate = inventory.LastReconciledDate;
            }
            else
            {
                _context.AssetInventories.Add(inventory);
            }

            await _context.SaveChangesAsync();
            return existing ?? inventory;
        }

        public string GenerateBarcodeNumber(int assetId, string prefix = "FA")
        {
            return $"{prefix}-{assetId:D6}";
        }

        public async Task<List<Asset>> GetAssetsWithoutBarcodeAsync()
        {
            var companyId = GetCompanyId();
            var assetsWithBarcode = await _context.AssetInventories
                .Select(x => x.AssetId)
                .ToListAsync();

            return await _context.Assets
                .Where(a => !assetsWithBarcode.Contains(a.Id) && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();
        }

        public async Task<int> GenerateBarcodesForAllAssetsAsync(string prefix = "FA")
        {
            var assetsWithoutBarcode = await GetAssetsWithoutBarcodeAsync();
            var count = 0;

            foreach (var asset in assetsWithoutBarcode)
            {
                var inventory = new AssetInventory
                {
                    AssetId = asset.Id,
                    BarcodeNumber = GenerateBarcodeNumber(asset.Id, prefix),
                    BarcodeType = "Code128",
                    Condition = AssetCondition.Good,
                    IsReconciled = true,
                    LastReconciledDate = DateTime.UtcNow
                };
                _context.AssetInventories.Add(inventory);
                count++;
            }

            await _context.SaveChangesAsync();
            return count;
        }

        public async Task<InventoryStats> GetInventoryStatsAsync()
        {
            var companyId = GetCompanyId();
            var totalAssets = await _context.Assets.Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0)).CountAsync();
            var assetsWithBarcode = await _context.AssetInventories
                .Where(ai => ai.Asset != null && _tenantContext.VisibleCompanyIds.Contains(ai.Asset.CompanyId ?? 0))
                .CountAsync();
            var recentScans = await _context.InventoryScans
                .Where(s => s.ScanDate > DateTime.UtcNow.AddDays(-30))
                .CountAsync();
            var openInventories = await _context.InventoryLists
                .CountAsync(l => l.Status == InventoryStatus.InProgress && _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0));

            return new InventoryStats
            {
                TotalAssets = totalAssets,
                AssetsWithBarcode = assetsWithBarcode,
                RecentScans = recentScans,
                OpenInventories = openInventories
            };
        }
    }

    public class InventoryStats
    {
        public int TotalAssets { get; set; }
        public int AssetsWithBarcode { get; set; }
        public int RecentScans { get; set; }
        public int OpenInventories { get; set; }
    }
}
