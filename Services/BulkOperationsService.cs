using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class BulkOperationsService
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public BulkOperationsService(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<PartialDisposal> ProcessPartialDisposalAsync(
            int assetId,
            decimal percentageToDispose,
            decimal saleProceeds,
            DisposalReason reason,
            string? notes,
            string? buyer,
            string? processedBy)
        {
            var companyId = GetCompanyId();
            var asset = await _context.Assets
                .Where(a => a.Id == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (asset == null)
                throw new InvalidOperationException("Asset not found");

            if (percentageToDispose <= 0 || percentageToDispose > 1)
                throw new InvalidOperationException("Percentage must be between 0 and 1 (e.g., 0.25 for 25%)");

            var costDisposed = asset.AcquisitionCost * percentageToDispose;
            var accumDepDisposed = asset.AccumulatedDepreciation * percentageToDispose;
            var bookValueDisposed = costDisposed - accumDepDisposed;
            var gainLoss = saleProceeds - bookValueDisposed;

            var disposal = new PartialDisposal
            {
                AssetId = assetId,
                DisposalDate = DateTime.UtcNow,
                PercentageDisposed = percentageToDispose,
                OriginalCostDisposed = costDisposed,
                AccumulatedDepreciationDisposed = accumDepDisposed,
                BookValueDisposed = bookValueDisposed,
                SaleProceeds = saleProceeds,
                GainLoss = gainLoss,
                Reason = reason,
                Notes = notes,
                Buyer = buyer,
                ProcessedBy = processedBy
            };

            asset.AcquisitionCost -= costDisposed;
            asset.AccumulatedDepreciation -= accumDepDisposed;

            // Create a child asset row representing the disposed portion so
            // both the remaining (parent) and disposed (child) pieces are
            // visible/queryable in the asset register.
            var seq = await _context.PartialDisposals.CountAsync(p => p.AssetId == assetId) + 1;
            var childAssetNumber = $"{asset.AssetNumber}-PD{seq}";
            var siblingExists = await _context.Assets
                .AnyAsync(a => a.AssetNumber == childAssetNumber && a.CompanyId == asset.CompanyId);
            if (siblingExists)
            {
                childAssetNumber = $"{asset.AssetNumber}-PD{seq}-{DateTime.UtcNow.Ticks % 100000}";
            }

            var childAsset = new Asset
            {
                AssetNumber = childAssetNumber,
                Description = $"{asset.Description} (Partial Disposal)",
                LongDescription = asset.LongDescription,
                Model = asset.Model,
                SerialNumber = asset.SerialNumber,
                AssetType = asset.AssetType,
                AssetTypeLookupValueId = asset.AssetTypeLookupValueId,
                ParentAssetId = asset.Id,
                AcquisitionCost = costDisposed,
                AccumulatedDepreciation = accumDepDisposed,
                SalvageValue = asset.SalvageValue * percentageToDispose,
                Currency = asset.Currency,
                DepreciationMethod = asset.DepreciationMethod,
                DepreciationMethodLookupValueId = asset.DepreciationMethodLookupValueId,
                UsefulLifeMonths = asset.UsefulLifeMonths,
                InServiceDate = asset.InServiceDate,
                PurchaseDate = asset.PurchaseDate,
                FiscalPurchaseYear = asset.FiscalPurchaseYear,
                CompanyId = asset.CompanyId,
                SiteId = asset.SiteId,
                LocationId = asset.LocationId,
                DepartmentId = asset.DepartmentId,
                AssetCategoryId = asset.AssetCategoryId,
                ManufacturerId = asset.ManufacturerId,
                VendorId = asset.VendorId,
                CostCenterId = asset.CostCenterId,
                Status = AssetStatus.Disposed,
                StatusLookupValueId = asset.StatusLookupValueId,
                Active = false,
                Priority = asset.Priority,
                AssetPriorityLookupValueId = asset.AssetPriorityLookupValueId,
                Condition = asset.Condition,
                ConditionLookupValueId = asset.ConditionLookupValueId,
                DisposalDate = DateTime.UtcNow,
                DisposalProceeds = saleProceeds,
                GainLossOnDisposal = gainLoss,
                DisposalReason = reason.ToString(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = processedBy
            };

            _context.Assets.Add(childAsset);
            _context.PartialDisposals.Add(disposal);
            await _context.SaveChangesAsync();

            return disposal;
        }

        public async Task<List<PartialDisposal>> GetPartialDisposalsAsync(int? assetId = null)
        {
            var companyId = GetCompanyId();
            var query = _context.PartialDisposals
                .Include(x => x.Asset)
                .Where(x => x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0))
                .AsQueryable();
            if (assetId.HasValue)
                query = query.Where(x => x.AssetId == assetId.Value);
            return await query.OrderByDescending(x => x.DisposalDate).ToListAsync();
        }

        public async Task<BulkOperation> BulkTransferAsync(
            List<int> assetIds,
            string newLocation,
            string? newDepartment,
            string? processedBy)
        {
            var companyId = GetCompanyId();
            var assets = await _context.Assets
                .Where(a => assetIds.Contains(a.Id) && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .ToListAsync();

            foreach (var asset in assets)
            {
                var transfer = new AssetTransfer
                {
                    AssetId = asset.Id,
                    TransferDate = DateTime.UtcNow,
                    FromLocation = asset.LocationRef?.Name,
                    ToLocation = newLocation,
                    FromDepartment = asset.Department,
                    ToDepartment = newDepartment ?? asset.Department,
                    CreatedBy = processedBy,
                    Reason = "Bulk transfer operation"
                };
                _context.AssetTransfers.Add(transfer);

                if (!string.IsNullOrEmpty(newDepartment))
                    asset.Department = newDepartment;
            }

            var bulkOp = new BulkOperation
            {
                OperationType = BulkOperationType.Transfer,
                OperationDate = DateTime.UtcNow,
                AssetsAffected = assets.Count,
                Description = $"Bulk transfer of {assets.Count} assets to {newLocation}",
                NewLocation = newLocation,
                NewDepartment = newDepartment,
                ProcessedBy = processedBy,
                AssetIds = string.Join(",", assetIds),
                CompanyId = companyId,
                TenantId = _tenantContext.TenantId
            };

            _context.BulkOperations.Add(bulkOp);
            await _context.SaveChangesAsync();

            return bulkOp;
        }

        public async Task<BulkOperation> BulkStatusChangeAsync(
            List<int> assetIds,
            AssetStatus newStatus,
            string? processedBy)
        {
            var companyId = GetCompanyId();
            var assets = await _context.Assets
                .Where(a => assetIds.Contains(a.Id) && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .ToListAsync();

            foreach (var asset in assets)
            {
                asset.Status = newStatus;
                if (newStatus == AssetStatus.Disposed || newStatus == AssetStatus.WrittenOff)
                {
                    asset.DisposalDate = DateTime.UtcNow;
                }
            }

            var bulkOp = new BulkOperation
            {
                OperationType = BulkOperationType.StatusChange,
                OperationDate = DateTime.UtcNow,
                AssetsAffected = assets.Count,
                Description = $"Bulk status change of {assets.Count} assets to {newStatus}",
                NewStatus = newStatus,
                ProcessedBy = processedBy,
                AssetIds = string.Join(",", assetIds),
                CompanyId = companyId,
                TenantId = _tenantContext.TenantId
            };

            _context.BulkOperations.Add(bulkOp);
            await _context.SaveChangesAsync();

            return bulkOp;
        }

        public async Task<List<BulkOperation>> GetBulkOperationsAsync()
        {
            var companyId = GetCompanyId();
            return await _context.BulkOperations
                .Where(bo => _tenantContext.VisibleCompanyIds.Contains(bo.CompanyId ?? 0))
                .OrderByDescending(x => x.OperationDate)
                .ToListAsync();
        }

        public async Task<BulkOperationStats> GetBulkOperationStatsAsync()
        {
            var companyId = GetCompanyId();
            var operations = await _context.BulkOperations
                .Where(bo => _tenantContext.VisibleCompanyIds.Contains(bo.CompanyId ?? 0))
                .ToListAsync();
            var partialDisposals = await _context.PartialDisposals
                .Include(p => p.Asset)
                .Where(p => p.Asset != null && _tenantContext.VisibleCompanyIds.Contains(p.Asset.CompanyId ?? 0))
                .ToListAsync();

            return new BulkOperationStats
            {
                TotalBulkOperations = operations.Count,
                TotalAssetsAffected = operations.Sum(x => x.AssetsAffected),
                TotalPartialDisposals = partialDisposals.Count,
                TotalProceedsFromPartialDisposals = partialDisposals.Sum(x => x.SaleProceeds)
            };
        }
    }

    public class BulkOperationStats
    {
        public int TotalBulkOperations { get; set; }
        public int TotalAssetsAffected { get; set; }
        public int TotalPartialDisposals { get; set; }
        public decimal TotalProceedsFromPartialDisposals { get; set; }
    }
}
