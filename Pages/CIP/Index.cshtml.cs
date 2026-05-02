using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CIP
{
    public class IndexModel : PageModel
    {
        private readonly CipService _cipService;
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public IndexModel(CipService cipService, AppDbContext context, ILookupService lookupService, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _cipService = cipService;
            _context = context;
            _lookupService = lookupService;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public CipStats Stats { get; set; } = new();
        public List<CipProject> Projects { get; set; } = new();
        public List<CipProject> AllProjects { get; set; } = new();
        public List<ProjectManager> ProjectManagers { get; set; } = new();
        public List<Location> Locations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<GlAccount> GlAccounts { get; set; } = new();
        public Dictionary<int, decimal> CostsByLookupId { get; set; } = new();
        public List<LookupValueDto> CipCostTypeLookups { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }
        
        public string FilterLabel { get; set; } = "All Projects";

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("projects"))
                return RedirectToPage("/ModuleDisabled", new { module = "CIP" });

            Stats = await _cipService.GetCipStatsAsync();
            AllProjects = await _cipService.GetAllProjectsAsync();

            if (_tenantContext.SiteId.HasValue)
                AllProjects = AllProjects.Where(p => p.SiteId == _tenantContext.SiteId.Value).ToList();

            var visibleIds = _tenantContext.VisibleCompanyIds;

            ProjectManagers = await _context.ProjectManagers.Where(pm => pm.Active).OrderBy(pm => pm.Name).ToListAsync();
            Locations = await _context.Locations.Where(l => l.CompanyId == null || visibleIds.Contains(l.CompanyId ?? 0)).OrderBy(l => l.Name).ToListAsync();
            Departments = await _context.Departments.Where(d => d.CompanyId == null || visibleIds.Contains(d.CompanyId ?? 0)).OrderBy(d => d.Name).ToListAsync();
            GlAccounts = await _context.GlAccounts.Where(g => g.CompanyId == null || visibleIds.Contains(g.CompanyId ?? 0)).OrderBy(g => g.AccountNumber).ToListAsync();
            
            Projects = Filter?.ToLower() switch
            {
                "active" => AllProjects.Where(p => p.Status == CipProjectStatus.Active).ToList(),
                "planned" => AllProjects.Where(p => p.Status == CipProjectStatus.Planned).ToList(),
                "completed" => AllProjects.Where(p => p.Status == CipProjectStatus.Completed).ToList(),
                "capitalized" => AllProjects.Where(p => p.Status == CipProjectStatus.Capitalized).ToList(),
                "onhold" => AllProjects.Where(p => p.Status == CipProjectStatus.OnHold).ToList(),
                _ => AllProjects
            };
            
            FilterLabel = Filter?.ToLower() switch
            {
                "active" => "Active Projects",
                "planned" => "Planned Projects",
                "completed" => "Completed Projects",
                "capitalized" => "Capitalized Projects",
                "onhold" => "On Hold Projects",
                _ => "All Projects"
            };
            
            var projectIds = Projects.Select(p => p.Id).ToList();
            var costEntries = await _context.CipCosts
                .Where(c => projectIds.Contains(c.CipProjectId))
                .ToListAsync();
            
            CipCostTypeLookups = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipCostType");

            foreach (var lv in CipCostTypeLookups)
            {
                CostsByLookupId[lv.Id] = costEntries
                    .Where(c => c.CostTypeLookupValueId == lv.Id)
                    .Sum(c => c.Amount);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCreateProjectAsync(
            string projectNumber,
            string name,
            string? description,
            DateTime startDate,
            DateTime? estimatedCompletionDate,
            decimal budgetAmount,
            string? location,
            string? department,
            string? glAccount,
            int? projectManagerId)
        {
            var project = new CipProject
            {
                ProjectNumber = projectNumber,
                Name = name,
                Description = description,
                StartDate = startDate,
                EstimatedCompletionDate = estimatedCompletionDate,
                BudgetAmount = budgetAmount,
                Location = location,
                Department = department,
                GlAccount = glAccount,
                ProjectManagerId = projectManagerId,
                Status = CipProjectStatus.Planned
            };

            await _cipService.CreateProjectAsync(project);
            return RedirectToPage();
        }
    }
}
