using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Reports
{
    /// <summary>
    /// PR #93: Per-asset maintenance spend rollup. The "show me total cost per
    /// asset YTD" KPI Maximo / Infor EAM / SAP PM open their demos on.
    ///
    /// Data sources:
    /// - <see cref="WorkOrderPart"/> (materials issued/returned/used per WO)
    /// - <see cref="WorkOrderOperationLabor"/> (labor hours × rate per op)
    /// Both joined to <see cref="MaintenanceEvent"/> → <see cref="Asset"/> for
    /// the asset-level roll-up.
    ///
    /// JE reconciliation footer: queries <see cref="JournalEntry"/> filtered by
    /// Source ∈ ('WO-ISS','WO-RTN','WO-LBR') over the same window and shows the
    /// totals side-by-side so finance can tie the operational view to the GL.
    /// </summary>
    public class MaintenanceSpendModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public MaintenanceSpendModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public sealed record AssetSpendRow(
            int AssetId,
            string AssetNumber,
            string AssetDescription,
            int WorkOrderCount,
            decimal LaborCost,
            decimal MaterialsIssued,
            decimal MaterialsReturned,
            decimal MaterialsNet,
            decimal TotalSpend);

        public List<AssetSpendRow> Rows { get; set; } = new();

        // KPIs
        public decimal TotalLabor { get; set; }
        public decimal TotalMaterialsNet { get; set; }
        public decimal TotalSpend { get; set; }
        public int ActiveAssetCount { get; set; }
        public int WorkOrderCount { get; set; }

        // JE reconciliation
        public decimal JeLaborTotal { get; set; }
        public decimal JeMaterialsNetTotal { get; set; }
        public decimal JeTotalSpend { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });

            // Default to last 90 days if no explicit range.
            var today = DateTime.UtcNow.Date;
            StartDate ??= today.AddDays(-90);
            EndDate ??= today;
            // Inclusive end-of-day for the EndDate so today's activity isn't trimmed.
            var endInclusive = EndDate.Value.Date.AddDays(1).AddTicks(-1);

            var visibleIds = _tenantContext.VisibleCompanyIds;

            // ============ Labor cost per asset ============
            // Sum WorkOrderOperationLabor.Hours * HourlyRate, joining op → WO → Asset.
            var laborRows = await (
                from lab in _context.Set<WorkOrderOperationLabor>()
                join op in _context.WorkOrderOperations on lab.WorkOrderOperationId equals op.Id
                join wo in _context.MaintenanceEvents on op.MaintenanceEventId equals wo.Id
                join a in _context.Assets on wo.AssetId equals a.Id
                where a.CompanyId.HasValue && visibleIds.Contains(a.CompanyId.Value)
                where lab.WorkDate >= StartDate.Value && lab.WorkDate <= endInclusive
                select new {
                    AssetId = a.Id,
                    AssetNumber = a.AssetNumber,
                    AssetDescription = a.Description ?? "",
                    WoId = wo.Id,
                    LaborCost = lab.Hours * lab.HourlyRate
                }).ToListAsync();

            // ============ Materials issued / returned per asset ============
            var partRows = await (
                from p in _context.WorkOrderParts
                join wo in _context.MaintenanceEvents on p.MaintenanceEventId equals wo.Id
                join a in _context.Assets on wo.AssetId equals a.Id
                where a.CompanyId.HasValue && visibleIds.Contains(a.CompanyId.Value)
                where (p.IssuedDate ?? p.CreatedAt) >= StartDate.Value && (p.IssuedDate ?? p.CreatedAt) <= endInclusive
                select new {
                    AssetId = a.Id,
                    AssetNumber = a.AssetNumber,
                    AssetDescription = a.Description ?? "",
                    WoId = wo.Id,
                    Issued = p.QuantityIssued * p.UnitCost,
                    Returned = p.QuantityReturned * p.UnitCost
                }).ToListAsync();

            // ============ Roll up per asset ============
            var byAsset = new Dictionary<int, (string AssetNumber, string Description, HashSet<int> Wos, decimal Labor, decimal Issued, decimal Returned)>();

            foreach (var l in laborRows)
            {
                if (!byAsset.TryGetValue(l.AssetId, out var agg))
                    agg = (l.AssetNumber, l.AssetDescription, new HashSet<int>(), 0m, 0m, 0m);
                agg.Wos.Add(l.WoId);
                agg.Labor += l.LaborCost;
                byAsset[l.AssetId] = agg;
            }

            foreach (var p in partRows)
            {
                if (!byAsset.TryGetValue(p.AssetId, out var agg))
                    agg = (p.AssetNumber, p.AssetDescription, new HashSet<int>(), 0m, 0m, 0m);
                agg.Wos.Add(p.WoId);
                agg.Issued += p.Issued;
                agg.Returned += p.Returned;
                byAsset[p.AssetId] = agg;
            }

            Rows = byAsset
                .Select(kv =>
                {
                    var net = kv.Value.Issued - kv.Value.Returned;
                    var total = kv.Value.Labor + net;
                    return new AssetSpendRow(
                        kv.Key,
                        kv.Value.AssetNumber,
                        kv.Value.Description,
                        kv.Value.Wos.Count,
                        kv.Value.Labor,
                        kv.Value.Issued,
                        kv.Value.Returned,
                        net,
                        total);
                })
                .OrderByDescending(r => r.TotalSpend)
                .ToList();

            TotalLabor = Rows.Sum(r => r.LaborCost);
            TotalMaterialsNet = Rows.Sum(r => r.MaterialsNet);
            TotalSpend = Rows.Sum(r => r.TotalSpend);
            ActiveAssetCount = Rows.Count;
            WorkOrderCount = byAsset.Values.SelectMany(v => v.Wos).Distinct().Count();

            // ============ JE reconciliation ============
            // Pull the GL-side picture by Source for the same window, so the
            // operational rollup above can be reconciled to the ledger.
            // Posting date filter uses PostingDate (which is the WO action date).
            var jeWindow = await _context.JournalEntries
                .Where(j => j.PostingDate >= StartDate.Value && j.PostingDate <= endInclusive)
                .Where(j => j.Source == "WO-LBR" || j.Source == "WO-ISS" || j.Source == "WO-RTN")
                .Select(j => new {
                    Source = j.Source!,
                    Amount = j.Lines.Sum(l => l.Debit) // Σdebits == Σcredits by PR #84 guard
                })
                .ToListAsync();
            JeLaborTotal = jeWindow.Where(j => j.Source == "WO-LBR").Sum(j => j.Amount);
            var jeIssued = jeWindow.Where(j => j.Source == "WO-ISS").Sum(j => j.Amount);
            var jeReturned = jeWindow.Where(j => j.Source == "WO-RTN").Sum(j => j.Amount);
            JeMaterialsNetTotal = jeIssued - jeReturned;
            JeTotalSpend = JeLaborTotal + JeMaterialsNetTotal;

            return Page();
        }
    }
}
