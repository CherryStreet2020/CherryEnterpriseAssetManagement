using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Journals
{
    [Authorize(Policy = "AccountantOrAdmin")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        public DetailsModel(AppDbContext db,
            IModuleGuardService moduleGuard, ITenantContext tenantContext) {
            _moduleGuard = moduleGuard; _db = db; _tenantContext = tenantContext; }

        public JournalEntry? Entry { get; private set; }
        public decimal TotalDebit  { get; private set; }
        public decimal TotalCredit { get; private set; }
        public bool IsBalanced => TotalDebit == TotalCredit;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            Entry = await _db.JournalEntries
                .Include(j => j.Lines)
                .Include(j => j.Book)
                .Where(j => j.Id == id && (j.Book == null || visibleIds.Contains(j.Book.CompanyId ?? 0)))
                .FirstOrDefaultAsync();

            if (Entry == null) return NotFound();

            var lines = Entry.Lines ?? new List<JournalLine>();
            TotalDebit  = lines.Sum(l => l.Debit);
            TotalCredit = lines.Sum(l => l.Credit);

            ViewData["ReturnUrl"] = ReturnUrl;
            ViewData["Breadcrumbs"] = new List<(string Label, string Href)>
            {
                ("Finance", "/Journals"),
                ("Journals", "/Journals"),
                ("Detail", "")
            };
            ViewData["ShowBackLink"] = true;
            ViewData["BackLinkFallback"] = "/Journals";
            ViewData["BackLinkLabel"] = "Back to results";

            return Page();
        }

        // GET /Journals/Details/{id}?handler=Export
        public async Task<IActionResult> OnGetExportAsync(int id)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;

            var entry = await _db.JournalEntries
                .Include(j => j.Lines)
                .Include(j => j.Book)
                .Where(j => j.Id == id && (j.Book == null || visibleIds.Contains(j.Book.CompanyId ?? 0)))
                .FirstOrDefaultAsync();

            if (entry == null) return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("Batch,PostingDate,Reference,Source,Description");
            sb.AppendLine(string.Join(",",
                Csv(entry.Batch),
                entry.PostingDate.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
                Csv(entry.Reference),
                Csv(entry.Source),
                Csv(entry.Description)));

            sb.AppendLine();
            sb.AppendLine("Line,Account,Description,Debit,Credit");

            int lineNo = 1;
            foreach (var l in (entry.Lines ?? new List<JournalLine>()))
            {
                sb.AppendLine(string.Join(",",
                    lineNo++.ToString(CultureInfo.InvariantCulture),
                    Csv(l.Account),
                    Csv(l.Description),
                    l.Debit.ToString("0.00", CultureInfo.InvariantCulture),
                    l.Credit.ToString("0.00", CultureInfo.InvariantCulture)));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var file  = $"journal_{entry.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", file);

            static string Csv(string? s) =>
                string.IsNullOrEmpty(s) ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
        }
    }
}