using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Controllers
{
    [Route("api/v1/org")]
    [ApiController]
    public class OrgController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public OrgController(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        [HttpGet("sites")]
        public async Task<IActionResult> GetSites()
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            if (visibleIds.Count == 0)
                return Ok(new { sites = Array.Empty<object>(), currentSiteId = (int?)null, showAllSites = true });

            var companyIds = _tenantContext.CompanyId.HasValue
                ? new List<int> { _tenantContext.CompanyId.Value }
                : visibleIds;

            var visibleSiteIds = _tenantContext.VisibleSiteIds;
            var sitesQuery = _db.Sites
                .Where(s => companyIds.Contains(s.CompanyId) && s.Status == Models.SiteStatus.Active);

            if (_tenantContext.AssignedSiteId.HasValue && visibleSiteIds.Count > 0)
                sitesQuery = sitesQuery.Where(s => visibleSiteIds.Contains(s.Id));

            var sites = await sitesQuery
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.SiteCode, s.CompanyId })
                .ToListAsync();

            var showAllSites = _tenantContext.AssignedSiteId == null;

            return Ok(new
            {
                sites,
                currentSiteId = _tenantContext.SiteId,
                showAllSites
            });
        }

        [HttpPost("site")]
        public IActionResult SetSite([FromQuery] int? siteId)
        {
            if (siteId.HasValue && !_tenantContext.VisibleSiteIds.Contains(siteId.Value))
            {
                return StatusCode(403, "Site not in visible scope");
            }

            _tenantContext.SetContext(_tenantContext.TenantId, _tenantContext.CompanyId, siteId);
            return Ok(new { siteId });
        }

        [HttpGet("tree")]
        public async Task<IActionResult> GetTree()
        {
            var tenantCode = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var showAllCompanies = _tenantContext.AssignedCompanyId == null;

            var allNodes = await _db.OrgNodes
                .Where(n => n.TenantCode == tenantCode && n.IsActive)
                .Where(n => n.NodeType == "holding" || n.NodeType == "company")
                .OrderBy(n => n.SortOrder)
                .ThenBy(n => n.Name)
                .ToListAsync();

            if (allNodes.Count == 0)
                return Ok(new { rootId = (Guid?)null, totalNodes = 0, nodes = Array.Empty<object>(), showAllCompanies });

            var visibleNodeIds = new HashSet<Guid>();
            foreach (var node in allNodes)
            {
                if (node.NodeType == "holding" ||
                    (node.CompanyId.HasValue && visibleIds.Contains(node.CompanyId.Value)))
                {
                    visibleNodeIds.Add(node.Id);
                    var current = node;
                    while (current.ParentId.HasValue)
                    {
                        var parent = allNodes.FirstOrDefault(n => n.Id == current.ParentId);
                        if (parent == null || !visibleNodeIds.Add(parent.Id)) break;
                        current = parent;
                    }
                }
            }

            var filteredNodes = allNodes.Where(n => visibleNodeIds.Contains(n.Id)).ToList();

            var holdingNode = filteredNodes.FirstOrDefault(n => n.NodeType == "holding");
            var rootId = holdingNode?.Id ?? filteredNodes.First().Id;

            var result = new List<object>();

            void Flatten(Guid nodeId, int indent)
            {
                var node = filteredNodes.FirstOrDefault(n => n.Id == nodeId);
                if (node == null) return;

                result.Add(new
                {
                    id = node.Id,
                    parentId = node.ParentId,
                    nodeType = node.NodeType,
                    name = node.Name,
                    code = node.Code,
                    indentLevel = indent,
                    companyId = node.CompanyId,
                    siteId = node.SiteId
                });

                var children = filteredNodes
                    .Where(n => n.ParentId == nodeId)
                    .OrderBy(n => n.SortOrder)
                    .ThenBy(n => n.Name);

                foreach (var child in children)
                {
                    Flatten(child.Id, indent + 1);
                }
            }

            Flatten(rootId, 0);

            return Ok(new
            {
                rootId = rootId,
                totalNodes = result.Count,
                nodes = result,
                showAllCompanies
            });
        }
    }
}
