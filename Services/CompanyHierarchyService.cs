using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;

namespace Abs.FixedAssets.Services;

public interface ICompanyHierarchyService
{
    Task<List<int>> GetVisibleCompanyIdsAsync(int tenantId, int? assignedCompanyId);
    Task<List<CompanyNode>> GetHierarchyTreeAsync(int tenantId);
    Task<List<int>> GetDescendantIdsAsync(int companyId, int tenantId);
    Task<bool> CanAccessCompanyAsync(int tenantId, int? assignedCompanyId, int targetCompanyId);
    Task<List<int>> GetVisibleSiteIdsAsync(List<int> visibleCompanyIds, int? assignedSiteId);
    Task<List<SiteNode>> GetSitesForCompaniesAsync(List<int> companyIds);
}

public class SiteNode
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int CompanyId { get; set; }
}

public class CompanyNode
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int? ParentCompanyId { get; set; }
    public int Level { get; set; }
    public List<CompanyNode> Children { get; set; } = new();
}

public class CompanyHierarchyService : ICompanyHierarchyService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CompanyHierarchyService> _logger;
    private const int MaxHierarchyDepth = 20;

    public CompanyHierarchyService(AppDbContext db, ILogger<CompanyHierarchyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<int>> GetVisibleCompanyIdsAsync(int tenantId, int? assignedCompanyId)
    {
        if (assignedCompanyId == null || assignedCompanyId == 0)
        {
            return await _db.Companies
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .Select(c => c.Id)
                .ToListAsync();
        }

        var assignedCompany = await _db.Companies
            .Where(c => c.Id == assignedCompanyId.Value && c.TenantId == tenantId && c.IsActive)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (assignedCompany == 0)
        {
            _logger.LogWarning("AssignedCompanyId {CompanyId} not found in tenant {TenantId} or inactive — fail closed, returning empty list",
                assignedCompanyId, tenantId);
            return new List<int>();
        }

        var visited = new HashSet<int> { assignedCompany };
        var result = new List<int> { assignedCompany };
        await CollectDescendantsAsync(assignedCompany, tenantId, result, visited, 0);
        return result;
    }

    public async Task<List<int>> GetDescendantIdsAsync(int companyId, int tenantId)
    {
        var visited = new HashSet<int> { companyId };
        var result = new List<int>();
        await CollectDescendantsAsync(companyId, tenantId, result, visited, 0);
        return result;
    }

    public async Task<bool> CanAccessCompanyAsync(int tenantId, int? assignedCompanyId, int targetCompanyId)
    {
        var visible = await GetVisibleCompanyIdsAsync(tenantId, assignedCompanyId);
        return visible.Contains(targetCompanyId);
    }

    public async Task<List<CompanyNode>> GetHierarchyTreeAsync(int tenantId)
    {
        var companies = await _db.Companies
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CompanyNode
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.CompanyCode ?? "",
                ParentCompanyId = c.ParentCompanyId
            })
            .ToListAsync();

        var lookup = companies.ToDictionary(c => c.Id);
        var roots = new List<CompanyNode>();

        foreach (var company in companies)
        {
            if (company.ParentCompanyId == null || !lookup.ContainsKey(company.ParentCompanyId.Value))
            {
                company.Level = 0;
                roots.Add(company);
            }
            else
            {
                company.Level = -1;
                lookup[company.ParentCompanyId.Value].Children.Add(company);
            }
        }

        void SetLevels(List<CompanyNode> nodes, int level)
        {
            foreach (var node in nodes)
            {
                node.Level = level;
                SetLevels(node.Children, level + 1);
            }
        }
        SetLevels(roots, 0);

        return roots;
    }

    public async Task<List<int>> GetVisibleSiteIdsAsync(List<int> visibleCompanyIds, int? assignedSiteId)
    {
        if (assignedSiteId != null && assignedSiteId > 0)
        {
            var site = await _db.Sites
                .Where(s => s.Id == assignedSiteId && visibleCompanyIds.Contains(s.CompanyId) && s.Status == Models.SiteStatus.Active)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
            return site != 0 ? new List<int> { site } : new List<int>();
        }

        return await _db.Sites
            .Where(s => visibleCompanyIds.Contains(s.CompanyId) && s.Status == Models.SiteStatus.Active)
            .Select(s => s.Id)
            .ToListAsync();
    }

    public async Task<List<SiteNode>> GetSitesForCompaniesAsync(List<int> companyIds)
    {
        return await _db.Sites
            .Where(s => companyIds.Contains(s.CompanyId) && s.Status == Models.SiteStatus.Active)
            .OrderBy(s => s.CompanyId)
            .ThenBy(s => s.Name)
            .Select(s => new SiteNode
            {
                Id = s.Id,
                Name = s.Name,
                Code = s.SiteCode,
                CompanyId = s.CompanyId
            })
            .ToListAsync();
    }

    private async Task CollectDescendantsAsync(int parentId, int tenantId, List<int> result, HashSet<int> visited, int depth)
    {
        if (depth >= MaxHierarchyDepth)
        {
            _logger.LogWarning("Company hierarchy exceeded max depth {MaxDepth} at company {CompanyId} — possible circular reference",
                MaxHierarchyDepth, parentId);
            return;
        }

        var childIds = await _db.Companies
            .Where(c => c.ParentCompanyId == parentId && c.TenantId == tenantId && c.IsActive)
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var childId in childIds)
        {
            if (!visited.Add(childId))
            {
                _logger.LogWarning("Circular reference detected in company hierarchy: company {CompanyId} already visited", childId);
                continue;
            }
            result.Add(childId);
            await CollectDescendantsAsync(childId, tenantId, result, visited, depth + 1);
        }
    }
}
