using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Reports
{
    public class BuilderModel : PageModel
    {
        private readonly ReportBuilderService _reportService;
        private readonly ILookupService _lookupService;
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public BuilderModel(ReportBuilderService reportService, ILookupService lookupService, AppDbContext context, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _reportService = reportService;
            _lookupService = lookupService;
            _context = context;
            _tenantContext = tenantContext;
        }

        public List<string> AvailableFields { get; set; } = new();
        public List<string> Locations { get; set; } = new();
        public List<string> SelectedFields { get; set; } = new();
        public string? LocationFilter { get; set; }
        public string? StatusFilter { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public List<Dictionary<string, object>> ReportData { get; set; } = new();
        public List<SelectListItem> AssetStatusOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });

            var visibleIds = _tenantContext.VisibleCompanyIds;
            AvailableFields = _reportService.GetAvailableFields();
            Locations = await _reportService.GetLocationsAsync();
            SelectedFields = new List<string> { "AssetNumber", "Description", "Location", "AcquisitionCost", "Status" };
            await LoadLookupsAsync();
        
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            List<string> selectedFields,
            string? locationFilter,
            string? statusFilter,
            decimal? minValue,
            decimal? maxValue)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            AvailableFields = _reportService.GetAvailableFields();
            Locations = await _reportService.GetLocationsAsync();
            SelectedFields = selectedFields.Any() ? selectedFields : new List<string> { "AssetNumber", "Description" };
            LocationFilter = locationFilter;
            StatusFilter = statusFilter;
            MinValue = minValue;
            MaxValue = maxValue;

            ReportData = await _reportService.BuildAssetReportAsync(
                SelectedFields,
                locationFilter,
                statusFilter,
                minValue,
                maxValue);

            await LoadLookupsAsync();
            return Page();
        }

        private async Task LoadLookupsAsync()
        {
            AssetStatusOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "AssetStatus", StatusFilter, "All Statuses");
        }
    }
}
