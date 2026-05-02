using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CIP
{
    public class CostTypeDetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public CostTypeDetailsModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        [BindProperty(SupportsGet = true)]
        public int? Type { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? LookupValueId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string CostTypeName { get; set; } = "";
        public string FilterLabel { get; set; } = "All Projects";
        public List<CipCost> CostEntries { get; set; } = new();
        public decimal TotalAmount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("projects"))
                return RedirectToPage("/ModuleDisabled", new { module = "CIP" });

            int? resolvedLookupValueId = LookupValueId;

            if (resolvedLookupValueId == null && Type.HasValue)
            {
                var values = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipCostType");
                var match = values.FirstOrDefault(v => v.SortOrder == Type.Value + 1);
                resolvedLookupValueId = match?.Id;
                CostTypeName = match?.Name ?? $"Cost Type {Type.Value}";
            }

            if (resolvedLookupValueId.HasValue && string.IsNullOrEmpty(CostTypeName))
            {
                var lv = await _context.Set<LookupValue>()
                    .Where(v => v.Id == resolvedLookupValueId.Value)
                    .FirstOrDefaultAsync();
                CostTypeName = lv?.Name ?? "Unknown";
            }

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var projectQuery = _context.CipProjects.Where(p => visibleIds.Contains(p.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                projectQuery = projectQuery.Where(p => p.SiteId == _tenantContext.SiteId.Value);
            
            if (!string.IsNullOrEmpty(Filter))
            {
                projectQuery = Filter.ToLower() switch
                {
                    "active" => projectQuery.Where(p => p.Status == CipProjectStatus.Active),
                    "planned" => projectQuery.Where(p => p.Status == CipProjectStatus.Planned),
                    "completed" => projectQuery.Where(p => p.Status == CipProjectStatus.Completed),
                    "capitalized" => projectQuery.Where(p => p.Status == CipProjectStatus.Capitalized),
                    "onhold" => projectQuery.Where(p => p.Status == CipProjectStatus.OnHold),
                    _ => projectQuery
                };

                FilterLabel = Filter.ToLower() switch
                {
                    "active" => "Active Projects",
                    "planned" => "Planned Projects",
                    "completed" => "Completed Projects",
                    "capitalized" => "Capitalized Projects",
                    "onhold" => "On Hold Projects",
                    _ => "All Projects"
                };
            }

            var projectIds = await projectQuery.Select(p => p.Id).ToListAsync();

            CostEntries = await _context.CipCosts
                .Include(c => c.Project)
                .Where(c => c.CostTypeLookupValueId == resolvedLookupValueId && projectIds.Contains(c.CipProjectId))
                .OrderByDescending(c => c.TransactionDate)
                .ToListAsync();

            TotalAmount = CostEntries.Sum(c => c.Amount);

            ViewData["ReturnUrl"] = ReturnUrl;
            ViewData["Breadcrumbs"] = new List<(string Label, string Href)>
            {
                ("Projects", "/CIP"),
                ("CIP", "/CIP"),
                ("Cost Type Details", "")
            };
            ViewData["ShowBackLink"] = true;
            ViewData["BackLinkFallback"] = "/CIP";
            ViewData["BackLinkLabel"] = "Back to results";

            return Page();
        }
    }
}
