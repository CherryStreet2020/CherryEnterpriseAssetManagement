using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class CcaService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public CcaService(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<CcaClassBalance> CalculateCcaForClassAsync(int ccaClassId, int fiscalYear, int? daysInFiscalPeriod = null)
        {
            var ccaClass = await _db.CcaClasses.Where(c => c.Id == ccaClassId).FirstOrDefaultAsync();
            if (ccaClass == null)
                throw new ArgumentException($"CCA Class {ccaClassId} not found");

            var existingBalance = await _db.CcaClassBalances
                .FirstOrDefaultAsync(b => b.CcaClassId == ccaClassId && b.FiscalYear == fiscalYear);

            if (existingBalance != null && existingBalance.IsPosted)
                throw new InvalidOperationException($"CCA for Class {ccaClass.ClassNumber} in fiscal year {fiscalYear} is already posted");

            var previousBalance = await _db.CcaClassBalances
                .Where(b => b.CcaClassId == ccaClassId && b.FiscalYear == fiscalYear - 1)
                .OrderBy(b => b.Id)
                .FirstOrDefaultAsync();

            decimal openingUcc = previousBalance?.ClosingUcc ?? 0;

            var companyId = GetCompanyId();
            var transactions = await _db.CcaTransactions
                .Include(t => t.Asset)
                .ThenInclude(a => a!.TaxSettings)
                .Where(t => t.CcaClassId == ccaClassId && t.FiscalYear == fiscalYear && t.Asset != null && _tenantContext.VisibleCompanyIds.Contains(t.Asset.CompanyId ?? 0))
                .ToListAsync();

            var additions = transactions
                .Where(t => t.TransactionType == CcaTransactionType.Addition)
                .Where(t => IsAvailableForUse(t, fiscalYear))
                .Sum(t => t.CapitalCost);

            var dispositions = transactions
                .Where(t => t.TransactionType == CcaTransactionType.Disposition)
                .Sum(t => Math.Min(t.Proceeds ?? 0, t.CapitalCost));

            decimal netAdditions = Math.Max(0, additions - dispositions);
            decimal halfYearAdjustment = 0;

            if (ccaClass.HalfYearRuleApplies && netAdditions > 0)
            {
                if (ccaClass.IsAcceleratedInvestmentIncentive)
                {
                    halfYearAdjustment = 0;
                }
                else
                {
                    halfYearAdjustment = netAdditions * 0.5m;
                }
            }

            decimal uccBeforeCca = openingUcc + additions - dispositions;

            decimal? recapture = null;
            decimal? terminalLoss = null;
            bool classIsEmpty = await IsClassEmptyAsync(ccaClassId, fiscalYear);

            if (uccBeforeCca < 0)
            {
                recapture = Math.Abs(uccBeforeCca);
                uccBeforeCca = 0;
            }
            else if (classIsEmpty && uccBeforeCca > 0 && additions == 0)
            {
                terminalLoss = uccBeforeCca;
                uccBeforeCca = 0;
            }

            decimal baseForCca = uccBeforeCca - halfYearAdjustment;
            if (baseForCca < 0)
                baseForCca = 0;

            decimal ccaClaimed = CalculateCcaAmount(ccaClass, baseForCca, fiscalYear, daysInFiscalPeriod);

            decimal closingUcc = uccBeforeCca - ccaClaimed;
            if (closingUcc < 0)
                closingUcc = 0;

            if (recapture.HasValue || terminalLoss.HasValue)
            {
                closingUcc = 0;
                ccaClaimed = 0;
            }

            var balance = existingBalance ?? new CcaClassBalance
            {
                CcaClassId = ccaClassId,
                FiscalYear = fiscalYear
            };

            balance.OpeningUcc = openingUcc;
            balance.Additions = additions;
            balance.Dispositions = dispositions;
            balance.HalfYearAdjustment = halfYearAdjustment;
            balance.BaseForCca = baseForCca;
            balance.CcaClaimed = Math.Round(ccaClaimed, 2);
            balance.ClosingUcc = Math.Round(closingUcc, 2);
            balance.Recapture = recapture.HasValue ? Math.Round(recapture.Value, 2) : null;
            balance.TerminalLoss = terminalLoss.HasValue ? Math.Round(terminalLoss.Value, 2) : null;
            balance.DaysInFiscalPeriod = daysInFiscalPeriod;
            balance.IsShortFiscalPeriod = daysInFiscalPeriod.HasValue && daysInFiscalPeriod.Value < 365;

            if (existingBalance == null)
                _db.CcaClassBalances.Add(balance);

            await _db.SaveChangesAsync();
            return balance;
        }

        private bool IsAvailableForUse(CcaTransaction transaction, int fiscalYear)
        {
            var afuDate = transaction.AvailableForUseDate ?? transaction.TransactionDate;
            return afuDate.Year <= fiscalYear;
        }

        private decimal CalculateCcaAmount(CcaClass ccaClass, decimal baseForCca, int fiscalYear, int? daysInFiscalPeriod)
        {
            if (baseForCca <= 0)
                return 0;

            decimal ccaClaimed;

            switch (ccaClass.ClassNumber)
            {
                case 12:
                    ccaClaimed = baseForCca;
                    break;
                case 13:
                case 14:
                    if (ccaClass.Rate > 0)
                        ccaClaimed = baseForCca * ccaClass.Rate;
                    else
                        ccaClaimed = 0;
                    break;
                case 29:
                    ccaClaimed = baseForCca * 0.5m;
                    break;
                default:
                    ccaClaimed = baseForCca * ccaClass.Rate;
                    break;
            }

            if (daysInFiscalPeriod.HasValue && daysInFiscalPeriod.Value < 365)
            {
                ccaClaimed = ccaClaimed * daysInFiscalPeriod.Value / 365m;
            }

            if (ccaClaimed > baseForCca)
                ccaClaimed = baseForCca;

            return ccaClaimed;
        }

        private async Task<bool> IsClassEmptyAsync(int ccaClassId, int fiscalYear)
        {
            var companyId = GetCompanyId();
            var activeAssets = await _db.AssetTaxSettings
                .Include(t => t.Asset)
                .Where(t => t.CcaClassId == ccaClassId
                    && _tenantContext.VisibleCompanyIds.Contains(t.Asset.CompanyId ?? 0)
                    && t.Asset.Status == AssetStatus.Active
                    && (t.DisposalDate == null || t.DisposalDate.Value.Year > fiscalYear))
                .CountAsync();

            return activeAssets == 0;
        }

        public async Task<CcaClassBalance> PostCcaForClassAsync(int ccaClassId, int fiscalYear, string postedBy)
        {
            var balance = await _db.CcaClassBalances
                .FirstOrDefaultAsync(b => b.CcaClassId == ccaClassId && b.FiscalYear == fiscalYear);

            if (balance == null)
                throw new InvalidOperationException("CCA balance not found. Calculate CCA first.");

            if (balance.IsPosted)
                throw new InvalidOperationException("CCA is already posted for this period.");

            balance.IsPosted = true;
            balance.PostedDate = DateTime.UtcNow;
            balance.PostedBy = postedBy;

            await _db.SaveChangesAsync();
            return balance;
        }

        public async Task<AssetTaxSettings> AddAssetToCcaClassAsync(int assetId, int ccaClassId, DateTime? availableForUseDate = null)
        {
            var companyId = GetCompanyId();
            var asset = await _db.Assets.Where(a => a.Id == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (asset == null)
                throw new ArgumentException($"Asset {assetId} not found");

            var ccaClass = await _db.CcaClasses.Where(c => c.Id == ccaClassId).FirstOrDefaultAsync();
            if (ccaClass == null)
                throw new ArgumentException($"CCA Class {ccaClassId} not found");

            var existingSettings = await _db.AssetTaxSettings.Where(t => t.AssetId == assetId).OrderBy(t => t.Id).FirstOrDefaultAsync();
            if (existingSettings != null)
                throw new InvalidOperationException($"Asset {assetId} already has tax settings. Use update instead.");

            var afuDate = availableForUseDate ?? asset.InServiceDate;

            var taxSettings = new AssetTaxSettings
            {
                AssetId = assetId,
                CcaClassId = ccaClassId,
                AvailableForUseDate = afuDate,
                CapitalCost = asset.AcquisitionCost,
                EligibleForAcceleratedIncentive = ccaClass.IsAcceleratedInvestmentIncentive
            };

            _db.AssetTaxSettings.Add(taxSettings);

            var fiscalYear = afuDate.Year;
            var transaction = new CcaTransaction
            {
                CcaClassId = ccaClassId,
                AssetId = assetId,
                FiscalYear = fiscalYear,
                TransactionType = CcaTransactionType.Addition,
                TransactionDate = asset.InServiceDate,
                AvailableForUseDate = afuDate,
                CapitalCost = asset.AcquisitionCost,
                NetAddition = asset.AcquisitionCost,
                SubjectToHalfYearRule = ccaClass.HalfYearRuleApplies,
                IsAcceleratedIncentiveEligible = ccaClass.IsAcceleratedInvestmentIncentive,
                Description = $"Addition: {asset.AssetNumber} - {asset.Description}"
            };

            _db.CcaTransactions.Add(transaction);
            await _db.SaveChangesAsync();

            return taxSettings;
        }

        public async Task<CcaTransaction> RecordDispositionAsync(int assetId, decimal proceeds, DateTime disposalDate, DisposalType disposalType)
        {
            var companyId = GetCompanyId();
            var taxSettings = await _db.AssetTaxSettings
                .Include(t => t.CcaClass)
                .Include(t => t.Asset)
                .Where(t => t.AssetId == assetId && _tenantContext.VisibleCompanyIds.Contains(t.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (taxSettings == null)
                throw new InvalidOperationException($"Asset {assetId} has no tax settings.");

            var cappedProceeds = Math.Min(proceeds, taxSettings.CapitalCost);

            taxSettings.Proceeds = proceeds;
            taxSettings.DisposalDate = disposalDate;
            taxSettings.DisposalType = disposalType;

            var transaction = new CcaTransaction
            {
                CcaClassId = taxSettings.CcaClassId,
                AssetId = assetId,
                FiscalYear = disposalDate.Year,
                TransactionType = CcaTransactionType.Disposition,
                TransactionDate = disposalDate,
                CapitalCost = taxSettings.CapitalCost,
                Proceeds = proceeds,
                AdjustedCostBase = taxSettings.CapitalCost,
                NetAddition = -cappedProceeds,
                SubjectToHalfYearRule = false,
                Description = $"Disposition: {taxSettings.Asset.AssetNumber} - {taxSettings.Asset.Description}"
            };

            _db.CcaTransactions.Add(transaction);

            var asset = taxSettings.Asset;
            asset.Status = AssetStatus.Disposed;
            asset.DisposalDate = disposalDate;
            asset.DisposalProceeds = proceeds;
            asset.GainLossOnDisposal = proceeds - (asset.BookValue ?? 0);
            asset.Active = false;

            await _db.SaveChangesAsync();
            return transaction;
        }

        public async Task<List<CcaClassBalance>> GetCcaSummaryByYearAsync(int fiscalYear)
        {
            return await _db.CcaClassBalances
                .Include(b => b.CcaClass)
                .Where(b => b.FiscalYear == fiscalYear)
                .OrderBy(b => b.CcaClass.ClassNumber)
                .ToListAsync();
        }

        public async Task<List<CcaTransaction>> GetTransactionsByClassAsync(int ccaClassId, int? fiscalYear = null)
        {
            var companyId = GetCompanyId();
            var query = _db.CcaTransactions
                .Include(t => t.Asset)
                .Where(t => t.CcaClassId == ccaClassId && t.Asset != null && _tenantContext.VisibleCompanyIds.Contains(t.Asset.CompanyId ?? 0));

            if (fiscalYear.HasValue)
                query = query.Where(t => t.FiscalYear == fiscalYear.Value);

            return await query
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<List<CcaClass>> GetCcaClassesAsync()
        {
            return await _db.CcaClasses
                .Where(c => c.Active)
                .OrderBy(c => c.ClassNumber)
                .ToListAsync();
        }
    }
}
