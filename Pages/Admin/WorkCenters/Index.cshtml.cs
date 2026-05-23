using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.WorkCenters;

// Sprint 13.5 PR #5c — /Admin/WorkCenters
//
// Read-only admin grid of WorkCenters with LIVE MACHINE STATE roll-up from
// linked Assets (Asset.CurrentOEE / Asset.CurrentAvailability are populated
// by the existing IoT pipeline — migration AddMesIotOeeFields shipped them).
//
// What makes this BIC vs the big boys:
//   - Epicor / SAP / Plex admin grids show STATIC fields only (Code / Name /
//     Cost Rate / Calendar). They send you to a separate "Live" page to see
//     machine state.
//   - We unify master + live state IN THE SAME GRID. Each row shows asset
//     count + average OEE (computed from linked Assets) + status tone.
//
// Create / Edit / WorkCenter Control Center surface (cards w/ live state, drag
// to assign assets, voice editing) land in PR #5c.1.
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only admin grid with live OEE rollup projection. AppDbContext used only for select projections.")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public IReadOnlyList<WorkCenterRow> WorkCenters { get; private set; } = new List<WorkCenterRow>();
    public int TotalCount { get; private set; }
    public int ActiveCount { get; private set; }
    public int DownCount { get; private set; }

    public async Task OnGetAsync()
    {
        var rows = await _db.WorkCenters
            .AsNoTracking()
            .OrderBy(w => w.CompanyId).ThenBy(w => w.Code)
            .Select(w => new WorkCenterRow
            {
                Id = w.Id,
                CompanyId = w.CompanyId,
                LocationId = w.LocationId,
                LocationName = _db.Locations.Where(l => l.Id == w.LocationId).Select(l => l.Name).FirstOrDefault() ?? "—",
                Code = w.Code,
                Name = w.Name,
                Type = w.Type,
                Status = w.Status,
                CapacityModel = w.CapacityModel,
                EfficiencyPct = w.EfficiencyPct,
                StandardCostRatePerHour = w.StandardCostRatePerHour,
                CurrencyCode = w.CurrencyCode,
                LinkedAssetCount = _db.WorkCenterAssetLinks
                    .Count(l => l.WorkCenterId == w.Id && l.EffectiveTo == null),
                AvgOeePct = _db.WorkCenterAssetLinks
                    .Where(l => l.WorkCenterId == w.Id && l.EffectiveTo == null)
                    .Join(_db.Assets,
                        l => l.AssetId,
                        a => a.Id,
                        (l, a) => a.CurrentOEE)
                    .Where(o => o.HasValue)
                    .Average(o => (decimal?)o) ?? 0m,
            })
            .ToListAsync();
        WorkCenters = rows;
        TotalCount = rows.Count;
        ActiveCount = rows.Count(w => w.Status == WorkCenterStatus.Active);
        DownCount = rows.Count(w => w.Status == WorkCenterStatus.Maintenance);
    }

    public sealed class WorkCenterRow
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = "—";
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public WorkCenterType Type { get; set; }
        public WorkCenterStatus Status { get; set; }
        public WorkCenterCapacityModel CapacityModel { get; set; }
        public decimal EfficiencyPct { get; set; }
        public decimal StandardCostRatePerHour { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public int LinkedAssetCount { get; set; }
        public decimal AvgOeePct { get; set; }

        public string StatusTone() => Status switch
        {
            WorkCenterStatus.Active => "success",
            WorkCenterStatus.Inactive => "neutral",
            WorkCenterStatus.Maintenance => "warning",
            WorkCenterStatus.Retired => "neutral",
            _ => "neutral",
        };

        public string OeeTone() => AvgOeePct switch
        {
            >= 85m => "success",
            >= 65m => "warning",
            > 0m => "danger",
            _ => "neutral",
        };
    }
}
