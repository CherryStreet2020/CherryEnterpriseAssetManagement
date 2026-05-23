using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Production.Operations;

// Sprint 13.5 PR #5c — /Production/Operations
//
// Read-only ProductionOperations grid. The universal execution-time view —
// every op across every active production order, time-bucketed, ranked by
// urgency. This is the supervisor's "what's happening on the floor right now"
// answer surface, complementary to the Production Control Center.
//
// Lives under /Production/ (not /Admin/) because operators + supervisors
// reach it daily; it's not master-data admin.
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only execution-time grid. AppDbContext used only for select projections.")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public IReadOnlyList<OperationRow> Operations { get; private set; } = new List<OperationRow>();
    public int TotalCount { get; private set; }
    public int RunningCount { get; private set; }
    public int PausedCount { get; private set; }

    public async Task OnGetAsync()
    {
        var rows = await _db.ProductionOperations
            .AsNoTracking()
            .Where(o => o.Status != ProductionOperationStatus.Completed
                     && o.Status != ProductionOperationStatus.Skipped
                     && o.Status != ProductionOperationStatus.Scrapped)
            .OrderBy(o => o.Status == ProductionOperationStatus.Running ? 0
                       : o.Status == ProductionOperationStatus.Paused  ? 1
                       : o.Status == ProductionOperationStatus.InSetup ? 2
                       : o.Status == ProductionOperationStatus.Released ? 3
                       : 4)
            .ThenBy(o => o.PlannedStart)
            .Select(o => new OperationRow
            {
                Id = o.Id,
                ProductionOrderId = o.ProductionOrderId,
                SequenceNumber = o.SequenceNumber,
                Description = o.Description,
                WorkCenterCode = _db.WorkCenters.Where(w => w.Id == o.WorkCenterId).Select(w => w.Code).FirstOrDefault() ?? "—",
                OperationType = o.OperationType,
                Status = o.Status,
                PlannedQty = o.PlannedQty,
                CompletedQty = o.CompletedQty,
                ScrappedQty = o.ScrappedQty,
                PlannedStart = o.PlannedStart,
                ActualStart = o.ActualStart,
            })
            .Take(200)
            .ToListAsync();
        Operations = rows;
        TotalCount = rows.Count;
        RunningCount = rows.Count(r => r.Status == ProductionOperationStatus.Running);
        PausedCount = rows.Count(r => r.Status == ProductionOperationStatus.Paused);
    }

    public sealed class OperationRow
    {
        public int Id { get; set; }
        public int ProductionOrderId { get; set; }
        public int SequenceNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string WorkCenterCode { get; set; } = string.Empty;
        public ProductionOperationType OperationType { get; set; }
        public ProductionOperationStatus Status { get; set; }
        public decimal PlannedQty { get; set; }
        public decimal CompletedQty { get; set; }
        public decimal ScrappedQty { get; set; }
        public System.DateTime? PlannedStart { get; set; }
        public System.DateTime? ActualStart { get; set; }

        public string StatusTone() => Status switch
        {
            ProductionOperationStatus.Running => "success",
            ProductionOperationStatus.Paused => "warning",
            ProductionOperationStatus.InSetup => "info",
            ProductionOperationStatus.Released => "info",
            ProductionOperationStatus.Scheduled => "neutral",
            _ => "neutral",
        };

        public decimal ProgressPct => PlannedQty > 0 ? (CompletedQty / PlannedQty) * 100m : 0m;
    }
}
