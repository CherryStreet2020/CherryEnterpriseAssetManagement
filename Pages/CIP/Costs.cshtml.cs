using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.CIP
{
    public class CostsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public CostsModel(AppDbContext context,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _tenantContext = tenantContext;
        }

        public int TotalCosts { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CapitalizableAmount { get; set; }
        public decimal NonCapitalizableAmount { get; set; }
        public int ProjectCount { get; set; }
        public List<ProjectCostSummary> ProjectCosts { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("projects"))
                return RedirectToPage("/ModuleDisabled", new { module = "CIP" });


            var visibleIds = _tenantContext.VisibleCompanyIds;

            var projectQuery = _context.CipProjects
                .Include(p => p.Costs!)
                .Where(p => visibleIds.Contains(p.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                projectQuery = projectQuery.Where(p => p.SiteId == _tenantContext.SiteId.Value);

            var projects = await projectQuery
                .OrderByDescending(p => p.Costs!.Sum(c => c.Amount))
                .ToListAsync();

            ProjectCount = projects.Count;
            TotalCosts = projects.Sum(p => (p.Costs ?? new List<CipCost>()).Count);
            TotalAmount = projects.Sum(p => (p.Costs ?? new List<CipCost>()).Sum(c => c.Amount));
            CapitalizableAmount = projects.Sum(p => (p.Costs ?? new List<CipCost>()).Where(c => c.IsCapitalizable).Sum(c => c.Amount));
            NonCapitalizableAmount = TotalAmount - CapitalizableAmount;

            ProjectCosts = projects
                .Where(p => (p.Costs ?? new List<CipCost>()).Any())
                .Select(p => new ProjectCostSummary
                {
                    ProjectId = p.Id,
                    ProjectNumber = p.ProjectNumber,
                    Name = p.Name,
                    Status = p.Status.ToString(),
                    CostCount = (p.Costs ?? new List<CipCost>()).Count,
                    TotalAmount = (p.Costs ?? new List<CipCost>()).Sum(c => c.Amount),
                    CapitalizableAmount = (p.Costs ?? new List<CipCost>()).Where(c => c.IsCapitalizable).Sum(c => c.Amount)
                })
                .ToList();
        
            return Page();
        }

        public class ProjectCostSummary
        {
            public int ProjectId { get; set; }
            public string ProjectNumber { get; set; } = "";
            public string Name { get; set; } = "";
            public string Status { get; set; } = "";
            public int CostCount { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal CapitalizableAmount { get; set; }
        }
    }
}
