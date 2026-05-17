using System.Linq;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Cip;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Maintenance
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly MaintenanceService _maintenanceService;
        private readonly AttachmentService _attachmentService;
        private readonly AppDbContext _context;
        private readonly IWorkOrderOriginService _originService;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;
        private readonly IModuleGuardService _moduleGuard;
        private readonly IPeriodGuard _periodGuard;
        private readonly CipAutoCostPostingService _cipAutoCostPosting;
        private readonly DepreciationBackfillService _depBackfill;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(MaintenanceService maintenanceService,
            AttachmentService attachmentService,
            AppDbContext context,
            IWorkOrderOriginService originService,
            ITenantContext tenantContext,
            ILookupService lookupService,
            IModuleGuardService moduleGuard,
            IPeriodGuard periodGuard,
            CipAutoCostPostingService cipAutoCostPosting,
            DepreciationBackfillService depBackfill,
            ILogger<DetailsModel> logger)
        {
            _moduleGuard = moduleGuard;
            _maintenanceService = maintenanceService;
            _attachmentService = attachmentService;
            _context = context;
            _originService = originService;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
            _periodGuard = periodGuard;
            _cipAutoCostPosting = cipAutoCostPosting;
            _depBackfill = depBackfill;
            _logger = logger;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

        private async Task<WorkOrder?> GetScopedEventAsync(int id)
        {
            var companyId = GetCompanyId();
            return await _context.WorkOrders
                .Include(e => e.Asset)
                .Include(e => e.Technician)
                .Include(e => e.Operations)!
                    .ThenInclude(o => o.AssignedTechnician)
                .Where(e => e.Asset != null && _tenantContext.VisibleCompanyIds.Contains(e.Asset.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || e.Asset.SiteId == _tenantContext.SiteId.Value))
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        private async Task<WorkOrder?> GetScopedEventSimpleAsync(int id)
        {
            var companyId = GetCompanyId();
            return await _context.WorkOrders
                .Include(e => e.Asset)
                .Where(e => e.Asset != null && _tenantContext.VisibleCompanyIds.Contains(e.Asset.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || e.Asset.SiteId == _tenantContext.SiteId.Value))
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public WorkOrder Event { get; set; } = null!;
        public WorkOrderOriginInfo Origin { get; set; } = new();
        public List<Attachment> Attachments { get; set; } = new();
        public List<Technician> Technicians { get; set; } = new();
        public List<WorkOrderOperation> Operations { get; set; } = new();
        public List<Craft> Crafts { get; set; } = new();
        public List<WorkOrderPart> Parts { get; set; } = new();
        public bool IsCapitalized { get; set; }
        public List<SelectListItem> PriorityOptions { get; set; } = new();
        public List<SelectListItem> MaintenanceTypeOptions { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> CraftOptions { get; set; } = new();
        public List<SelectListItem> AttachmentCategoryOptions { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "overview";

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }
        
        private static readonly HashSet<string> ValidTabs = new(StringComparer.OrdinalIgnoreCase)
        {
            "overview", "operations", "parts", "labor", "closeout", "history"
        };

        // PR #98: Hard 301 redirect to the canonical /WorkOrders/Details surface.
        // After PRs #89, #92, #94, #96, #97 every routine WO workflow lives on
        // the modern page; the legacy GET view here is now obsolete. POST
        // handlers below are kept so any external integration (CRM webhook,
        // mobile app) that still POSTs to legacy ?handler= URLs continues to
        // function — but a fresh GET always lands on the modern page.
        //
        // Module guard stays first; if Maintenance is disabled the user gets
        // the ModuleDisabled landing, same as the modern page does. The
        // `id` route value and `returnUrl` query string are carried forward
        // so "back to results" still works after the redirect.
        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Maintenance" });

            return RedirectToPagePermanent("/WorkOrders/Details",
                new { id, returnUrl = ReturnUrl });
        }

        private async Task SyncStatusFkAsync(WorkOrder evt, MaintenanceStatus status)
        {
            evt.Status = status;
            var lv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenanceStatus", ((int)status).ToString());
            if (lv != null)
                evt.StatusLookupValueId = lv.Id;
        }

        public async Task<IActionResult> OnPostStartAsync(int id)
        {
            var evt = await GetScopedEventSimpleAsync(id);
            if (evt == null)
                return NotFound();

            await SyncStatusFkAsync(evt, MaintenanceStatus.InProgress);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCompleteAsync(
            int id,
            string? resolution,
            decimal? laborCost,
            decimal? materialsCost,
            decimal? partsCost,
            decimal? outsideVendorCost)
        {
            var evt = await GetScopedEventSimpleAsync(id);
            if (evt == null)
                return NotFound();

            // Period locking: closing a work order with cost lines (labor,
            // materials, parts, outside vendor) creates a financial-state
            // change that downstream GL postings hang off of. Block close
            // when the asset's company has the relevant period closed —
            // same posture as Pages/Assets/Improve.cshtml.cs and Dispose.
            var completionDate = DateTime.UtcNow;
            // Resolve the company from the event's asset; matches the way
            // GetScopedEventSimpleAsync scopes via Asset.CompanyId.
            var asset = evt.AssetId > 0
                ? await _context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == evt.AssetId)
                : null;
            var workCompanyId = asset?.CompanyId ?? _tenantContext.CompanyId ?? 0;
            if (workCompanyId > 0)
            {
                var periodCheck = await _periodGuard.CanPostAsync(workCompanyId, completionDate);
                if (!periodCheck.IsAllowed)
                {
                    TempData["Error"] = periodCheck.Reason
                        ?? $"Cannot close work order: posting period for {completionDate:yyyy-MM-dd} is closed.";
                    return RedirectToPage(new { id });
                }
            }

            // S1-6: roll up per-operation labor + parts into the WO totals.
            // Audit: WO ActualCost was whatever the user typed in the form,
            // and the per-operation Hours×HourlyRate / QuantityUsed×UnitCost
            // data was ignored. Operation-level data is the canonical source
            // — if it exists, it wins; otherwise we accept the manual input.
            var operationsWithDetails = await _context.Set<WorkOrderOperation>()
                .Include(o => o.LaborEntries)
                .Include(o => o.Parts)
                .Where(o => o.WorkOrderId == evt.Id)
                .ToListAsync();

            decimal operationLaborTotal = operationsWithDetails
                .SelectMany(o => o.LaborEntries ?? Enumerable.Empty<WorkOrderOperationLabor>())
                .Sum(l => l.TotalCost);

            decimal operationPartsTotal = operationsWithDetails
                .SelectMany(o => o.Parts ?? Enumerable.Empty<WorkOrderOperationPart>())
                .Sum(p => p.TotalCost);

            decimal workOrderPartsTotal = await _context.WorkOrderParts
                .Where(p => p.WorkOrderId == evt.Id)
                .SumAsync(p => p.QuantityUsed * p.UnitCost);

            // Rollups override the manual fields when operation-level data
            // exists. Materials and OutsideVendor have no operation-level
            // source today — those stay as user input.
            var rolledUpLabor = operationLaborTotal > 0 ? operationLaborTotal : (laborCost ?? 0);
            var rolledUpParts = (operationPartsTotal + workOrderPartsTotal) > 0
                ? (operationPartsTotal + workOrderPartsTotal)
                : (partsCost ?? 0);

            await SyncStatusFkAsync(evt, MaintenanceStatus.Completed);
            evt.CompletedDate = completionDate;
            evt.Resolution = resolution;
            evt.LaborCost = rolledUpLabor;
            evt.MaterialsCost = materialsCost ?? 0;
            evt.PartsCost = rolledUpParts;
            evt.OutsideVendorCost = outsideVendorCost ?? 0;
            evt.ActualCost = rolledUpLabor + (materialsCost ?? 0) + rolledUpParts + (outsideVendorCost ?? 0);
            evt.CompletedBy = User.Identity?.Name;

            await _context.SaveChangesAsync();

            // S1-3: route the just-closed WO into CipAutoCostPostingService.
            // The service early-returns if evt.CipProjectId is null, so this
            // is a no-op for non-CIP WOs. Idempotent: re-runs return the
            // existing CipCost. Failure is logged but does not roll back the
            // close — the WO close is the operational truth, CIP routing is
            // a downstream financial concern.
            try
            {
                var cipCost = await _cipAutoCostPosting.PostFromWorkOrderAsync(evt.Id);
                if (cipCost != null)
                    TempData["Success"] = $"Work order {evt.WorkOrderNumber} closed and ${cipCost.Amount:N2} posted to CIP project.";
            }
            catch (Exception cipEx)
            {
                _logger.LogError(cipEx,
                    "CIP auto-cost posting failed for closed work order {WorkOrderId} ({WorkOrderNumber})",
                    evt.Id, evt.WorkOrderNumber);
            }

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCancelAsync(int id)
        {
            var evt = await GetScopedEventSimpleAsync(id);
            if (evt == null)
                return NotFound();

            await SyncStatusFkAsync(evt, MaintenanceStatus.Cancelled);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        private static readonly HashSet<string> AllowedContentTypes = new()
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf", "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain", "text/csv"
        };
        private const long MaxFileSize = 10 * 1024 * 1024;

        public async Task<IActionResult> OnPostUploadAsync(int id, IFormFile file, int category, string? description)
        {
            if (file == null || file.Length == 0)
                return RedirectToPage(new { id });

            if (file.Length > MaxFileSize || !AllowedContentTypes.Contains(file.ContentType))
                return RedirectToPage(new { id });

            var evt = await GetScopedEventSimpleAsync(id);
            if (evt == null)
                return NotFound();

            using var stream = file.OpenReadStream();
            await _attachmentService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                evt.AssetId,
                AttachmentSource.WorkOrder,
                id,
                (AttachmentCategory)category,
                description,
                User.Identity?.Name
            );

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteAttachmentAsync(int id, int attachmentId)
        {
            var evt = await GetScopedEventSimpleAsync(id);
            if (evt == null)
                return NotFound();

            var attachment = await _context.Attachments
                .Where(a => a.Id == attachmentId && a.WorkOrderId == id)
                .FirstOrDefaultAsync();
            if (attachment == null)
                return RedirectToPage(new { id });

            await _attachmentService.DeleteAsync(attachmentId);
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCapitalizeAsync(int id, decimal amount, string description)
        {
            var evt = await GetScopedEventSimpleAsync(id);
            if (evt == null)
                return NotFound();

            if (evt.Status != MaintenanceStatus.Completed || evt.AssetId <= 0)
                return RedirectToPage(new { id });

            if (!string.IsNullOrEmpty(evt.CustomField2) && evt.CustomField2.StartsWith("IMPR:"))
                return RedirectToPage(new { id });

            // S2-9: capitalizing a WO into an asset's cost basis is a financial
            // posting (Asset.AcquisitionCost increment + CapitalImprovement row
            // that downstream depreciation reads). Must respect the period lock
            // — same posture as Pages/Assets/Improve.cshtml.cs:99 and
            // Pages/Assets/Dispose.cshtml.cs:128.
            var improvementDate = evt.CompletedDate ?? DateTime.UtcNow;
            var asset = await _context.Assets
                .Where(a => a.Id == evt.AssetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (asset == null)
                return NotFound();

            var assetCompanyId = asset.CompanyId ?? _tenantContext.CompanyId ?? 0;
            if (assetCompanyId > 0)
            {
                var periodCheck = await _periodGuard.CanPostAsync(assetCompanyId, improvementDate);
                if (!periodCheck.IsAllowed)
                {
                    TempData["Error"] = periodCheck.Reason
                        ?? $"Cannot capitalize: posting period for {improvementDate:yyyy-MM-dd} is closed.";
                    return RedirectToPage(new { id });
                }
            }

            var improvement = new CapitalImprovement
            {
                AssetId = evt.AssetId,
                ImprovementDate = improvementDate,
                Description = string.IsNullOrEmpty(description) ? $"WO {evt.WorkOrderNumber}: {evt.Description}" : description,
                Cost = amount,
                Vendor = evt.Vendor,
                Notes = $"Source: Work Order {evt.WorkOrderNumber ?? evt.Id.ToString()}",
                Capitalized = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name
            };

            // PR #99: Save first so EF populates improvement.Id, *then* stamp
            // the IMPR pointer on CustomField2 and save again. Pre-fix code
            // stamped IMPR:{improvement.Id} while Id was still its 0 default,
            // so every capitalization landed as IMPR:0 — the marker satisfied
            // the IsCapitalized "starts with IMPR:" gate but pointed nowhere.
            _context.CapitalImprovements.Add(improvement);
            asset.AcquisitionCost += amount;
            await _context.SaveChangesAsync();

            evt.CustomField2 = $"IMPR:{improvement.Id}";
            await _context.SaveChangesAsync();

            // S2-9: refresh the depreciation snapshot on Asset and each
            // AssetBookSettings so subsequent reads (asset detail, KPI
            // dashboard, schedule report) reflect the new cost basis.
            // Same pattern as Pages/Assets/Improve.cshtml.cs (PR #27).
            // Posted JournalEntries are append-only and untouched.
            await _depBackfill.RecomputeAssetAsync(evt.AssetId, improvementDate);

            TempData["Message"] = $"Capitalized ${amount:N0} to asset {evt.Asset?.AssetNumber} as improvement #{improvement.Id}";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostEditAsync(
            int id,
            int maintenanceTypeLookupValueId,
            int priorityLookupValueId,
            DateTime scheduledDate,
            string? workOrderNumber,
            string? vendor,
            int? technicianId,
            decimal estimatedCost,
            string? description,
            string? notes)
        {
            var evt = await GetScopedEventSimpleAsync(id);
            if (evt == null)
                return NotFound();

            if (evt.Status == MaintenanceStatus.Completed || evt.Status == MaintenanceStatus.Cancelled)
                return RedirectToPage(new { id });

            var typeLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, maintenanceTypeLookupValueId);
            if (typeLv != null)
            {
                evt.TypeLookupValueId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    evt.Type = (MaintenanceType)enumVal;
            }

            var priorityLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, priorityLookupValueId);
            if (priorityLv != null)
            {
                evt.PriorityLookupValueId = priorityLv.Id;
                if (int.TryParse(priorityLv.Code, out var enumVal))
                    evt.Priority = (MaintenancePriority)enumVal;
            }
            evt.ScheduledDate = scheduledDate;
            evt.WorkOrderNumber = workOrderNumber;
            evt.Vendor = vendor;
            evt.TechnicianId = technicianId;
            evt.EstimatedCost = estimatedCost;
            evt.Description = description ?? "";
            evt.Notes = notes;

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDispatchUpdateAsync(
            int id,
            int priorityLookupValueId,
            DateTime scheduledDate,
            int? technicianId,
            string? tab)
        {
            var returnTab = ValidTabs.Contains(tab ?? "") ? tab : "overview";
            
            var resolvedPriority = MaintenancePriority.Medium;
            int? resolvedPriorityLvId = null;
            var priorityLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, priorityLookupValueId);
            if (priorityLv != null && int.TryParse(priorityLv.Code, out var pEnumVal))
            {
                resolvedPriority = (MaintenancePriority)pEnumVal;
                resolvedPriorityLvId = priorityLv.Id;
            }

            var result = await _maintenanceService.UpdateDispatchAsync(
                id, 
                resolvedPriority, 
                scheduledDate, 
                technicianId);
            
            if (result == null)
                return NotFound();

            result.PriorityLookupValueId = resolvedPriorityLvId;
            await _context.SaveChangesAsync();
            
            return RedirectToPage(new { id, tab = returnTab });
        }
    }
}
