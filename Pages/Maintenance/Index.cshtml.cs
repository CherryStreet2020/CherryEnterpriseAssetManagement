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
    public class IndexModel : PageModel
    {
        private readonly MaintenanceService _maintenanceService;
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ICloseoutService _closeoutService;
        private readonly IWorkOrderOriginService _originService;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;

        public IndexModel(
            MaintenanceService maintenanceService, 
            AppDbContext context, 
            IModuleGuardService moduleGuard, 
            ICloseoutService closeoutService,
            IWorkOrderOriginService originService,
            ITenantContext tenantContext,
            ILookupService lookupService)
        {
            _maintenanceService = maintenanceService;
            _context = context;
            _moduleGuard = moduleGuard;
            _closeoutService = closeoutService;
            _originService = originService;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

        public MaintenanceStats Stats { get; set; } = new();
        public List<MaintenanceEvent> Events { get; set; } = new();
        public Dictionary<int, WorkOrderOriginInfo> EventOrigins { get; set; } = new();
        public List<Asset> Assets { get; set; } = new();
        public List<Technician> Technicians { get; set; } = new();
        public List<RecurringFailure> RecurringFailures30 { get; set; } = new();
        public List<RecurringFailure> RecurringFailures90 { get; set; } = new();
        public List<SelectListItem> MaintenanceTypeOptions { get; set; } = new();
        public List<SelectListItem> PriorityOptions { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? CreateFor { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? PmtaId { get; set; }
        
        public string FilterLabel { get; set; } = "All Events";

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("workorders"))
                return RedirectToPage("/ModuleDisabled", new { module = "Work Orders" });

            MaintenanceTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenanceType", null, "");
            PriorityOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenancePriority", null, "");

            var companyId = GetCompanyId();
            Stats = await _maintenanceService.GetMaintenanceStatsAsync();
            
            var isOriginFilter = Filter?.ToLower() is "smartassist" or "pm" or "manual";
            
            if (isOriginFilter)
            {
                Events = await _maintenanceService.GetEventsForDashboardAsync(null, 250);
            }
            else
            {
                Events = await _maintenanceService.GetEventsForDashboardAsync(Filter, 250);
            }
            
            var maintAssetQuery = _context.Assets
                .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.Status == AssetStatus.Active);
            if (_tenantContext.SiteId.HasValue)
                maintAssetQuery = maintAssetQuery.Where(a => a.SiteId == _tenantContext.SiteId.Value);
            Assets = await maintAssetQuery
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();
            Technicians = await _context.Technicians.Where(t => t.Active).OrderBy(t => t.Name).ToListAsync();
            RecurringFailures30 = await _closeoutService.GetRecurringFailuresAsync(30, 5);
            RecurringFailures90 = await _closeoutService.GetRecurringFailuresAsync(90, 5);
            
            EventOrigins = await _originService.GetOriginsForEventsAsync(Events.Select(e => e.Id));
            
            if (Filter?.ToLower() == "smartassist")
            {
                Events = Events.Where(e => EventOrigins.TryGetValue(e.Id, out var o) && o.Origin == WorkOrderOrigin.SmartAssist).ToList();
            }
            else if (Filter?.ToLower() == "pm")
            {
                Events = Events.Where(e => EventOrigins.TryGetValue(e.Id, out var o) && o.Origin == WorkOrderOrigin.PMSchedule).ToList();
            }
            else if (Filter?.ToLower() == "manual")
            {
                Events = Events.Where(e => EventOrigins.TryGetValue(e.Id, out var o) && o.Origin == WorkOrderOrigin.Manual).ToList();
            }
            
            FilterLabel = Filter?.ToLower() switch
            {
                "overdue" => "Overdue Events",
                "scheduled" => "Scheduled Events",
                "inprogress" => "In Progress Events",
                "completed" => "Completed (Last 30 Days)",
                "smartassist" => "Smart Assist WOs",
                "pm" => "PM Schedule WOs",
                "manual" => "Manual WOs",
                _ => "All Events"
            };
            
            return Page();
        }

        public async Task<IActionResult> OnPostCreateEventAsync(
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
                return NotFound();
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
            return RedirectToPage();
        }
    }
}
