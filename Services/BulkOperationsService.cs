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
            // visible/queryable in the asset register. Asset.AssetNumber is
            // capped at 50 chars, so we budget the suffix and truncate the
            // parent prefix to fit, then resolve uniqueness against the DB.
            var seq = await _context.PartialDisposals.CountAsync(p => p.AssetId == assetId) + 1;
            var childAssetNumber = await GenerateUniqueChildAssetNumberAsync(asset, seq);

            var childAsset = new Asset
            {
                AssetNumber = childAssetNumber,
                Description = Truncate($"{asset.Description} (Partial Disposal)", 200),
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
                // Intentionally do not copy parent's StatusLookupValueId — the
                // child is Disposed, not whatever lookup the parent carries.
                StatusLookupValueId = null,
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

            // Build the gain/loss journal entry for the disposed portion when
            // the company has a usable book + GL account mapping. If config is
            // absent the journal is simply not produced; if config is present
            // the journal save is part of the same transaction as the disposal
            // so accounting cannot silently diverge from the asset register.
            var journalEntry = await BuildPartialDisposalJournalAsync(asset, childAsset, disposal);
            if (journalEntry != null)
            {
                _context.JournalEntries.Add(journalEntry);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();
            if (journalEntry != null)
            {
                disposal.JournalEntryId = journalEntry.Id;
                await _context.SaveChangesAsync();
            }
            await tx.CommitAsync();

            return disposal;
        }

        private async Task<string> GenerateUniqueChildAssetNumberAsync(Asset parent, int seq)
        {
            const int maxLen = 50;
            var suffix = $"-PD{seq}";
            var prefix = parent.AssetNumber ?? string.Empty;
            if (prefix.Length + suffix.Length > maxLen)
                prefix = prefix.Substring(0, maxLen - suffix.Length);

            var candidate = prefix + suffix;
            var exists = await _context.Assets
                .AnyAsync(a => a.AssetNumber == candidate && a.CompanyId == parent.CompanyId);
            if (!exists) return candidate;

            // Fall back to a uniqueness-safe form by appending a short token,
            // truncating the prefix further so the final string still fits.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var token = ((DateTime.UtcNow.Ticks + attempt) % 100000).ToString("D5");
                var altSuffix = $"-PD{seq}-{token}";
                var altPrefix = parent.AssetNumber ?? string.Empty;
                if (altPrefix.Length + altSuffix.Length > maxLen)
                    altPrefix = altPrefix.Substring(0, maxLen - altSuffix.Length);
                var alt = altPrefix + altSuffix;
                var altExists = await _context.Assets
                    .AnyAsync(a => a.AssetNumber == alt && a.CompanyId == parent.CompanyId);
                if (!altExists) return alt;
            }

            throw new InvalidOperationException(
                $"Unable to generate a unique child asset number for parent '{parent.AssetNumber}'.");
        }

        private async Task<JournalEntry?> BuildPartialDisposalJournalAsync(
            Asset parent,
            Asset child,
            PartialDisposal disposal)
        {
            var companyId = parent.CompanyId;
            if (companyId == null) return null;

            var book = await _context.Books
                .Where(b => b.CompanyId == companyId)
                .OrderByDescending(b => b.IsPrimaryBook)
                .ThenBy(b => b.Id)
                .FirstOrDefaultAsync();
            if (book == null) return null;

            var glAccounts = await _context.BookGlAccounts
                .Where(g => g.BookId == book.Id)
                .OrderBy(g => g.Id)
                .FirstOrDefaultAsync();

            string? accumDepAcct = FirstNonEmpty(glAccounts?.AccumulatedDepreciation, book.GlAccountAccumDep);
            string? assetAcct = FirstNonEmpty(glAccounts?.Asset, book.GlAccountAssetClearing);
            string? clearingAcct = FirstNonEmpty(glAccounts?.Clearing, book.GlAccountAssetClearing);
            string? gainAcct = FirstNonEmpty(glAccounts?.GainOnDisposal, book.GlAccountGainOnDisposal);
            string? lossAcct = FirstNonEmpty(glAccounts?.LossOnDisposal, book.GlAccountLossOnDisposal);

            if (string.IsNullOrWhiteSpace(accumDepAcct) || string.IsNullOrWhiteSpace(assetAcct))
                return null;
            if (disposal.SaleProceeds > 0 && string.IsNullOrWhiteSpace(clearingAcct))
                return null;
            if (disposal.GainLoss > 0 && string.IsNullOrWhiteSpace(gainAcct))
                return null;
            if (disposal.GainLoss < 0 && string.IsNullOrWhiteSpace(lossAcct))
                return null;

            var posting = disposal.DisposalDate == default ? DateTime.UtcNow : disposal.DisposalDate;
            // PR #108 / B-24: comment scrubbed for accuracy. JournalEntry.Batch
            // was widened from varchar(30) → varchar(60) in PR #83; the
            // hard-truncate below is left in as defensive belt-and-suspenders
            // even though the new format ("PDISP-yyyyMMdd-{9-digit token}",
            // 24 chars) is well within both old and new bounds.
            var token = (DateTime.UtcNow.Ticks % 1_000_000_000).ToString("D9");
            var batch = $"PDISP-{posting:yyyyMMdd}-{token}";
            if (batch.Length > 60) batch = batch.Substring(0, 60);

            var entry = new JournalEntry
            {
                BookId = book.Id,
                Period = posting.Year * 100 + posting.Month,
                Batch = batch,
                PostingDate = posting,
                Reference = Truncate(child.AssetNumber, 50),
                Source = "PartialDisposal",
                Description = Truncate($"Partial disposal of {parent.AssetNumber} ({disposal.PercentageDisposed:P0}) — {child.AssetNumber}", 200),
                CreatedUtc = DateTime.UtcNow
            };

            var lines = new List<JournalLine>();
            int lineNo = 1;

            lines.Add(new JournalLine
            {
                LineNo = lineNo++,
                Account = accumDepAcct!,
                Description = $"Remove accumulated depreciation - {child.AssetNumber}",
                Debit = disposal.AccumulatedDepreciationDisposed,
                Credit = 0m
            });

            if (disposal.SaleProceeds > 0)
            {
                lines.Add(new JournalLine
                {
                    LineNo = lineNo++,
                    Account = clearingAcct!,
                    Description = $"Cash/AR from partial disposal - {child.AssetNumber}",
                    Debit = disposal.SaleProceeds,
                    Credit = 0m
                });
            }

            lines.Add(new JournalLine
            {
                LineNo = lineNo++,
                Account = assetAcct!,
                Description = $"Remove asset cost - {child.AssetNumber}",
                Debit = 0m,
                Credit = disposal.OriginalCostDisposed
            });

            if (disposal.GainLoss > 0)
            {
                lines.Add(new JournalLine
                {
                    LineNo = lineNo++,
                    Account = gainAcct!,
                    Description = $"Gain on partial disposal - {child.AssetNumber}",
                    Debit = 0m,
                    Credit = disposal.GainLoss
                });
            }
            else if (disposal.GainLoss < 0)
            {
                lines.Add(new JournalLine
                {
                    LineNo = lineNo++,
                    Account = lossAcct!,
                    Description = $"Loss on partial disposal - {child.AssetNumber}",
                    Debit = Math.Abs(disposal.GainLoss),
                    Credit = 0m
                });
            }

            entry.Lines = lines;
            return entry;
        }

        private static string Truncate(string? value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value!.Length <= maxLen ? value : value.Substring(0, maxLen);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v)) return v;
            return null;
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
