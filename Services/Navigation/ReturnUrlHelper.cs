using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace Abs.FixedAssets.Services.Navigation;

public static class ReturnUrlHelper
{
    private static readonly HashSet<string> AllowedBasePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/Index",
        "/Assets", "/Assets/Asset", "/Assets/Locations", "/Assets/Register", "/Assets/Transfer",
        "/Maintenance", "/Maintenance/Index", "/Maintenance/Details", "/Maintenance/Schedules", "/Maintenance/Assignments",
        "/Maintenance/WorkRequests", "/Maintenance/WorkRequests/Index", "/Maintenance/WorkRequests/Details", "/Maintenance/WorkRequests/Create",
        "/Materials", "/Materials/Items", "/Materials/ItemEdit", "/Materials/Vendors", "/Materials/Manufacturers",
        "/Materials/Kits", "/Materials/PMTemplates", "/Materials/Categories",
        "/WorkOrders", "/WorkOrders/Index", "/WorkOrders/Details", "/WorkOrders/Execute", "/WorkOrders/Closeout",
        "/Admin", "/Admin/Users", "/Admin/Companies", "/Admin/Settings", "/Admin/AuditLog",
        "/Admin/PMSchedules", "/Admin/PMScheduleEdit", "/Admin/IntegrationEndpoints", "/Admin/IntegrationMappings",
        "/Reports", "/Reports/ReportHub", "/Reports/TrialBalance", "/Reports/Reliability", "/Reports/AssetReliability",
        "/Depreciation", "/Depreciation/Books", "/Depreciation/Calculate", "/Depreciation/Schedule",
        "/CIP", "/CIP/Index", "/CIP/Details", "/CIP/Costs",
        "/CCA", "/CCA/Index", "/CCA/ClassReport",
        "/Journals", "/Journals/Index", "/Journals/Details",
        "/Purchasing", "/Purchasing/Index", "/Purchasing/Details",
        "/Help", "/Help/Index", "/Help/Guides", "/Help/Glossary"
    };

    private static readonly Dictionary<string, string> CanonicalFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        { "/WorkOrders/Details", "/Maintenance" },
        { "/WorkOrders/Execute", "/Maintenance" },
        { "/WorkOrders/Closeout", "/Maintenance" },
        { "/Maintenance/Details", "/Maintenance" },
        { "/Maintenance/WorkRequests/Details", "/Maintenance/WorkRequests" },
        { "/Assets/Asset", "/Assets" },
        { "/Assets/Locations", "/Assets" },
        { "/Materials/Vendors", "/Materials/Items" },
        { "/Maintenance/PMTemplates", "/Maintenance" },
        { "/Materials/ItemEdit", "/Materials/Items" },
        { "/Materials/Manufacturers", "/Materials" },
        { "/Materials/PMTemplates", "/Materials" },
        { "/Purchasing/Details", "/Purchasing" },
        { "/CIP/Details", "/CIP" },
        { "/CIP/Costs", "/CIP" },
        { "/Journals/Details", "/Journals" },
        { "/CCA/ClassReport", "/CCA" },
        { "/Admin/PMScheduleEdit", "/Admin/PMSchedules" },
        { "/Admin/IntegrationMappings", "/Admin/IntegrationEndpoints" }
    };

    public static string BuildReturnUrl(HttpRequest request)
    {
        var path = request.Path.Value ?? "/";
        var query = request.QueryString.Value ?? "";
        return path + query;
    }

    public static bool IsSafeLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        // Check for dangerous characters BEFORE trimming (to catch \n/path attacks)
        if (returnUrl.Contains('\n') || returnUrl.Contains('\r') || returnUrl.Contains('\0'))
            return false;

        returnUrl = returnUrl.Trim();

        if (returnUrl.Contains("://") || returnUrl.StartsWith("//"))
            return false;

        if (returnUrl.Contains(".."))
            return false;

        if (!returnUrl.StartsWith("/"))
            return false;

        if (Regex.IsMatch(returnUrl, @"[<>""]") || returnUrl.Contains('\''))
            return false;

        var pathOnly = returnUrl.Split('?')[0].Split('#')[0];
        
        pathOnly = Regex.Replace(pathOnly, @"/+", "/");
        if (pathOnly.Length > 1 && pathOnly.EndsWith("/"))
            pathOnly = pathOnly.TrimEnd('/');

        foreach (var allowed in AllowedBasePaths)
        {
            if (pathOnly.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
            if (pathOnly.StartsWith(allowed + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string GetSafeReturnUrlOrDefault(string? returnUrl, string defaultUrl)
    {
        if (IsSafeLocalReturnUrl(returnUrl))
            return returnUrl!;
        return defaultUrl;
    }

    public static string GetCanonicalFallback(string currentPagePath)
    {
        var pathOnly = currentPagePath.Split('?')[0].Split('#')[0];
        
        if (CanonicalFallbacks.TryGetValue(pathOnly, out var fallback))
            return fallback;

        var segments = pathOnly.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 1)
            return "/" + segments[0];

        return "/";
    }

    public static string GetBackUrl(string? returnUrl, string currentPagePath)
    {
        var fallback = GetCanonicalFallback(currentPagePath);
        return GetSafeReturnUrlOrDefault(returnUrl, fallback);
    }
}
