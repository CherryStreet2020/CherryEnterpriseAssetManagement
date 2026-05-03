using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Reports
{
    public class ExportModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ExportService _exportService;
        private readonly DepreciationService _depService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public ExportModel(AppDbContext db, ExportService exportService, DepreciationService depService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _exportService = exportService;
            _depService = depService;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

        public async Task<IActionResult> OnGetAsync(string? type, string? format, int? year, int? assetId)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });

            year ??= DateTime.Now.Year;

            if (string.IsNullOrWhiteSpace(type))
                return RedirectToPage("/Reports/Index");

            return type.ToLowerInvariant() switch
            {
                "assets" => await ExportAssets(format),
                "journals" => await ExportJournals(format),
                "cca" => await ExportCca(format, year.Value),
                "maintenance" => await ExportMaintenance(format),
                "cip" => await ExportCip(format),
                "depreciation" => await ExportDepreciationSchedule(format, assetId ?? 0),
                _ => RedirectToPage("/Reports/Index")
            };
        }

        private async Task<IActionResult> ExportAssets(string? format)
        {
            var exportQuery = _db.Assets.Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                exportQuery = exportQuery.Where(a => a.SiteId == _tenantContext.SiteId.Value);
            var assets = await exportQuery.OrderBy(a => a.AssetNumber).ToListAsync();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return format?.ToLower() switch
            {
                "csv" => File(_exportService.ExportAssetsToCsv(assets), "text/csv", $"assets_{timestamp}.csv"),
                "excel" => File(_exportService.ExportAssetsToExcel(assets), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"assets_{timestamp}.xlsx"),
                "pdf" => File(_exportService.ExportAssetsToPdf(assets), "application/pdf", $"assets_{timestamp}.pdf"),
                _ => RedirectToPage("/Reports/Index")
            };
        }

        private async Task<IActionResult> ExportJournals(string? format)
        {
            var journals = await _db.JournalEntries
                .Include(j => j.Book)
                .Include(j => j.Lines)
                .Where(j => j.Book != null && _tenantContext.VisibleCompanyIds.Contains(j.Book.CompanyId ?? 0))
                .OrderByDescending(j => j.PostingDate)
                .ToListAsync();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return format?.ToLower() switch
            {
                "csv" => File(_exportService.ExportJournalsToCsv(journals), "text/csv", $"journals_{timestamp}.csv"),
                "excel" => File(_exportService.ExportJournalsToExcel(journals), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"journals_{timestamp}.xlsx"),
                _ => RedirectToPage("/Reports/Index")
            };
        }

        private async Task<IActionResult> ExportCca(string? format, int year)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var classes = await _db.CcaClasses.OrderBy(c => c.ClassNumber).ToListAsync();
            var balances = await _db.CcaClassBalances
                .Where(b => b.FiscalYear == year && visibleIds.Contains(b.CompanyId))
                .ToListAsync();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return format?.ToLower() switch
            {
                "csv" => File(_exportService.ExportCcaReportToCsv(classes, balances, year), "text/csv", $"cca_report_{year}_{timestamp}.csv"),
                "excel" => File(_exportService.ExportCcaReportToExcel(classes, balances, year), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"cca_report_{year}_{timestamp}.xlsx"),
                _ => RedirectToPage("/Reports/Index")
            };
        }

        private async Task<IActionResult> ExportMaintenance(string? format)
        {
            var eventsQuery = _db.MaintenanceEvents
                .Include(m => m.Asset)
                .Where(m => m.Asset != null && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                eventsQuery = eventsQuery.Where(m => m.Asset!.SiteId == _tenantContext.SiteId.Value);
            var events = await eventsQuery
                .OrderByDescending(m => m.ScheduledDate)
                .ToListAsync();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return format?.ToLower() switch
            {
                "excel" => File(_exportService.ExportMaintenanceToExcel(events), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"maintenance_{timestamp}.xlsx"),
                _ => RedirectToPage("/Maintenance/Index")
            };
        }

        private async Task<IActionResult> ExportCip(string? format)
        {
            var projects = await _db.CipProjects
                .Where(p => _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return format?.ToLower() switch
            {
                "excel" => File(_exportService.ExportCipToExcel(projects), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"cip_projects_{timestamp}.xlsx"),
                _ => RedirectToPage("/CIP/Index")
            };
        }

        private async Task<IActionResult> ExportDepreciationSchedule(string? format, int assetId)
        {
            var asset = await _db.Assets
                .Where(a => a.Id == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (asset == null) return NotFound();

            var schedule = _depService.BuildSchedule(asset, DateTime.Now);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return format?.ToLower() switch
            {
                "excel" => File(_exportService.ExportDepreciationScheduleToExcel(asset, schedule, "GAAP"), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"depreciation_{asset.AssetNumber}_{timestamp}.xlsx"),
                _ => RedirectToPage("/Assets/Schedule", new { id = assetId })
            };
        }
    }
}
