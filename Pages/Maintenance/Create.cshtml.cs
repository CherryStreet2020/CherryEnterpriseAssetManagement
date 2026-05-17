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
            // DEF-N04 (PR #85): default to Corrective for ad-hoc/user-created
            // WOs rather than Preventative. The old default of Preventative
            // meant any lookup Code that didn't parse to a MaintenanceType
            // enum value name silently dropped to PM — but the seeded
            // MaintenanceType lookup uses industry-standard short codes
            // (PM/CM/PDM/EM/OH per SystemReferenceSeedPipeline.cs L351-355)
            // and NONE of them parse via Enum.TryParse against the enum
            // value names (Preventative/Corrective/Predictive/Emergency).
            // Result: every ad-hoc WO got stamped Preventative regardless of
            // UI selection, corrupting the PM-Compliance KPI — the metric
            // every EAM vendor (Maximo, Infor EAM, SAP PM) wins deals on.
            var resolvedType = MaintenanceType.Corrective;
            var resolvedPriority = MaintenancePriority.Medium;

            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                resolvedType = ResolveMaintenanceTypeFromLookup(typeLv.Code, typeLv.Name) ?? resolvedType;
            }

            var priorityLv = await _lookupService.GetValueByIdAsync(null, null, priorityLookupValueId);
            if (priorityLv != null)
            {
                resolvedPriorityLvId = priorityLv.Id;
                if (Enum.TryParse<MaintenancePriority>(priorityLv.Code, true, out var parsedPriority))
                    resolvedPriority = parsedPriority;
            }

            var scheduledLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenanceStatus", "Scheduled");

            var evt = new WorkOrder
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

        /// <summary>
        /// Maps a maintenance-type lookup Code/Name to the legacy
        /// MaintenanceType enum that backs PM-compliance metrics and category
        /// filtering. Three tiers:
        /// 1. Exact enum-name match on the Code (future-proof for seeds that
        ///    use enum names directly).
        /// 2. Name stem match (e.g. "Corrective Maintenance" → Corrective;
        ///    strips trailing " Maintenance" suffix).
        /// 3. Industry abbreviation map (PM/CM/PDM/EM/OH — matching the
        ///    SystemReferenceSeedPipeline seed and what every Maximo /
        ///    SAP PM / Infor EAM operator types muscle-memory).
        /// Returns null if nothing matches; caller falls back to its own
        /// default. Kept private to this page model — if a second caller
        /// needs the same logic, lift to a shared service.
        /// </summary>
        private static MaintenanceType? ResolveMaintenanceTypeFromLookup(string? code, string? name)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                if (Enum.TryParse<MaintenanceType>(code, ignoreCase: true, out var parsed))
                    return parsed;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var stem = name.Trim();
                if (stem.EndsWith(" Maintenance", StringComparison.OrdinalIgnoreCase))
                    stem = stem.Substring(0, stem.Length - " Maintenance".Length).TrimEnd();
                if (Enum.TryParse<MaintenanceType>(stem, ignoreCase: true, out var parsedName))
                    return parsedName;
            }

            return code?.Trim().ToUpperInvariant() switch
            {
                "PM"  => MaintenanceType.Preventative,
                "CM"  => MaintenanceType.Corrective,
                "PDM" => MaintenanceType.Predictive,
                "EM"  => MaintenanceType.Emergency,
                "OH"  => MaintenanceType.Other,   // Overhaul has no dedicated enum value; closest legacy slot is Other
                "INS" => MaintenanceType.Inspection,
                "CAL" => MaintenanceType.Calibration,
                "UPG" => MaintenanceType.Upgrade,
                _     => null
            };
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
