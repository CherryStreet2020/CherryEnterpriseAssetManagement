using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CustomerProjects;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only list page — no mutations. AppDbContext used only for projection/filtering of CustomerProject rows.")]
// Sprint 13.5 PR #4 — /CustomerProjects cockpit landing page.
// List view + status filter chips. Cockpit polish (queue-left +
// preview-right + KPI band per ADR-018) lands in PR #5a.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public IndexModel(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public IReadOnlyList<ProjectRow> Projects { get; private set; } = new List<ProjectRow>();
    public CustomerProjectStatus? FilterStatus { get; private set; }
    public Dictionary<CustomerProjectStatus, int> CountByStatus { get; private set; } = new();
    public int TotalCount { get; private set; }

    public async Task OnGetAsync(string? status)
    {
        if (!string.IsNullOrWhiteSpace(status)
            && System.Enum.TryParse<CustomerProjectStatus>(status, ignoreCase: true, out var parsed))
        {
            FilterStatus = parsed;
        }

        var visible = _tenantContext.VisibleCompanyIds;

        var baseQuery = _db.CustomerProjects
            .AsNoTracking()
            .Include(p => p.PrimaryCustomer)
            .Include(p => p.Company)
            .Where(p => p.CompanyId == null || visible.Contains(p.CompanyId ?? 0));

        var counts = await baseQuery
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        TotalCount = counts.Sum(c => c.Count);
        CountByStatus = counts.ToDictionary(c => c.Status, c => c.Count);

        var filtered = FilterStatus.HasValue
            ? baseQuery.Where(p => p.Status == FilterStatus.Value)
            : baseQuery;

        Projects = await filtered
            .OrderByDescending(p => p.CreatedAt)
            .Take(200)
            .Select(p => new ProjectRow
            {
                Id              = p.Id,
                Code            = p.Code,
                Name            = p.Name,
                Status          = p.Status,
                Mode            = p.Mode,
                CustomerName    = p.PrimaryCustomer != null ? p.PrimaryCustomer.Name : null,
                ContractValue   = p.ContractValue,
                Currency        = p.Currency,
                TargetStartDate = p.TargetStartDate,
                TargetEndDate   = p.TargetEndDate,
                RiskTone        = p.RiskTone,
                RiskScore       = p.RiskScore,
                CustomerPo      = p.CustomerPoNumber,
            })
            .ToListAsync();
    }

    public sealed class ProjectRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CustomerProjectStatus Status { get; set; }
        public CustomerProjectMode Mode { get; set; }
        public string? CustomerName { get; set; }
        public decimal? ContractValue { get; set; }
        public string Currency { get; set; } = string.Empty;
        public System.DateTime? TargetStartDate { get; set; }
        public System.DateTime? TargetEndDate { get; set; }
        public RiskTone? RiskTone { get; set; }
        public short? RiskScore { get; set; }
        public string? CustomerPo { get; set; }
    }
}
