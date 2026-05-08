using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Cip
{
    public class CipCostTypeTotals
    {
        public int? LookupValueId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string TypeCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public class CipCostSummary
    {
        public decimal TotalSpent { get; set; }
        public decimal BudgetAmount { get; set; }
        public decimal Remaining => BudgetAmount - TotalSpent;
        public decimal PercentUsed => BudgetAmount > 0 ? (TotalSpent / BudgetAmount * 100) : 0;
        public int TotalCostCount { get; set; }
        public List<CipCostTypeTotals> ByType { get; set; } = new();
    }

    public class CipCostService
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public CipCostService(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<CipCost> AddManualCostAsync(
            int cipProjectId,
            int costTypeLookupValueId,
            decimal amount,
            DateTime costDate,
            string description,
            int? vendorId = null,
            string? notes = null,
            string? userId = null)
        {
            var companyId = GetCompanyId();
            var project = await _db.CipProjects.Where(p => p.Id == cipProjectId && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (project == null)
                throw new InvalidOperationException($"CIP project {cipProjectId} not found.");
            if (project.IsLocked)
                throw new InvalidOperationException($"CIP project {cipProjectId} is locked (capitalized). No new costs allowed.");

            var resolvedCostType = CipCostType.Construction;
            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, costTypeLookupValueId);
            if (lv != null && int.TryParse(lv.Code, out var enumVal))
                resolvedCostType = (CipCostType)enumVal;

            var cost = new CipCost
            {
                CipProjectId = cipProjectId,
                CostTypeLookupValueId = costTypeLookupValueId,
                CostType = resolvedCostType,
                Amount = amount,
                TransactionDate = DateTime.SpecifyKind(costDate, DateTimeKind.Utc),
                Description = description,
                VendorId = vendorId,
                Notes = notes,
                SourceType = "MANUAL", // pre-uppercased to match AppDbContext.CapitalizeStringProperties
                SourceDisplayRef = $"Manual entry",
                EnteredBy = userId ?? "system",
                CreatedByUserId = userId ?? "system",
                IsCapitalizable = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.CipCosts.Add(cost);
            await _db.SaveChangesAsync();

            await ReconcileProjectTotalAsync(cipProjectId);
            return cost;
        }

        public async Task<CipCostSummary> ComputeTotalsAsync(int cipProjectId)
        {
            var companyId = GetCompanyId();
            var project = await _db.CipProjects.Where(p => p.Id == cipProjectId && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (project == null)
                throw new InvalidOperationException($"CIP project {cipProjectId} not found.");

            var costs = await _db.CipCosts
                .Where(c => c.CipProjectId == cipProjectId)
                .ToListAsync();

            var costTypeValues = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipCostType");
            var lvMap = costTypeValues.ToDictionary(v => v.Id, v => v);

            var grouped = costs
                .GroupBy(c => c.CostTypeLookupValueId)
                .Select(g =>
                {
                    var lvId = g.Key;
                    string name = "Unknown", code = "";
                    if (lvId.HasValue && lvMap.TryGetValue(lvId.Value, out var lv))
                    {
                        name = lv.Name;
                        code = lv.Code;
                    }
                    return new CipCostTypeTotals
                    {
                        LookupValueId = lvId,
                        TypeName = name,
                        TypeCode = code,
                        Amount = g.Sum(c => c.Amount),
                        Count = g.Count()
                    };
                })
                .OrderBy(t => t.TypeName)
                .ToList();

            return new CipCostSummary
            {
                TotalSpent = costs.Sum(c => c.Amount),
                BudgetAmount = project.BudgetAmount,
                TotalCostCount = costs.Count,
                ByType = grouped
            };
        }

        public async Task ReconcileProjectTotalAsync(int cipProjectId)
        {
            var companyId = GetCompanyId();
            var project = await _db.CipProjects.Where(p => p.Id == cipProjectId && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (project == null) return;

            var totalCosts = await _db.CipCosts
                .Where(c => c.CipProjectId == cipProjectId)
                .SumAsync(c => c.Amount);

            project.TotalCosts = totalCosts;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
