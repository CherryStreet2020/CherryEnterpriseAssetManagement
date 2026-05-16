using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Reports
{
    /// <summary>
    /// PR #109: Failure mode Pareto analytics.
    ///
    /// Pareto analysis answers the question "which failure modes drive the most
    /// of our maintenance pain?" — visualized as a descending-frequency bar
    /// chart with a cumulative-% overlay line. The classic 80/20 lens lets a
    /// reliability engineer focus on the few failure codes that produce most
    /// of the work / cost.
    ///
    /// Two views, same data, different sort:
    ///   - <b>By Frequency</b>: sorted by WO count desc — surfaces the codes
    ///     causing the most work orders.
    ///   - <b>By Cost</b>: sorted by total spend desc — surfaces the codes
    ///     driving the most $$ even if their count is lower (rare-but-expensive
    ///     vs frequent-but-cheap).
    ///
    /// Data sources:
    ///   - <see cref="MaintenanceEvent"/> filtered to Type=Corrective, FailureCodeId IS NOT NULL,
    ///     CompletedDate within the [StartDate, EndDate] window.
    ///   - Cost = Σ JournalLine.Debit across WO-LBR + WO-ISS-OP for each WO,
    ///     minus WO-RTN / WO-RTN-OP returns. Single source of truth with the
    ///     PR #93 spend report and PR #108 dashboard tile.
    ///
    /// Drill-through: each row links to /Maintenance?failureCode={code} so the
    /// operator can see the underlying WO list.
    /// CSV export: ?format=csv (frequency view).
    /// </summary>
    [Authorize]
    public class ReliabilityModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public ReliabilityModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public sealed record ParetoRow(
            int FailureCodeId,
            string Code,
            string Name,
            int WoCount,
            decimal TotalCost,
            decimal CumulativePctByCount,
            decimal CumulativePctByCost);

        public List<ParetoRow> ByFrequency { get; set; } = new();
        public List<ParetoRow> ByCost { get; set; } = new();

        public int TotalWos { get; set; }
        public decimal TotalCost { get; set; }
        public int FailureCodeCount { get; set; }

        [BindProperty(SupportsGet = true)] public DateTime? StartDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? EndDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? Format { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });

            // Default to last 90 days — the same window the Sprint 1 fixture
            // spans, so the demo immediately shows a populated Pareto.
            var today = DateTime.UtcNow.Date;
            StartDate ??= today.AddDays(-90);
            EndDate ??= today;
            var endInclusive = EndDate.Value.Date.AddDays(1).AddTicks(-1);

            var visibleIds = _tenantContext.VisibleCompanyIds;

            // 1. Pull corrective WOs with FailureCodeId set in the window.
            var wos = await (
                from m in _context.MaintenanceEvents
                join a in _context.Assets on m.AssetId equals a.Id
                join fc in _context.FailureCodes on m.FailureCodeId equals fc.Id
                where m.Type == MaintenanceType.Corrective
                  && m.FailureCodeId.HasValue
                  && m.CompletedDate.HasValue
                  && m.CompletedDate >= StartDate.Value
                  && m.CompletedDate <= endInclusive
                  && a.CompanyId.HasValue && visibleIds.Contains(a.CompanyId.Value)
                select new
                {
                    WoId = m.Id,
                    FcId = fc.Id,
                    fc.Code,
                    fc.Name,
                    HeaderLabor = m.LaborCost ?? 0m,
                    HeaderMaterials = m.MaterialsCost ?? 0m
                }).ToListAsync();

            TotalWos = wos.Count;
            if (TotalWos == 0)
            {
                // Empty-state — the view will render an explainer + link to
                // the Sprint 1 fixture seeder if no data exists yet.
                return Page();
            }

            // 2. Cost lookup per WO from the JE table (same as PR #108 dashboard).
            //    We tolerate WOs that have header rollups but no JEs (legacy)
            //    by falling back to header LaborCost + MaterialsCost when no
            //    JE rows match. Pre-PR data wouldn't have op-part JEs anyway.
            var woIds = wos.Select(w => w.WoId).ToHashSet();
            var jeByWo = await (
                from j in _context.JournalEntries
                join l in _context.JournalLines on j.Id equals l.JournalEntryId
                where l.Debit > 0m
                  && (j.Source == "WO-LBR" || j.Source == "WO-ISS" || j.Source == "WO-ISS-OP")
                  && j.Reference != null
                select new { j.Reference, j.Source, l.Debit }).ToListAsync();

            var costByWo = new Dictionary<int, decimal>();
            foreach (var row in jeByWo)
            {
                // Parse the WO id from the reference prefix. References look like:
                //   WO-LBR-{woId}-...
                //   WO-ISS-{woId}-...
                //   WO-ISS-OP-{woId}-op{opId}-p{partId}-...
                if (row.Reference is null) continue;
                var parts = row.Reference.Split('-');
                int idx = row.Source == "WO-ISS-OP" ? 3 : 2;
                if (parts.Length <= idx) continue;
                if (!int.TryParse(parts[idx], out var woId)) continue;
                if (!woIds.Contains(woId)) continue;
                costByWo.TryGetValue(woId, out var existing);
                costByWo[woId] = existing + row.Debit;
            }

            // Subtract returns (WO-RTN / WO-RTN-OP) — same parsing as above.
            var returnsByWo = await (
                from j in _context.JournalEntries
                join l in _context.JournalLines on j.Id equals l.JournalEntryId
                where l.Debit > 0m
                  && (j.Source == "WO-RTN" || j.Source == "WO-RTN-OP")
                  && j.Reference != null
                select new { j.Reference, j.Source, l.Debit }).ToListAsync();
            foreach (var row in returnsByWo)
            {
                if (row.Reference is null) continue;
                var parts = row.Reference.Split('-');
                int idx = row.Source == "WO-RTN-OP" ? 3 : 2;
                if (parts.Length <= idx) continue;
                if (!int.TryParse(parts[idx], out var woId)) continue;
                if (!woIds.Contains(woId)) continue;
                costByWo.TryGetValue(woId, out var existing);
                costByWo[woId] = existing - row.Debit;
            }

            // 3. Aggregate by FailureCode.
            var byFc = new Dictionary<int, (string Code, string Name, int Count, decimal Cost)>();
            foreach (var wo in wos)
            {
                if (!byFc.TryGetValue(wo.FcId, out var agg))
                    agg = (wo.Code, wo.Name, 0, 0m);
                agg.Count++;
                // Prefer JE-sourced cost; fall back to header rollup for any
                // WO whose JE rows haven't landed yet.
                var jeCost = costByWo.GetValueOrDefault(wo.WoId, 0m);
                agg.Cost += jeCost > 0m ? jeCost : (wo.HeaderLabor + wo.HeaderMaterials);
                byFc[wo.FcId] = agg;
            }

            TotalCost = byFc.Values.Sum(v => v.Cost);
            FailureCodeCount = byFc.Count;

            // 4. By Frequency — sort desc by count, compute cumulative % of total WOs.
            var freqSorted = byFc.OrderByDescending(kv => kv.Value.Count).ToList();
            decimal cumCount = 0m;
            foreach (var kv in freqSorted)
            {
                cumCount += kv.Value.Count;
                var pctByCount = TotalWos == 0 ? 0 : Math.Round(100m * cumCount / TotalWos, 1);
                ByFrequency.Add(new ParetoRow(
                    kv.Key, kv.Value.Code, kv.Value.Name,
                    kv.Value.Count, kv.Value.Cost,
                    pctByCount,
                    0m));
            }

            // 5. By Cost — sort desc by cost, compute cumulative % of total cost.
            var costSorted = byFc.OrderByDescending(kv => kv.Value.Cost).ToList();
            decimal cumCost = 0m;
            foreach (var kv in costSorted)
            {
                cumCost += kv.Value.Cost;
                var pctByCost = TotalCost == 0m ? 0m : Math.Round(100m * cumCost / TotalCost, 1);
                ByCost.Add(new ParetoRow(
                    kv.Key, kv.Value.Code, kv.Value.Name,
                    kv.Value.Count, kv.Value.Cost,
                    0m,
                    pctByCost));
            }

            // CSV export of the frequency view (the spec calls for one CSV;
            // by-cost users can re-sort the spreadsheet themselves).
            if (string.Equals(Format, "csv", StringComparison.OrdinalIgnoreCase))
                return ExportCsv();

            return Page();
        }

        private IActionResult ExportCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("FailureCode,FailureName,WOCount,TotalCost,CumulativePctByCount");
            foreach (var r in ByFrequency)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},\"{1}\",{2},{3:F2},{4:F1}",
                    r.Code, r.Name.Replace("\"", "\"\""), r.WoCount, r.TotalCost, r.CumulativePctByCount));
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = $"reliability-pareto-{StartDate:yyyyMMdd}-to-{EndDate:yyyyMMdd}.csv";
            return File(bytes, "text/csv", filename);
        }
    }
}
