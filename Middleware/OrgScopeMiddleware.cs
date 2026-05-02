using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Middleware
{
    public class OrgScopeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<OrgScopeMiddleware> _logger;

        public OrgScopeMiddleware(RequestDelegate next, ILogger<OrgScopeMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, AppDbContext db,
            ICompanyHierarchyService hierarchyService)
        {
            var tenantIdHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            var orgNodeIdHeader = context.Request.Headers["X-Org-Node-Id"].FirstOrDefault();
            var userIdHeader = context.Request.Headers["X-User-Id"].FirstOrDefault();

            if (!string.IsNullOrEmpty(tenantIdHeader) && !string.IsNullOrEmpty(orgNodeIdHeader))
            {
                if (Guid.TryParse(orgNodeIdHeader, out var orgNodeId))
                {
                    if (orgNodeId == Guid.Empty)
                    {
                        _logger.LogDebug("OrgScope: bootstrap UUID received, skipping scope resolution");
                        await _next(context);
                        return;
                    }

                    var tenant = await db.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Code == tenantIdHeader && t.IsActive);

                    if (tenant == null)
                    {
                        await _next(context);
                        return;
                    }

                    int? assignedCompanyId = null;
                    var userIdClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
                    {
                        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                        assignedCompanyId = user?.AssignedCompanyId;
                    }

                    var visibleIds = await hierarchyService.GetVisibleCompanyIdsAsync(tenant.Id, assignedCompanyId);

                    if (assignedCompanyId.HasValue && visibleIds.Count == 0)
                    {
                        _logger.LogWarning("OrgScope: scoped user (AssignedCompanyId={AssignedCompanyId}) resolved to empty visible companies — denying access", assignedCompanyId);
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Forbidden: assigned company not valid");
                        return;
                    }

                    var node = await db.OrgNodes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.Id == orgNodeId && n.TenantCode == tenantIdHeader && n.IsActive);

                    if (node != null)
                    {
                        if (node.CompanyId.HasValue && visibleIds.Count > 0 &&
                            !visibleIds.Contains(node.CompanyId.Value))
                        {
                            _logger.LogWarning("OrgScope: company {CompanyId} not in visible companies for user, rejecting scope switch", node.CompanyId);
                            var isApiRequest = context.Request.Path.StartsWithSegments("/api");
                            if (isApiRequest)
                            {
                                context.Response.StatusCode = 403;
                                await context.Response.WriteAsync("Forbidden: company not in visible scope");
                                return;
                            }
                            await _next(context);
                            return;
                        }

                        int? tenantId = tenant.Id;
                        int? companyId = node.CompanyId;
                        int? siteId = node.SiteId;

                        if (companyId.HasValue && !siteId.HasValue && node.NodeType == "site")
                        {
                            siteId = node.SiteId;
                        }

                        if (node.NodeType == "location" && !siteId.HasValue)
                        {
                            var parentSite = await db.OrgNodes
                                .AsNoTracking()
                                .FirstOrDefaultAsync(n => n.Id == node.ParentId && n.NodeType == "site");
                            if (parentSite != null)
                            {
                                siteId = parentSite.SiteId;
                            }
                        }

                        if (node.NodeType == "holding")
                        {
                            companyId = null;
                            siteId = null;
                        }

                        tenantContext.SetContext(tenantId, companyId, siteId);
                        tenantContext.SetHierarchyContext(assignedCompanyId, visibleIds);

                        int? assignedSiteId = null;
                        if (userIdClaim != null && int.TryParse(userIdClaim, out var parsedUserId))
                        {
                            var userForSite = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == parsedUserId);
                            assignedSiteId = userForSite?.AssignedSiteId;
                        }
                        var visibleSiteIds = await hierarchyService.GetVisibleSiteIdsAsync(visibleIds, assignedSiteId);
                        tenantContext.SetSiteHierarchyContext(assignedSiteId, visibleSiteIds);

                        if (siteId.HasValue && !visibleSiteIds.Contains(siteId.Value))
                        {
                            tenantContext.SetContext(tenantId, companyId, null);
                        }

                        if (!string.IsNullOrEmpty(userIdHeader))
                        {
                            context.Items["UserId"] = userIdHeader;
                        }

                        context.Items["OrgNodeId"] = orgNodeId;
                        context.Items["OrgNodeType"] = node.NodeType;

                        _logger.LogDebug("OrgScope resolved: tenant={TenantId}, company={CompanyId}, site={SiteId}, nodeType={NodeType}",
                            tenantId, companyId, siteId, node.NodeType);
                    }
                }
            }

            await _next(context);
        }
    }

    public static class OrgScopeMiddlewareExtensions
    {
        public static IApplicationBuilder UseOrgScope(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<OrgScopeMiddleware>();
        }
    }
}
