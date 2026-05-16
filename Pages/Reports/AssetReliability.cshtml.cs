using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Reliability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Reports
{
    /// <summary>
    /// PR #110: Per-asset reliability report. The "which assets are killing us"
    /// view that complements the Pareto failure-mode breakdown (PR #109/#112).
    ///
    /// Columns per asset: corrective WO count, MTBF (hrs), Availability %,
    /// Spend in window. Sortable by any column via ?sort=col&desc=true.
    /// CSV export at ?format=csv.
    /// </summary>
    [Authorize]
    public class AssetReliabilityModel : PageModel
    {
        private readonly ReliabilityMetricsService _metrics;
        private readonly IModuleGuardService _moduleGuard;

        public AssetReliabilityModel(ReliabilityMetricsService metrics, IModuleGuardService moduleGuard)
        {
            _metrics = metrics;
            _moduleGuard = moduleGuard;
        }

        public List<ReliabilityMetricsService.AssetReliabilityRow> Rows { get; private set; } = new();
        public decimal WindowHours { get; private set; }
        public decimal AvgMtbf { get; private set; }
        public decimal AvgAvailability { get; private set; }

        [BindProperty(SupportsGet = true)] public DateTime? StartDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? EndDate { get; set; }
        [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "spend";
        [BindProperty(SupportsGet = true)] public bool Desc { get; set; } = true;
        [BindProperty(SupportsGet = true)] public string? Format { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });

            var today = DateTime.UtcNow.Date;
            StartDate ??= today.AddDays(-90);
            EndDate ??= today;
            WindowHours = (decimal)(EndDate.Value.Date.AddDays(1).AddTicks(-1) - StartDate.Value.Date).TotalHours;

            var unsorted = await _metrics.ComputeAsync(StartDate.Value, EndDate.Value);
            Rows = ApplySort(unsorted, Sort, Desc);

            // KPI averages — informative summary numbers at the top.
            var withMtbf = Rows.Where(r => r.MtbfHours.HasValue).Select(r => r.MtbfHours!.Value).ToList();
            AvgMtbf = withMtbf.Count == 0 ? 0m : Math.Round(withMtbf.Average(), 1);
            AvgAvailability = Rows.Count == 0 ? 0m : Math.Round(Rows.Average(r => r.AvailabilityPercent), 2);

            if (string.Equals(Format, "csv", StringComparison.OrdinalIgnoreCase))
                return ExportCsv();

            return Page();
        }

        private List<ReliabilityMetricsService.AssetReliabilityRow> ApplySort(
            List<ReliabilityMetricsService.AssetReliabilityRow> rows, string sort, bool desc)
        {
            IOrderedEnumerable<ReliabilityMetricsService.AssetReliabilityRow> ordered = sort switch
            {
                "asset" => desc ? rows.OrderByDescending(r => r.AssetNumber) : rows.OrderBy(r => r.AssetNumber),
                "wos" => desc ? rows.OrderByDescending(r => r.CorrectiveWoCount) : rows.OrderBy(r => r.CorrectiveWoCount),
                "mtbf" => desc
                    ? rows.OrderByDescending(r => r.MtbfHours ?? 0m)
                    : rows.OrderBy(r => r.MtbfHours ?? decimal.MaxValue), // null-MTBF assets float to the bottom on ascending
                "avail" => desc ? rows.OrderByDescending(r => r.AvailabilityPercent) : rows.OrderBy(r => r.AvailabilityPercent),
                _ => desc ? rows.OrderByDescending(r => r.SpendInWindow) : rows.OrderBy(r => r.SpendInWindow)
            };
            return ordered.ToList();
        }

        private IActionResult ExportCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("AssetNumber,Description,CorrectiveWOCount,MTBFHours,AvailabilityPercent,SpendInWindow");
            foreach (var r in Rows)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},\"{1}\",{2},{3},{4:F2},{5:F2}",
                    r.AssetNumber,
                    r.Description.Replace("\"", "\"\""),
                    r.CorrectiveWoCount,
                    r.MtbfHours.HasValue ? r.MtbfHours.Value.ToString("F1", CultureInfo.InvariantCulture) : "",
                    r.AvailabilityPercent,
                    r.SpendInWindow));
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = $"asset-reliability-{StartDate:yyyyMMdd}-to-{EndDate:yyyyMMdd}.csv";
            return File(bytes, "text/csv", filename);
        }
    }
}
