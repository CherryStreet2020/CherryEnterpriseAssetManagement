using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Cip
{
    public class CipCapitalizationPreview
    {
        public int CipProjectId { get; set; }
        public string ProjectNumber { get; set; } = string.Empty;
        public decimal TotalCapitalizableCosts { get; set; }
        public decimal TotalNonCapitalizableCosts { get; set; }
        public decimal ProposedAssetBasis { get; set; }
        public int CostCount { get; set; }
        public List<CipCostTypeTotals> ByType { get; set; } = new();
    }

    public class CipCapitalizationService
    {
        private readonly AppDbContext _db;
        private readonly CipCostService _cipCostService;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public CipCapitalizationService(AppDbContext db, CipCostService cipCostService, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _cipCostService = cipCostService;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public async Task<CipCapitalizationPreview?> PreviewAsync(int cipProjectId)
        {
            var project = await _db.CipProjects
                .Include(p => p.Costs)
                .FirstOrDefaultAsync(p => p.Id == cipProjectId);
            if (project == null) return null;

            var costs = project.Costs?.ToList() ?? new List<CipCost>();
            var costTypeValues = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipCostType");
            var lvMap = costTypeValues.ToDictionary(v => v.Id, v => v);

            var byType = costs
                .Where(c => c.IsCapitalizable)
                .GroupBy(c => c.CostTypeLookupValueId)
                .Select(g =>
                {
                    string name = "Unknown", code = "";
                    if (g.Key.HasValue && lvMap.TryGetValue(g.Key.Value, out var lv))
                    {
                        name = lv.Name;
                        code = lv.Code;
                    }
                    return new CipCostTypeTotals
                    {
                        LookupValueId = g.Key,
                        TypeName = name,
                        TypeCode = code,
                        Amount = g.Sum(c => c.Amount),
                        Count = g.Count()
                    };
                })
                .OrderBy(t => t.TypeName)
                .ToList();

            var capitalizableTotal = costs.Where(c => c.IsCapitalizable).Sum(c => c.Amount);
            var nonCapitalizableTotal = costs.Where(c => !c.IsCapitalizable).Sum(c => c.Amount);

            return new CipCapitalizationPreview
            {
                CipProjectId = cipProjectId,
                ProjectNumber = project.ProjectNumber,
                TotalCapitalizableCosts = capitalizableTotal,
                TotalNonCapitalizableCosts = nonCapitalizableTotal,
                ProposedAssetBasis = capitalizableTotal,
                CostCount = costs.Count,
                ByType = byType
            };
        }

        public async Task<(Asset? asset, CipCapitalization? capitalization)> CapitalizeAsync(
            int cipProjectId,
            string assetNumber,
            string description,
            int? glAccountId = null,
            string? userId = null)
        {
            var project = await _db.CipProjects
                .Include(p => p.Costs)
                .FirstOrDefaultAsync(p => p.Id == cipProjectId);
            if (project == null)
                throw new InvalidOperationException($"CIP project {cipProjectId} not found.");
            if (project.IsLocked)
                throw new InvalidOperationException($"CIP project {cipProjectId} is already capitalized/locked.");

            var costs = project.Costs?.ToList() ?? new List<CipCost>();
            var capitalizableAmount = costs.Where(c => c.IsCapitalizable).Sum(c => c.Amount);

            var asset = new Asset
            {
                AssetNumber = assetNumber,
                Description = description,
                InServiceDate = DateTime.UtcNow,
                AcquisitionCost = capitalizableAmount,
                Status = AssetStatus.Active
            };
            _db.Assets.Add(asset);
            await _db.SaveChangesAsync();

            var journalEntry = new JournalEntry
            {
                Batch = $"CIP-CAP-{project.ProjectNumber}",
                Period = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
                PostingDate = DateTime.UtcNow.Date,
                Source = "CIP Capitalization",
                Reference = project.ProjectNumber,
                Description = $"Capitalization of CIP {project.ProjectNumber}: {project.Name}",
                Lines = new List<JournalLine>
                {
                    new JournalLine
                    {
                        LineNo = 1,
                        Account = "1500",
                        Description = $"Fixed Asset - {assetNumber}",
                        Debit = capitalizableAmount,
                        Credit = 0m
                    },
                    new JournalLine
                    {
                        LineNo = 2,
                        Account = "1400",
                        Description = $"CIP WIP - {project.ProjectNumber}",
                        Debit = 0m,
                        Credit = capitalizableAmount
                    }
                }
            };
            _db.JournalEntries.Add(journalEntry);
            await _db.SaveChangesAsync();

            var capitalization = new CipCapitalization
            {
                CipProjectId = cipProjectId,
                AssetId = asset.Id,
                JournalEntryId = journalEntry.Id,
                CapitalizedAt = DateTime.UtcNow,
                CapitalizedByUserId = userId ?? "system",
                TotalCapitalized = capitalizableAmount,
                CostMappings = costs.Where(c => c.IsCapitalizable).Select(c => new CipCapitalizationCost
                {
                    CipCostId = c.Id
                }).ToList()
            };
            _db.CipCapitalizations.Add(capitalization);

            project.IsCapitalized = true;
            project.CapitalizedAt = DateTime.UtcNow;
            project.Status = CipProjectStatus.Capitalized;
            project.ConvertedAssetId = asset.Id;
            project.ActualCompletionDate = DateTime.UtcNow;
            project.PlacedInServiceDate = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return (asset, capitalization);
        }
    }
}
