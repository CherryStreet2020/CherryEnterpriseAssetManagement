using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Maintenance
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly MaintenanceService _maintenanceService;
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public CreateModel(
            MaintenanceService maintenanceService,
            AppDbContext context,
            IModuleGuardService moduleGuard,
            ILookupService lookupService,
            ITenantContext tenantContext)
        {
            _maintenanceService = maintenanceService;
            _context = context;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

        public List<Asset> Assets { get; set; } = new();
        public List<Technician> Technicians { get; set; } = new();
        public List<SelectListItem> MaintenanceTypeOptions { get; set; } = new();
        public List<SelectListItem> PriorityOptions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? CreateFor { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PmtaId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("workorders"))
                return RedirectToPage("/ModuleDisabled", new { module = "Work Orders" });

            await LoadFormDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            int assetId,
            int typeLookupValueId,
            string description,
            DateTime scheduledDate,
            int priorityLookupValueId,
            decimal? estimatedCost,
            string? vendor,
            int? technicianId,
            int? pmtaId)
        {
            var companyId = GetCompanyId();

            var assetValid = await _context.Assets
                .AnyAsync(a => a.Id == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.Status == AssetStatus.Active);
            if (!assetValid)
            {
                TempData["Error"] = "Please select a valid asset.";
                await LoadFormDataAsync();
                return Page();
            }

            // S1-2: stamp the PMTemplateAssetId FK directly. Replaces the
            // prior CustomField1 = "PMTA:N" string hack.
            int? resolvedPmTemplateAssetId = null;
            if (pmtaId.HasValue)
            {
                var pmta = await _context.Set<PMTemplateAsset>()
                    .Include(p => p.Asset)
                    .FirstOrDefaultAsync(p => p.Id == pmtaId.Value &&
                                              p.AssetId == assetId &&
                                              p.IsActive &&
                                              p.Asset != null &&
                                              _tenantContext.VisibleCompanyIds.Contains(p.Asset.CompanyId ?? 0));
                if (pmta != null)
                {
                    resolvedPmTemplateAssetId = pmtaId.Value;
                }
            }

            var resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var resolvedPriorityLvId = priorityLookupValueId > 0 ? priorityLookupValueId : (int?)null;
            var resolvedType = MaintenanceType.Preventative;
            var resolvedPriority = MaintenancePriority.Medium;

            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (Enum.TryParse<MaintenanceType>(typeLv.Code, true, out var parsedType))
                    resolvedType = parsedType;
            }

            var priorityLv = await _lookupService.GetValueByIdAsync(null, null, priorityLookupValueId);
            if (priorityLv != null)
            {
                resolvedPriorityLvId = priorityLv.Id;
                if (Enum.TryParse<MaintenancePriority>(priorityLv.Code, true, out var parsedPriority))
                    resolvedPriority = parsedPriority;
            }

            var scheduledLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenanceStatus", "Scheduled");

            var evt = new MaintenanceEvent
            {
                AssetId = assetId,
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                Description = description,
                ScheduledDate = scheduledDate,
                Priority = resolvedPriority,
                PriorityLookupValueId = resolvedPriorityLvId,
                EstimatedCost = estimatedCost ?? 0,
                Vendor = vendor,
                TechnicianId = technicianId,
                Status = MaintenanceStatus.Scheduled,
                StatusLookupValueId = scheduledLv?.Id,
                PMTemplateAssetId = resolvedPmTemplateAssetId
            };

            await _maintenanceService.CreateEventAsync(evt);
            return RedirectToPage("/Maintenance/Details", new { id = evt.Id });
        }

        private async Task LoadFormDataAsync()
        {
            var companyId = GetCompanyId();
            var createAssetQuery = _context.Assets
                .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.Status == AssetStatus.Active);
            if (_tenantContext.SiteId.HasValue)
                createAssetQuery = createAssetQuery.Where(a => a.SiteId == _tenantContext.SiteId.Value);
            Assets = await createAssetQuery
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();
            Technicians = await _context.Technicians.Where(t => t.Active).OrderBy(t => t.Name).ToListAsync();
            MaintenanceTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenanceType", null, "");
            PriorityOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenancePriority", null, "");
        }
    }
}
