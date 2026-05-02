using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class CipService
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public CipService(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<List<CipProject>> GetAllProjectsAsync()
        {
            var companyId = GetCompanyId();
            var projects = await _context.CipProjects
                .Include(x => x.Costs)
                .Where(x => _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            foreach (var p in projects) ReconcileTotalCosts(p);
            return projects;
        }

        public async Task<List<CipProject>> GetActiveProjectsAsync()
        {
            var companyId = GetCompanyId();
            var projects = await _context.CipProjects
                .Include(x => x.Costs)
                .Where(x => _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0) && (x.Status == CipProjectStatus.Active || x.Status == CipProjectStatus.Planned))
                .OrderBy(x => x.EstimatedCompletionDate)
                .ToListAsync();
            foreach (var p in projects) ReconcileTotalCosts(p);
            return projects;
        }

        public async Task<CipProject?> GetProjectAsync(int id)
        {
            var companyId = GetCompanyId();
            var project = await _context.CipProjects
                .Include(x => x.Costs)
                .Include(x => x.ConvertedAsset)
                .Where(x => x.Id == id && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (project != null)
                ReconcileTotalCosts(project);

            return project;
        }

        public async Task<CipProject> CreateProjectAsync(CipProject project)
        {
            project.CompanyId = GetCompanyId();
            project.CreatedAt = DateTime.UtcNow;
            _context.CipProjects.Add(project);
            await _context.SaveChangesAsync();
            return project;
        }

        public async Task<CipProject> UpdateProjectAsync(CipProject project)
        {
            var companyId = GetCompanyId();
            var existing = await _context.CipProjects.Where(x => x.Id == project.Id && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (existing == null)
                throw new InvalidOperationException("CIP Project not found for this tenant");

            project.UpdatedAt = DateTime.UtcNow;
            _context.Entry(existing).CurrentValues.SetValues(project);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<CipCost> AddCostAsync(CipCost cost)
        {
            var companyId = GetCompanyId();
            var project = await _context.CipProjects
                .Include(x => x.Costs)
                .Where(x => x.Id == cost.CipProjectId && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (project == null)
                throw new InvalidOperationException("CIP Project not found for this tenant");

            cost.CreatedAt = DateTime.UtcNow;
            _context.CipCosts.Add(cost);
            await _context.SaveChangesAsync();

            project.TotalCosts = project.Costs?.Sum(c => c.Amount) ?? 0m;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return cost;
        }

        public async Task<List<CipCost>> GetProjectCostsAsync(int projectId)
        {
            var companyId = GetCompanyId();
            var projectExists = await _context.CipProjects.AnyAsync(x => x.Id == projectId && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0));
            if (!projectExists)
                return new List<CipCost>();

            return await _context.CipCosts
                .Where(x => x.CipProjectId == projectId)
                .OrderByDescending(x => x.TransactionDate)
                .ToListAsync();
        }

        public async Task<Asset?> CapitalizeProjectAsync(int projectId, string assetNumber, string description)
        {
            var companyId = GetCompanyId();
            var project = await _context.CipProjects
                .Include(x => x.Costs)
                .Where(x => x.Id == projectId && _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (project == null || project.Status == CipProjectStatus.Capitalized)
                return null;

            var capitalizableCosts = project.Costs?.Where(c => c.IsCapitalizable).Sum(c => c.Amount) ?? 0;

            var asset = new Asset
            {
                AssetNumber = assetNumber,
                Description = description,
                InServiceDate = DateTime.UtcNow,
                AcquisitionCost = capitalizableCosts,
                Status = AssetStatus.Active,
                CompanyId = companyId
            };

            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();

            project.Status = CipProjectStatus.Capitalized;
            project.ConvertedAssetId = asset.Id;
            project.ActualCompletionDate = DateTime.UtcNow;
            project.PlacedInServiceDate = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return asset;
        }

        public async Task<CipStats> GetCipStatsAsync()
        {
            var companyId = GetCompanyId();
            var projects = await _context.CipProjects
                .Include(x => x.Costs)
                .Where(x => _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .ToListAsync();
            foreach (var p in projects) ReconcileTotalCosts(p);

            return new CipStats
            {
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(x => x.Status == CipProjectStatus.Active),
                PlannedProjects = projects.Count(x => x.Status == CipProjectStatus.Planned),
                CompletedProjects = projects.Count(x => x.Status == CipProjectStatus.Completed || x.Status == CipProjectStatus.Capitalized),
                TotalBudget = projects.Sum(x => x.BudgetAmount),
                TotalSpent = projects.Sum(x => x.TotalCosts),
                TotalCommitted = projects.Sum(x => x.CommittedCosts)
            };
        }

        private static void ReconcileTotalCosts(CipProject project)
        {
            var computedTotal = project.Costs?.Sum(c => c.Amount) ?? 0m;
            project.TotalCosts = computedTotal;
        }

        public async Task<int> ReconcileAllProjectCostsAsync()
        {
            var companyId = GetCompanyId();
            var projects = await _context.CipProjects
                .Include(x => x.Costs)
                .Where(x => _tenantContext.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .ToListAsync();

            int reconciled = 0;
            foreach (var project in projects)
            {
                var computedTotal = project.Costs?.Sum(c => c.Amount) ?? 0m;
                if (project.TotalCosts != computedTotal)
                {
                    project.TotalCosts = computedTotal;
                    project.UpdatedAt = DateTime.UtcNow;
                    reconciled++;
                }
            }

            if (reconciled > 0)
                await _context.SaveChangesAsync();

            return reconciled;
        }
    }

    public class CipStats
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int PlannedProjects { get; set; }
        public int CompletedProjects { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalCommitted { get; set; }
        public decimal BudgetRemaining => TotalBudget - TotalSpent - TotalCommitted;
    }
}
