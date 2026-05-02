using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class UsTaxService
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public UsTaxService(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<Section179Limits?> GetSection179LimitsAsync(int taxYear)
        {
            return await _context.Section179Limits
                .FirstOrDefaultAsync(x => x.TaxYear == taxYear);
        }

        public async Task<decimal> GetBonusDepreciationRateAsync(int taxYear)
        {
            var rate = await _context.BonusDepreciationRates
                .FirstOrDefaultAsync(x => x.TaxYear == taxYear);
            return rate?.Rate ?? 0;
        }

        public async Task<UsTaxSettings?> GetAssetUsTaxSettingsAsync(int assetId)
        {
            var companyId = GetCompanyId();
            return await _context.UsTaxSettings
                .Include(x => x.Asset)
                .Where(x => x.AssetId == assetId && x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
        }

        public async Task<UsTaxSettings> CreateOrUpdateUsTaxSettingsAsync(UsTaxSettings settings)
        {
            var companyId = GetCompanyId();
            var assetBelongsToTenant = await _context.Assets
                .AnyAsync(a => a.Id == settings.AssetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0));
            if (!assetBelongsToTenant)
                throw new InvalidOperationException("Asset not found for this tenant");

            var existing = await _context.UsTaxSettings
                .FirstOrDefaultAsync(x => x.AssetId == settings.AssetId);

            if (existing != null)
            {
                existing.PropertyClass = settings.PropertyClass;
                existing.Convention = settings.Convention;
                existing.UseADS = settings.UseADS;
                existing.Section179Amount = settings.Section179Amount;
                existing.Section179Elected = settings.Section179Elected;
                existing.BonusDepreciationPercent = settings.BonusDepreciationPercent;
                existing.BonusDepreciationAmount = settings.BonusDepreciationAmount;
                existing.QualifiedImprovementProperty = settings.QualifiedImprovementProperty;
                existing.ListedProperty = settings.ListedProperty;
                existing.BusinessUsePercent = settings.BusinessUsePercent;
                existing.PlacedInServiceDate = settings.PlacedInServiceDate;
                existing.TaxYear = settings.TaxYear;
                existing.DepreciableBasis = settings.DepreciableBasis;
                existing.Notes = settings.Notes;
            }
            else
            {
                _context.UsTaxSettings.Add(settings);
            }

            await _context.SaveChangesAsync();
            return existing ?? settings;
        }

        public decimal CalculateSection179Deduction(
            decimal acquisitionCost,
            int taxYear,
            Section179Limits limits,
            decimal totalPropertyPlacedInService)
        {
            if (limits == null) return 0;

            var phaseout = Math.Max(0, totalPropertyPlacedInService - limits.PhaseoutThreshold);
            var adjustedLimit = Math.Max(0, limits.MaxDeduction - phaseout);
            
            return Math.Min(acquisitionCost, adjustedLimit);
        }

        public decimal CalculateBonusDepreciation(
            decimal depreciableBasis,
            decimal bonusRate)
        {
            return depreciableBasis * (bonusRate / 100);
        }

        public decimal CalculateMacrsDepreciation(
            decimal depreciableBasis,
            MacrsPropertyClass propertyClass,
            MacrsConvention convention,
            int yearInService,
            bool useADS = false)
        {
            var years = GetMacrsRecoveryPeriod(propertyClass, useADS);
            var rates = GetMacrsRates(propertyClass, convention, useADS);

            if (yearInService < 1 || yearInService > rates.Length)
                return 0;

            return depreciableBasis * rates[yearInService - 1];
        }

        public int GetMacrsRecoveryPeriod(MacrsPropertyClass propertyClass, bool useADS)
        {
            if (useADS)
            {
                return propertyClass switch
                {
                    MacrsPropertyClass.ThreeYear => 4,
                    MacrsPropertyClass.FiveYear => 6,
                    MacrsPropertyClass.SevenYear => 10,
                    MacrsPropertyClass.TenYear => 15,
                    MacrsPropertyClass.FifteenYear => 20,
                    MacrsPropertyClass.TwentyYear => 25,
                    MacrsPropertyClass.TwentySevenAndHalfYear => 30,
                    MacrsPropertyClass.ThirtyNineYear => 40,
                    _ => 5
                };
            }

            return propertyClass switch
            {
                MacrsPropertyClass.ThreeYear => 3,
                MacrsPropertyClass.FiveYear => 5,
                MacrsPropertyClass.SevenYear => 7,
                MacrsPropertyClass.TenYear => 10,
                MacrsPropertyClass.FifteenYear => 15,
                MacrsPropertyClass.TwentyYear => 20,
                MacrsPropertyClass.TwentySevenAndHalfYear => 28,
                MacrsPropertyClass.ThirtyNineYear => 39,
                _ => 5
            };
        }

        private decimal[] GetMacrsRates(MacrsPropertyClass propertyClass, MacrsConvention convention, bool useADS)
        {
            if (convention == MacrsConvention.HalfYear && !useADS)
            {
                return propertyClass switch
                {
                    MacrsPropertyClass.ThreeYear => new[] { 0.3333m, 0.4445m, 0.1481m, 0.0741m },
                    MacrsPropertyClass.FiveYear => new[] { 0.20m, 0.32m, 0.192m, 0.1152m, 0.1152m, 0.0576m },
                    MacrsPropertyClass.SevenYear => new[] { 0.1429m, 0.2449m, 0.1749m, 0.1249m, 0.0893m, 0.0892m, 0.0893m, 0.0446m },
                    MacrsPropertyClass.TenYear => new[] { 0.10m, 0.18m, 0.144m, 0.1152m, 0.0922m, 0.0737m, 0.0655m, 0.0655m, 0.0656m, 0.0655m, 0.0328m },
                    MacrsPropertyClass.FifteenYear => new[] { 0.05m, 0.095m, 0.0855m, 0.077m, 0.0693m, 0.0623m, 0.059m, 0.059m, 0.0591m, 0.059m, 0.0591m, 0.059m, 0.0591m, 0.059m, 0.0591m, 0.0295m },
                    MacrsPropertyClass.TwentyYear => new[] { 0.0375m, 0.07219m, 0.06677m, 0.06177m, 0.05713m, 0.05285m, 0.04888m, 0.04522m, 0.04462m, 0.04461m, 0.04462m, 0.04461m, 0.04462m, 0.04461m, 0.04462m, 0.04461m, 0.04462m, 0.04461m, 0.04462m, 0.04461m, 0.02231m },
                    _ => new[] { 0.20m, 0.32m, 0.192m, 0.1152m, 0.1152m, 0.0576m }
                };
            }

            return propertyClass switch
            {
                MacrsPropertyClass.FiveYear => new[] { 0.20m, 0.32m, 0.192m, 0.1152m, 0.1152m, 0.0576m },
                _ => new[] { 0.20m, 0.32m, 0.192m, 0.1152m, 0.1152m, 0.0576m }
            };
        }

        public async Task<List<UsTaxSettings>> GetAllUsTaxSettingsAsync()
        {
            var companyId = GetCompanyId();
            return await _context.UsTaxSettings
                .Include(x => x.Asset)
                .Where(x => x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0))
                .ToListAsync();
        }

        public async Task<List<Section179Limits>> GetAllSection179LimitsAsync()
        {
            return await _context.Section179Limits
                .OrderByDescending(x => x.TaxYear)
                .ToListAsync();
        }

        public async Task<List<BonusDepreciationRates>> GetAllBonusRatesAsync()
        {
            return await _context.BonusDepreciationRates
                .OrderByDescending(x => x.TaxYear)
                .ToListAsync();
        }

        public string GetPropertyClassDescription(MacrsPropertyClass propertyClass)
        {
            return propertyClass switch
            {
                MacrsPropertyClass.ThreeYear => "3-Year Property (Tractors, race horses)",
                MacrsPropertyClass.FiveYear => "5-Year Property (Autos, computers, office equipment)",
                MacrsPropertyClass.SevenYear => "7-Year Property (Office furniture, fixtures)",
                MacrsPropertyClass.TenYear => "10-Year Property (Vessels, barges, tugs)",
                MacrsPropertyClass.FifteenYear => "15-Year Property (Land improvements, pipelines)",
                MacrsPropertyClass.TwentyYear => "20-Year Property (Farm buildings, municipal sewers)",
                MacrsPropertyClass.TwentySevenAndHalfYear => "27.5-Year Property (Residential rental)",
                MacrsPropertyClass.ThirtyNineYear => "39-Year Property (Nonresidential real property)",
                _ => "Unknown Property Class"
            };
        }

        public string GetConventionDescription(MacrsConvention convention)
        {
            return convention switch
            {
                MacrsConvention.HalfYear => "Half-Year Convention",
                MacrsConvention.MidMonth => "Mid-Month Convention",
                MacrsConvention.MidQuarter => "Mid-Quarter Convention",
                _ => "Unknown Convention"
            };
        }
    }
}
