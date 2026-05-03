using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abs.FixedAssets.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, 
        IOptions<TenantSettings> settings, AppDbContext db,
        ICompanyHierarchyService hierarchyService)
    {
        if (context.Items.ContainsKey("OrgNodeId"))
        {
            if (tenantContext.VisibleCompanyIds.Count == 0 && tenantContext.IsResolved && tenantContext.TenantId.HasValue)
            {
                await ResolveHierarchyAsync(context, tenantContext, db, hierarchyService);
            }
            await _next(context);
            return;
        }

        var config = settings.Value;
        
        if (config.DeploymentMode == DeploymentMode.SingleTenant)
        {
            await ResolveSingleTenantAsync(tenantContext, config, db);
        }
        else
        {
            var resolved = await ResolveMultiTenantAsync(context, tenantContext, db);
            if (!resolved && !IsExemptPath(context.Request.Path))
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync($"{{\"error\":\"{tenantContext.ResolutionError ?? "Tenant resolution failed"}\"}}");
                return;
            }
        }

        await ResolveHierarchyAsync(context, tenantContext, db, hierarchyService);

        if (!context.Items.ContainsKey("OrgNodeId"))
        {
            var siteCookie = context.Request.Cookies["cherryai_site_id"];
            if (!string.IsNullOrEmpty(siteCookie) && int.TryParse(siteCookie, out var cookieSiteId))
            {
                if (tenantContext.VisibleSiteIds.Contains(cookieSiteId))
                {
                    tenantContext.SetContext(tenantContext.TenantId, tenantContext.CompanyId, cookieSiteId);
                }
            }
        }

        await _next(context);
    }

    private async Task ResolveHierarchyAsync(HttpContext context, ITenantContext tenantContext,
        AppDbContext db, ICompanyHierarchyService hierarchyService)
    {
        if (!tenantContext.IsResolved || !tenantContext.TenantId.HasValue)
            return;

        int? assignedCompanyId = null;
        int? assignedSiteId = null;
        var userIdClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            assignedCompanyId = user?.AssignedCompanyId;
            assignedSiteId = user?.AssignedSiteId;
        }

        var visibleIds = await hierarchyService.GetVisibleCompanyIdsAsync(
            tenantContext.TenantId.Value, assignedCompanyId);

        tenantContext.SetHierarchyContext(assignedCompanyId, visibleIds);

        if (tenantContext.CompanyId.HasValue && !visibleIds.Contains(tenantContext.CompanyId.Value))
        {
            var fallbackCompanyId = assignedCompanyId ?? visibleIds.FirstOrDefault();
            if (fallbackCompanyId > 0)
            {
                tenantContext.SetContext(tenantContext.TenantId, fallbackCompanyId, tenantContext.SiteId);
            }
        }

        var visibleSiteIds = await hierarchyService.GetVisibleSiteIdsAsync(visibleIds, assignedSiteId);
        tenantContext.SetSiteHierarchyContext(assignedSiteId, visibleSiteIds);

        if (tenantContext.SiteId.HasValue && !visibleSiteIds.Contains(tenantContext.SiteId.Value))
        {
            tenantContext.SetContext(tenantContext.TenantId, tenantContext.CompanyId, null);
        }
    }

    // Memoize the resolved single-tenant (tenantId, companyId) so we don't
    // hit the DB twice per request; only cached after a successful lookup.
    private static (int? TenantId, int? CompanyId)? _singleTenantCache;
    private static readonly SemaphoreSlim _singleTenantLock = new(1, 1);

    private async Task ResolveSingleTenantAsync(ITenantContext tenantContext, TenantSettings config, AppDbContext db)
    {
        var cached = _singleTenantCache;
        if (cached.HasValue)
        {
            tenantContext.SetContext(cached.Value.TenantId, cached.Value.CompanyId, null);
            return;
        }

        await _singleTenantLock.WaitAsync();
        try
        {
            cached = _singleTenantCache;
            if (cached.HasValue)
            {
                tenantContext.SetContext(cached.Value.TenantId, cached.Value.CompanyId, null);
                return;
            }

            var tenantId = config.DefaultTenantId;
            var companyId = config.DefaultCompanyId;

            var tenant = await db.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                tenant = await db.Tenants.FirstOrDefaultAsync();
                tenantId = tenant?.Id ?? 0;
            }

            var company = await db.Companies.FindAsync(companyId);
            if (company == null)
            {
                company = await db.Companies.FirstOrDefaultAsync();
                companyId = company?.Id ?? 0;
            }

            var resolved = (
                TenantId: tenantId > 0 ? (int?)tenantId : null,
                CompanyId: companyId > 0 ? (int?)companyId : null
            );

            // Only cache once we successfully resolved both — otherwise a
            // transient empty-DB state at startup would poison the cache.
            if (resolved.TenantId.HasValue && resolved.CompanyId.HasValue)
            {
                _singleTenantCache = resolved;
            }

            tenantContext.SetContext(resolved.TenantId, resolved.CompanyId, null);
        }
        finally
        {
            _singleTenantLock.Release();
        }
    }

    private async Task<bool> ResolveMultiTenantAsync(HttpContext context, ITenantContext tenantContext, AppDbContext db)
    {
        var tenantHeader = context.Request.Headers["X-CherryAI-Tenant"].FirstOrDefault();
        var companyHeader = context.Request.Headers["X-CherryAI-Company"].FirstOrDefault();
        var siteHeader = context.Request.Headers["X-CherryAI-Site"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantHeader))
        {
            tenantContext.SetError("Missing required header: X-CherryAI-Tenant");
            return false;
        }

        int? tenantId = null;
        int? companyId = null;
        int? siteId = null;

        if (int.TryParse(tenantHeader, out var tid))
        {
            var tenant = await db.Tenants.FindAsync(tid);
            if (tenant == null || !tenant.IsActive)
            {
                tenantContext.SetError($"Tenant not found or inactive: {tenantHeader}");
                return false;
            }
            tenantId = tid;
        }
        else
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Code == tenantHeader && t.IsActive);
            if (tenant == null)
            {
                tenantContext.SetError($"Tenant not found: {tenantHeader}");
                return false;
            }
            tenantId = tenant.Id;
        }

        if (!string.IsNullOrEmpty(companyHeader))
        {
            if (int.TryParse(companyHeader, out var cid))
            {
                var company = await db.Companies.FindAsync(cid);
                if (company != null && company.TenantId == tenantId)
                    companyId = cid;
            }
            else
            {
                var company = await db.Companies.FirstOrDefaultAsync(c => c.CompanyCode == companyHeader && c.TenantId == tenantId);
                companyId = company?.Id;
            }
        }

        if (!string.IsNullOrEmpty(siteHeader))
        {
            if (int.TryParse(siteHeader, out var sid))
            {
                var site = await db.Sites.FindAsync(sid);
                if (site != null && (companyId == null || site.CompanyId == companyId))
                    siteId = sid;
            }
            else
            {
                var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteCode == siteHeader && (companyId == null || s.CompanyId == companyId));
                siteId = site?.Id;
            }
        }

        tenantContext.SetContext(tenantId, companyId, siteId);
        return true;
    }

    private bool IsExemptPath(PathString path)
    {
        var exemptPaths = new[] { "/Account/Login", "/Account/Logout", "/health", "/api/health" };
        return exemptPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}
