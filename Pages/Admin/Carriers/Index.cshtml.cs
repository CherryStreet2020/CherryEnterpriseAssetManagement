using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.Carriers;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only admin lookup. AppDbContext used only for projection of the Carrier master.")]
// Sprint 13.5 PR #4 — /Admin/Carriers
// Read-only admin view of the Carrier master seeded in PRA-1.
// 12 system carriers (NULL CompanyId) live by default; tenants can fork
// by creating their own rows with CompanyId set (deferred to a future PR).
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public IReadOnlyList<Carrier> Carriers { get; private set; } = new List<Carrier>();
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        Carriers = await _db.Set<Carrier>()
            .AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .ToListAsync();
        TotalCount = Carriers.Count;
    }
}
