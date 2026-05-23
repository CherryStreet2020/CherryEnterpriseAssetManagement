using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.Routings;

// Sprint 13.5 PR #5c — /Admin/Routings
//
// Read-only Routings admin grid. Drag-to-reorder Kanban-card builder + voice
// editing + smart-default-from-prior composer land in PR #5c.1.
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only admin grid. AppDbContext used only for select projections.")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public IReadOnlyList<RoutingRow> Routings { get; private set; } = new List<RoutingRow>();
    public int TotalCount { get; private set; }
    public int ReleasedCount { get; private set; }
    public int DraftCount { get; private set; }

    public async Task OnGetAsync()
    {
        var rows = await _db.Routings
            .AsNoTracking()
            .OrderBy(r => r.CompanyId).ThenBy(r => r.Code).ThenBy(r => r.RevisionNumber)
            .Select(r => new RoutingRow
            {
                Id = r.Id,
                CompanyId = r.CompanyId,
                LocationId = r.LocationId,
                IsSiteWideTemplate = r.IsSiteWideTemplate,
                LocationName = r.LocationId.HasValue
                    ? (_db.Locations.Where(l => l.Id == r.LocationId.Value).Select(l => l.Name).FirstOrDefault() ?? "—")
                    : "Company-wide template",
                Code = r.Code,
                RevisionNumber = r.RevisionNumber,
                Name = r.Name,
                ItemId = r.ItemId,
                Type = r.Type,
                Status = r.Status,
                IsDefault = r.IsDefault,
                LotBaseSize = r.LotBaseSize,
                OperationCount = _db.RoutingOperations.Count(o => o.RoutingId == r.Id),
                EffectiveFrom = r.EffectiveFrom,
            })
            .ToListAsync();
        Routings = rows;
        TotalCount = rows.Count;
        ReleasedCount = rows.Count(r => r.Status == RoutingStatus.Released);
        DraftCount = rows.Count(r => r.Status == RoutingStatus.Draft);
    }

    public sealed class RoutingRow
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int? LocationId { get; set; }
        public bool IsSiteWideTemplate { get; set; }
        public string LocationName { get; set; } = "—";
        public string Code { get; set; } = string.Empty;
        public string RevisionNumber { get; set; } = "A";
        public string Name { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public RoutingType Type { get; set; }
        public RoutingStatus Status { get; set; }
        public bool IsDefault { get; set; }
        public decimal LotBaseSize { get; set; }
        public int OperationCount { get; set; }
        public System.DateTime? EffectiveFrom { get; set; }

        public string StatusTone() => Status switch
        {
            RoutingStatus.Draft => "neutral",
            RoutingStatus.UnderReview => "warning",
            RoutingStatus.Approved => "info",
            RoutingStatus.Released => "success",
            RoutingStatus.Obsolete => "neutral",
            _ => "neutral",
        };
    }
}
