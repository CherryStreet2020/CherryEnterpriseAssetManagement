using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Reports
{
    public class DepreciationPreviewModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly DepreciationService _depr;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public DepreciationPreviewModel(AppDbContext db, DepreciationService depr,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _depr = depr;
            _tenantContext = tenantContext;
        }

        // Header inputs
        public DateTime AsOf { get; private set; }
        public DateTime AsOfMonthEnd { get; private set; }

        // Rows for the grid
        public List<Line> Lines { get; private set; } = new List<Line>();

        // Totals shown in the summary cards
        public decimal TotalAccumulated
        {
            get
            {
                decimal sum = 0m;
                foreach (var x in Lines) sum += x.Accumulated;
                return sum;
            }
        }

        public decimal TotalEndNbv
        {
            get
            {
                decimal sum = 0m;
                foreach (var x in Lines) sum += x.NBV;
                return sum;
            }
        }

        public class Line
        {
            public string AssetNumber { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public DateTime InService { get; set; }
            public decimal Cost { get; set; }
            public decimal Salvage { get; set; }
            public int LifeMonths { get; set; }
            public decimal Monthly { get; set; }
            public decimal Accumulated { get; set; }
            public decimal NBV { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string? asOf)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });


            DateTime parsed;
            if (!DateTime.TryParse(asOf, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
                parsed = DateTime.Today;

            AsOf = parsed.Date;
            AsOfMonthEnd = new DateTime(AsOf.Year, AsOf.Month, DateTime.DaysInMonth(AsOf.Year, AsOf.Month));

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var assets = await _db.Assets
                .AsNoTracking()
                .Where(a => a.Active)
                .Where(a => visibleIds.Contains(a.CompanyId ?? 0))
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();

            var lines = new List<Line>(assets.Count);

            foreach (var a in assets)
            {
                // Build schedule as of month-end and take the last row if available
                var schedule = _depr.BuildSchedule(a, AsOfMonthEnd);
                DepreciationRow? row = null;
                if (schedule != null && schedule.Count > 0)
                    row = schedule[schedule.Count - 1];

                var line = new Line
                {
                    AssetNumber = GetString(a, new[] { "AssetNumber", "Number", "AssetId", "Id" }) ?? string.Empty,
                    Description = GetString(a, new[] { "Description", "Name", "Title" }) ?? string.Empty,
                    InService = GetDate(a, new[] { "InServiceDate", "InService", "PlacedInService" }),
                    Cost = GetDecimal(a, new[] { "AcquisitionCost", "Cost", "OriginalCost" }),
                    Salvage = GetDecimal(a, new[] { "SalvageValue", "Salvage", "ResidualValue" }),
                    LifeMonths = GetInt(a, new[] { "UsefulLifeMonths", "LifeMonths", "LifeInMonths" }),

                    // Values from the schedule row (property names can vary)
                    Monthly = GetDecimal(row, new[] { "Depreciation", "DepreciationAmount", "Monthly", "Amount" }),
                    Accumulated = GetDecimal(row, new[] { "Accumulated", "AccumulatedDepreciation", "TotalAccumulated" }),
                    NBV = GetDecimal(row, new[] { "EndNBV", "EndingBookValue", "NBV", "NetBookValue", "BookValue" })
                };

                lines.Add(line);
            }

            Lines = lines;
        
            return Page();
        }

        // CSV export mirrors the grid columns and keeps your UI behavior
        public async Task<FileContentResult> OnGetExportAsync(string? asOf)
        {
            await OnGetAsync(asOf);

            var sb = new StringBuilder();
            sb.AppendLine("Asset #,Description,In Service,Cost,Salvage,Life (mo),Monthly,Accumulated,NBV");
            foreach (var r in Lines)
            {
                sb.AppendLine(string.Join(",",
                    Csv(r.AssetNumber),
                    Csv(r.Description),
                    r.InService.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
                    r.Cost.ToString("F2", CultureInfo.InvariantCulture),
                    r.Salvage.ToString("F2", CultureInfo.InvariantCulture),
                    r.LifeMonths.ToString(CultureInfo.InvariantCulture),
                    r.Monthly.ToString("F2", CultureInfo.InvariantCulture),
                    r.Accumulated.ToString("F2", CultureInfo.InvariantCulture),
                    r.NBV.ToString("F2", CultureInfo.InvariantCulture)
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "depreciation-preview-" + AsOfMonthEnd.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv");
        }

        private static string Csv(string? s)
        {
            if (s == null) s = string.Empty;
            if (s.IndexOf('"') >= 0 || s.IndexOf(',') >= 0 || s.IndexOf('\n') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        // -------- Helpers exposed to the .cshtml (keeps UI unchanged) --------
        public string Money(decimal value)
        {
            return value.ToString("C0", CultureInfo.CurrentCulture);
        }

        public string ShortDate(DateTime dt)
        {
            return dt.ToString("MM/dd/yy", CultureInfo.InvariantCulture);
        }

        // -------- Reflection helpers (robust to differing model property names) --------
        private static string? GetString(object? obj, string[] names)
        {
            if (obj == null) return null;

            foreach (var n in names)
            {
                var p = obj.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null && p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(obj) as string;
                    if (v != null) return v;
                }
            }
            return null;
        }

        private static DateTime GetDate(object? obj, string[] names)
        {
            if (obj == null) return default(DateTime);

            foreach (var n in names)
            {
                var p = obj.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                {
                    var val = p.GetValue(obj);
                    if (val is DateTime dt) return dt;
                    var ndt = val as DateTime?;
                    if (ndt.HasValue) return ndt.Value;
                }
            }
            return default(DateTime);
        }

        private static int GetInt(object? obj, string[] names)
        {
            if (obj == null) return 0;

            foreach (var n in names)
            {
                var p = obj.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                {
                    var val = p.GetValue(obj);
                    if (val is int i) return i;
                    var ni = val as int?;
                    if (ni.HasValue) return ni.Value;
                    if (val is long l) return (int)l;
                    var s = val as string;
                    if (s != null)
                    {
                        int parsed;
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                            return parsed;
                    }
                }
            }
            return 0;
        }

        private static decimal GetDecimal(object? obj, string[] names)
        {
            if (obj == null) return 0m;

            foreach (var n in names)
            {
                var p = obj.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                {
                    var val = p.GetValue(obj);

                    if (val is decimal d) return d;
                    var nd = val as decimal?;
                    if (nd.HasValue) return nd.Value;
                    if (val is double dbl) return (decimal)dbl;
                    if (val is float flt) return (decimal)flt;
                    if (val is int i) return i;
                    if (val is long l) return l;
                    var s = val as string;
                    if (s != null)
                    {
                        decimal parsed;
                        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                            return parsed;
                    }
                }
            }
            return 0m;
        }
    }
}