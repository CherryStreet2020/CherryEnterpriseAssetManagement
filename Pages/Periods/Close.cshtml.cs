using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Periods
{
    // MP #112 — Period Close Orchestration: the live close screen.
    //
    // GET renders: period header, live pre-flight checklist (red/green dots),
    //   "Run sequenced close" button (disabled if any FAIL and override box unchecked),
    //   for already-closed periods, the captured close packet from PreflightSnapshotJson.
    //
    // POST runs the close. On success, redirects back to this page to render the
    //   immutable packet for that period (clean PRG pattern).
    //
    // Reopen is a separate POST handler with reason required.
    [Authorize(Roles = "Admin,Manager,Accountant,Finance")]
    public class CloseModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IPeriodCloseOrchestrationService _orchestration;
        private readonly ITenantContext _tenant;

        public CloseModel(
            AppDbContext db,
            IPeriodCloseOrchestrationService orchestration,
            ITenantContext tenant)
        {
            _db = db;
            _orchestration = orchestration;
            _tenant = tenant;
        }

        [BindProperty(SupportsGet = true)] public int FiscalPeriodId { get; set; }
        [BindProperty(SupportsGet = true)] public int CompanyId { get; set; }

        public FiscalPeriod? Period { get; set; }
        public IReadOnlyList<PreflightCheck> Preflight { get; set; } = new List<PreflightCheck>();
        public PeriodClosePacket? CapturedPacket { get; set; }
        public string? Notice { get; set; }
        public string? Error { get; set; }
        public bool HasBlockingFailures => Preflight.Any(c => c.Status == CheckStatus.Fail);
        public bool HasWarnings => Preflight.Any(c => c.Status == CheckStatus.Warn);

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            if (Period == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostCloseAsync(bool overrideFailures, string? overrideReason)
        {
            try
            {
                var result = await _orchestration.ExecuteCloseAsync(
                    CompanyId,
                    FiscalPeriodId,
                    username: User.Identity?.Name ?? "unknown",
                    overrideFailures: overrideFailures,
                    overrideReason: overrideReason);

                if (result.Succeeded)
                {
                    Notice = $"Period '{result.Period?.Name}' closed successfully. " +
                             (result.Packet.OverrideUsed ? "Closed with override." : "All pre-flight checks passed.");
                }
                else
                {
                    Error = result.ErrorMessage ?? "Close failed. See pre-flight checks below.";
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }

            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostReopenAsync(string reopenReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reopenReason))
                {
                    Error = "A written reason is required to reopen a closed period.";
                }
                else
                {
                    var result = await _orchestration.ReopenAsync(
                        CompanyId, FiscalPeriodId,
                        username: User.Identity?.Name ?? "unknown",
                        reason: reopenReason);
                    Notice = result.Succeeded
                        ? $"Period '{result.Period?.Name}' reopened."
                        : (result.ErrorMessage ?? "Reopen failed.");
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }

            await LoadAsync();
            return Page();
        }

        private async Task LoadAsync()
        {
            Period = await _db.FiscalPeriods
                .Include(p => p.FiscalYear)
                .Include(p => p.Company)
                .FirstOrDefaultAsync(p => p.Id == FiscalPeriodId && p.CompanyId == CompanyId);

            if (Period == null) return;

            // Always run preflight so the user sees the current state.
            // For already-closed periods this is informational (the actual
            // close was already done; the snapshot is authoritative).
            Preflight = await _orchestration.RunPreflightAsync(CompanyId, FiscalPeriodId);

            if (Period.Status != PeriodStatus.Open
                && !string.IsNullOrWhiteSpace(Period.PreflightSnapshotJson))
            {
                try
                {
                    CapturedPacket = JsonSerializer.Deserialize<PeriodClosePacket>(
                        Period.PreflightSnapshotJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    CapturedPacket = null;
                }
            }
        }
    }
}
