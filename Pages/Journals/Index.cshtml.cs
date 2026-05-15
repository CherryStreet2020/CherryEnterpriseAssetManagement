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

namespace Abs.FixedAssets.Pages.Journals
{
    [Authorize(Policy = "AccountantOrAdmin")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        public IndexModel(AppDbContext db, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        { _db = db; _moduleGuard = moduleGuard; _tenantContext = tenantContext; }

        [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? To   { get; set; }
        [BindProperty(SupportsGet = true)] public string? Source { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }

        public List<JournalEntry> Items { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Journals" });

            Items = await BuildQuery().ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnGetExportAsync()
        {
            var rows = await BuildQuery().ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Batch,PostingDate,Reference,Source,Description,Lines,TotalDebit,TotalCredit");

            string Csv(string? s) => string.IsNullOrEmpty(s) ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

            foreach (var j in rows)
            {
                var lines = j.Lines ?? new List<JournalLine>();
                decimal totDr = lines.Sum(l => l.Debit);
                decimal totCr = lines.Sum(l => l.Credit);

                sb.AppendLine(string.Join(",",
                    Csv(j.Batch),
                    j.PostingDate.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
                    Csv(j.Reference),
                    Csv(j.Source),
                    Csv(j.Description),
                    lines.Count.ToString(CultureInfo.InvariantCulture),
                    totDr.ToString("0.00", CultureInfo.InvariantCulture),
                    totCr.ToString("0.00", CultureInfo.InvariantCulture)
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"journals_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        private IQueryable<JournalEntry> BuildQuery()
        {
            var q = _db.JournalEntries
                .Include(j => j.Lines)
                .OrderByDescending(j => j.PostingDate)
                .ThenByDescending(j => j.Id)
                .AsQueryable();

            if (From.HasValue)
            {
                var fromDate = From.Value.Date;
                q = q.Where(j => j.PostingDate >= fromDate);
            }

            if (To.HasValue)
            {
                var toExclusive = To.Value.Date.AddDays(1);
                q = q.Where(j => j.PostingDate < toExclusive);
            }

            if (!string.IsNullOrWhiteSpace(Source))
            {
                var src = Source.Trim();
                q = q.Where(j => j.Source != null && j.Source.Contains(src));
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(j =>
                    (j.Batch != null && j.Batch.Contains(s)) ||
                    (j.Reference != null && j.Reference.Contains(s)) ||
                    (j.Description != null && j.Description.Contains(s)));
            }

            return q;
        }
    }
}
