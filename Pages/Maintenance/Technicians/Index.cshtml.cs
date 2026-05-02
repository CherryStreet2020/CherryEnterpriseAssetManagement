using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Maintenance.Technicians
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public IndexModel(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public List<Technician> Technicians { get; set; } = new();

        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int AvailableCount { get; set; }
        public double AvgUtilization { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Craft { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SiteId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Shift { get; set; }

        public List<Site> AvailableSites { get; set; } = new();

        public async Task OnGetAsync()
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;

            var query = _context.Technicians
                .Include(t => t.Certifications)
                .Include(t => t.Site)
                .Include(t => t.Company)
                .Where(t => t.CompanyId == null || visibleIds.Contains(t.CompanyId ?? 0))
                .AsQueryable();

            if (_tenantContext.SiteId.HasValue && !SiteId.HasValue)
            {
                query = query.Where(t => t.SiteId == null || t.SiteId == _tenantContext.SiteId.Value);
            }

            if (SiteId.HasValue)
            {
                query = query.Where(t => t.SiteId == SiteId.Value);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim().ToLower();
                query = query.Where(t =>
                    t.Name.ToLower().Contains(s) ||
                    (t.EmployeeId != null && t.EmployeeId.ToLower().Contains(s)) ||
                    (t.Email != null && t.Email.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(Craft))
            {
                query = query.Where(t => t.PrimaryCraft == Craft || t.SecondaryCraft == Craft);
            }

            if (!string.IsNullOrWhiteSpace(Shift))
            {
                query = query.Where(t => t.ShiftPattern == Shift);
            }

            Technicians = await query.OrderBy(t => t.Name).ToListAsync();

            TotalCount = Technicians.Count;
            ActiveCount = Technicians.Count(t => t.Active);
            AvailableCount = Technicians.Count(t => t.Active && t.ShiftPattern != null);
            var activeTechs = Technicians.Where(t => t.Active).ToList();
            AvgUtilization = activeTechs.Count > 0
                ? Math.Round(activeTechs.Average(t => (double)t.ProficiencyLevel) / 5.0 * 100, 0)
                : 0;

            AvailableSites = await _context.Sites
                .Where(s => visibleIds.Contains(s.CompanyId))
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
    }
}
