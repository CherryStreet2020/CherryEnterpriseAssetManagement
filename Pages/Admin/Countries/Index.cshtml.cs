using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Masters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.Countries;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only admin lookup. AppDbContext used only for projection of the ISO 3166-1 Country master.")]
// Sprint 13.5 PR #4 — /Admin/Countries
// Read-only admin view of the ISO 3166-1 country master seeded in PRA-2.
// Edit forms deferred — system-wide masters rarely change.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public IReadOnlyList<CountryRow> Countries { get; private set; } = new List<CountryRow>();
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        var rows = await _db.Countries
            .AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Alpha2)
            .Select(c => new CountryRow
            {
                Id = c.Id,
                Alpha2 = c.Alpha2,
                Alpha3 = c.Alpha3,
                Numeric = c.Numeric,
                Name = c.Name,
                CallingCode = c.CallingCode,
                Currency = c.DefaultCurrencyCode,
                SubdivisionCount = _db.Subdivisions.Count(s => s.CountryId == c.Id),
                IsActive = c.IsActive,
            })
            .ToListAsync();
        Countries = rows;
        TotalCount = rows.Count;
    }

    public sealed class CountryRow
    {
        public int Id { get; set; }
        public string Alpha2 { get; set; } = string.Empty;
        public string Alpha3 { get; set; } = string.Empty;
        public string Numeric { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CallingCode { get; set; }
        public string? Currency { get; set; }
        public int SubdivisionCount { get; set; }
        public bool IsActive { get; set; }
    }
}
