using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Models.Quality;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CustomerProjects;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only deep-dive view. AppDbContext used only for projection of the project + its child collections. Mutations land in PR #5a cockpit through ICustomerProjectService.")]
// Sprint 13.5 PR #4 — /CustomerProjects/Details/{id}
// Read-only deep-dive view: header + 5 child summaries (Jobs / Phases /
// Members / Amendments / Vertical-tagged regulatory). Full cockpit
// pattern (queue-left + preview-right + KPI band) lands in PR #5a.
public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;

    public DetailsModel(AppDbContext db)
    {
        _db = db;
    }

    public CustomerProject? Project { get; private set; }
    public IReadOnlyList<ProductionOrder> Jobs { get; private set; } = new List<ProductionOrder>();
    public IReadOnlyList<ProjectPhase> Phases { get; private set; } = new List<ProjectPhase>();
    public IReadOnlyList<ProjectMember> Members { get; private set; } = new List<ProjectMember>();
    public IReadOnlyList<ProjectAmendment> Amendments { get; private set; } = new List<ProjectAmendment>();

    // Sprint 13.5 PR #338 — FAI section. Read-only projection, same control-plane
    // exemption as the other child collections. Mutations route through IFaiService
    // on /Quality/Fai/*.
    public IReadOnlyList<FaiReport> FaiReports { get; private set; } = new List<FaiReport>();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Project = await _db.CustomerProjects
            .AsNoTracking()
            .Include(p => p.PrimaryCustomer)
            .Include(p => p.Program)
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Project == null)
        {
            return NotFound();
        }

        Jobs = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Item)
            .Include(o => o.Location)
            .Where(o => o.CustomerProjectId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .ToListAsync();

        Phases = await _db.ProjectPhases
            .AsNoTracking()
            .Where(ph => ph.CustomerProjectId == id)
            .OrderBy(ph => ph.SortOrder)
            .ThenBy(ph => ph.Code)
            .ToListAsync();

        Members = await _db.ProjectMembers
            .AsNoTracking()
            .Include(m => m.Customer)
            .Where(m => m.CustomerProjectId == id)
            .OrderBy(m => m.Role)
            .ToListAsync();

        Amendments = await _db.ProjectAmendments
            .AsNoTracking()
            .Where(a => a.CustomerProjectId == id)
            .OrderByDescending(a => a.AmendmentNumber)
            .Take(50)
            .ToListAsync();

        // PR #338 — pull recent FAIs scoped to this project.
        FaiReports = await _db.FaiReports
            .AsNoTracking()
            .Where(f => f.CustomerProjectId == id)
            .OrderByDescending(f => f.CreatedAt)
            .Take(50)
            .ToListAsync();

        return Page();
    }
}
