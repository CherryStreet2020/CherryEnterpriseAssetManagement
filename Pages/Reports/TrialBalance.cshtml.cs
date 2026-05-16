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
    /// PR #113: Trial Balance report — the auditor's first stop.
    ///
    /// For each GL account that appears in any JournalLine within the window:
    ///   - Σ Debit (period activity)
    ///   - Σ Credit (period activity)
    ///   - Net = Σ Debit − Σ Credit
    ///
    /// Joined to the GlAccount master for human-readable Account Name +
    /// account type classification. Grand totals at the bottom prove the
    /// trial balance is balanced (Σ Debit == Σ Credit across all accounts);
    /// if it's not, that's a finance-level integrity bug worth shouting about.
    ///
    /// CSV export at ?format=csv.
    /// Drill-through: clicking any account row opens /Journals filtered by
    /// account number for the same window.
    /// </summary>
    [Authorize]
    public class TrialBalanceModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public TrialBalanceModel(AppDbContext db, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _db = db;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public sealed record TbRow(
            string AccountNumber,
            string AccountName,
            string AccountType,
            decimal Debits,
            decimal Credits,
            decimal Net);

        public List<TbRow> Rows { get; private set; } = new();
        public decimal TotalDebits { get; private set; }
        public decimal TotalCredits { get; private set; }
        public decimal NetDifference { get; private set; }
        public bool IsBalanced => Math.Abs(NetDifference) < 0.01m;

        [BindProperty(SupportsGet = true)] public DateTime? StartDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? EndDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? Format { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });

            var today = DateTime.UtcNow.Date;
            // Default to "current month to date" — auditor's most common slice
            StartDate ??= new DateTime(today.Year, today.Month, 1);
            EndDate ??= today;
            var endInclusive = EndDate.Value.Date.AddDays(1).AddTicks(-1);

            // Aggregate every JournalLine in the window by Account string.
            // We do not (yet) filter by company on the JE side — the JE
            // table is currently tenant-global; CompanyId scoping lives on
            // the source entities (PO, WO, etc.). Adding a company filter
            // here would require denormalizing CompanyId onto JournalEntry,
            // which is the right long-term move but out of scope for this PR.
            var lineAgg = await _db.JournalLines
                .Where(l => l.JournalEntry != null
                    && l.JournalEntry.PostingDate >= StartDate.Value
                    && l.JournalEntry.PostingDate <= endInclusive)
                .GroupBy(l => l.Account)
                .Select(g => new
                {
                    Account = g.Key,
                    Debits = g.Sum(l => l.Debit),
                    Credits = g.Sum(l => l.Credit)
                })
                .ToListAsync();

            // Join to GlAccount for human labels. The Account column on
            // JournalLine is a string FK (the GL account number, e.g. "1300"),
            // so we look it up by AccountNumber.
            var accountCodes = lineAgg.Select(x => x.Account).Distinct().ToList();
            var accounts = await _db.GlAccounts
                .Where(g => accountCodes.Contains(g.AccountNumber))
                .Select(g => new { g.AccountNumber, g.Name, g.AccountType })
                .ToListAsync();

            var accountLookup = accounts.ToDictionary(a => a.AccountNumber);
            Rows = lineAgg
                .Select(x =>
                {
                    accountLookup.TryGetValue(x.Account, out var ga);
                    var net = x.Debits - x.Credits;
                    return new TbRow(
                        AccountNumber: x.Account,
                        AccountName: ga?.Name ?? "(unmapped)",
                        AccountType: ga?.AccountType.ToString() ?? "Unknown",
                        Debits: x.Debits,
                        Credits: x.Credits,
                        Net: net);
                })
                .OrderBy(r => r.AccountNumber)
                .ToList();

            TotalDebits = Rows.Sum(r => r.Debits);
            TotalCredits = Rows.Sum(r => r.Credits);
            NetDifference = TotalDebits - TotalCredits;

            if (string.Equals(Format, "csv", StringComparison.OrdinalIgnoreCase))
                return ExportCsv();

            return Page();
        }

        private IActionResult ExportCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("AccountNumber,AccountName,AccountType,Debits,Credits,Net");
            foreach (var r in Rows)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},\"{1}\",{2},{3:F2},{4:F2},{5:F2}",
                    r.AccountNumber,
                    r.AccountName.Replace("\"", "\"\""),
                    r.AccountType,
                    r.Debits, r.Credits, r.Net));
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "TOTAL,,, {0:F2},{1:F2},{2:F2}",
                TotalDebits, TotalCredits, NetDifference));
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"trial-balance-{StartDate:yyyyMMdd}-to-{EndDate:yyyyMMdd}.csv");
        }
    }
}
