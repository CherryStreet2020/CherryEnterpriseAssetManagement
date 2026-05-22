using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Maintenance;
using Abs.FixedAssets.Services.Navigation;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;

namespace Abs.FixedAssets.Pages.WorkOrders
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ICloseoutService _closeoutService;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;
        private readonly IOutboxWriter _outbox;
        private readonly IGlAccountResolver _glResolver;
        // PR #94: AttachmentService injected to migrate the Attachments
        // workflow off the legacy /Maintenance/Details surface. Same service
        // the legacy handlers call, so the upload/download/delete behavior
        // is bit-identical — only the entry point moves to the modern page.
        private readonly AttachmentService _attachmentService;
        // PR #96: Capitalize → CIP migration. Period guard refuses postings
        // into closed fiscal periods (same posture as Asset Improve / Dispose).
        // DepreciationBackfillService refreshes the asset's depreciation
        // snapshot after the AcquisitionCost increment.
        private readonly IPeriodGuard _periodGuard;
        private readonly DepreciationBackfillService _depBackfill;
        // PR #97: MaintenanceService.UpdateDispatchAsync ports the quick
        // dispatch reassignment from the legacy page (priority + date +
        // technician). Same service the legacy /Maintenance/Details surface
        // calls so behavior is bit-identical.
        private readonly MaintenanceService _maintenanceService;
        // PR #102 (B-10): Capitalize-to-CIP now posts a real GL entry.
        // Pre-fix, Asset.AcquisitionCost was bumped in-place with no offsetting
        // CR — every capitalization broke trial balance silently.
        private readonly ICapitalImprovementPostingService _improvementPosting;

        // Sprint 12.9 PR #3 — IWorkOrderService extracts the worst-offender
        // page's writes off direct AppDbContext access. v1 covers 5 of 17
        // writes (Add/Move/UpdateStatus/AddTool for Operations + AddPlanned
        // for Materials). Subsequent PRs in Sprint 12.9 (#3.1-3.3) finish
        // the JE-posting + WO-level writes. _context still injected for
        // read-path projections, which is ADR-025 compliant.
        private readonly IWorkOrderService _workOrderService;

        public DetailsModel(AppDbContext context, ICloseoutService closeoutService, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard, IOutboxWriter outbox, IGlAccountResolver glResolver, AttachmentService attachmentService,
            IPeriodGuard periodGuard, DepreciationBackfillService depBackfill, MaintenanceService maintenanceService,
            ICapitalImprovementPostingService improvementPosting, IWorkOrderService workOrderService)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _closeoutService = closeoutService;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
            _outbox = outbox;
            _glResolver = glResolver;
            _attachmentService = attachmentService;
            _periodGuard = periodGuard;
            _depBackfill = depBackfill;
            _maintenanceService = maintenanceService;
            _improvementPosting = improvementPosting;
            _workOrderService = workOrderService;
        }

        public WorkOrder WorkOrder { get; set; } = null!;
        public List<WorkOrderOperation> Operations { get; set; } = new();
        public List<WorkOrderPart> LegacyParts { get; set; } = new();
        public List<Technician> Technicians { get; set; } = new();
        public List<Craft> Crafts { get; set; } = new();
        public List<LaborType> LaborTypes { get; set; } = new();
        public List<Item> Items { get; set; } = new();
        public List<SelectListItem> OperationStatusOptions { get; set; } = new();
        public List<SelectListItem> OperationTypeOptions { get; set; } = new();

        // PR #94: Attachments migrated from legacy /Maintenance/Details.
        public List<Attachment> Attachments { get; set; } = new();
        public List<SelectListItem> AttachmentCategoryOptions { get; set; } = new();

        // PR #96: Capitalize → CIP. Mirrors the IsCapitalized property the
        // legacy page uses to gate the form ("IMPR:" prefix on CustomField2
        // means this WO has already been capitalized to an asset improvement).
        public bool IsCapitalized => !string.IsNullOrEmpty(WorkOrder?.CustomField2)
                                     && WorkOrder.CustomField2.StartsWith("IMPR:");

        // PR #97: WO-level lookup dropdowns for the Edit form.
        public List<SelectListItem> MaintenanceTypeOptions { get; set; } = new();
        public List<SelectListItem> MaintenancePriorityOptions { get; set; } = new();
        // PR #104 (B-16): FailureCode master, seeded reference data. Loaded
        // in OnGetAsync for the Edit form's dropdown so operators can no
        // longer free-text the value.
        public List<FailureCode> FailureCodeOptions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public decimal TotalPlannedHours => Operations.Sum(o => o.PlannedHours);
        public decimal TotalActualHours => Operations.Sum(o => o.ActualHours);
        public decimal TotalLaborCost => Operations.SelectMany(o => o.LaborEntries ?? new List<WorkOrderOperationLabor>()).Sum(l => l.Hours * l.HourlyRate);
        public int TotalPartsCount => Operations.SelectMany(o => o.Parts ?? new List<WorkOrderOperationPart>()).Count() + LegacyParts.Count;
        public decimal TotalPartsCost => Operations.SelectMany(o => o.Parts ?? new List<WorkOrderOperationPart>()).Sum(p => p.QuantityUsed * p.UnitCost) + LegacyParts.Sum(p => p.QuantityUsed * p.UnitCost);
        public bool RequiresShutdown => Operations.Any(o => o.RequiresShutdown);
        public bool RequiresLOTO => Operations.Any(o => o.RequiresLOTO);

        public string GetBackUrl() => ReturnUrlHelper.GetBackUrl(ReturnUrl, "/WorkOrders/Details");

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Work Orders" });

            if (id == null) return RedirectToPage("/Maintenance/Index");

            var companyId = GetCompanyId();
            var wo = await _context.WorkOrders
                .Include(m => m.Asset)
                .Include(m => m.Technician)
                .Include(m => m.Operations!)
                    .ThenInclude(o => o.AssignedTechnician)
                .Include(m => m.Operations!)
                    .ThenInclude(o => o.Craft)
                .Include(m => m.Operations!)
                    .ThenInclude(o => o.LaborEntries!)
                        .ThenInclude(l => l.Technician)
                .Include(m => m.Operations!)
                    .ThenInclude(o => o.Tools)
                .Include(m => m.Operations!)
                    .ThenInclude(o => o.Parts!)
                        .ThenInclude(p => p.Item)
                .Where(m => m.Asset != null && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || m.Asset.SiteId == _tenantContext.SiteId.Value))
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (wo == null) return NotFound();

            WorkOrder = wo;
            Operations = wo.Operations?.OrderBy(o => o.Sequence).ToList() ?? new();

            LegacyParts = await _context.WorkOrderParts
                .Include(p => p.Item)
                .Where(p => p.WorkOrderId == id)
                .OrderBy(p => p.Id)
                .ToListAsync();

            Technicians = await _context.Technicians.Where(t => t.Active).OrderBy(t => t.Name).ToListAsync();
            Crafts = await _context.Crafts.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            LaborTypes = await _context.LaborTypes.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
            // S1-7: tenant-scope the picker. Items with no CompanyId are
            // treated as global (visible to every tenant); company-scoped
            // items are filtered by VisibleCompanyIds.
            Items = await _context.Items
                .Where(i => i.IsActive
                    && (i.CompanyId == null
                        || _tenantContext.VisibleCompanyIds.Contains(i.CompanyId.Value)))
                .OrderBy(i => i.PartNumber)
                .Take(100)
                .ToListAsync();

            OperationStatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "OperationStatus", null, "");
            OperationTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "OperationType", null, "");

            // PR #94: Pull attachments for this WO + the category options for
            // the upload form. Uses the same AttachmentService API the legacy
            // page calls so the underlying storage / metadata is bit-identical.
            Attachments = await _attachmentService.GetByWorkOrderAsync(id.Value);
            AttachmentCategoryOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "AttachmentCategory", null, "");

            // PR #97: WO-level lookups for the Edit form (type + priority).
            // Selected values pre-fill from the current WO so the operator
            // sees what the WO is set to, not a placeholder.
            MaintenanceTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenanceType", wo.TypeLookupValueId, "");
            MaintenancePriorityOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "MaintenancePriority", wo.PriorityLookupValueId, "");

            // PR #104 (B-16): seeded FailureCode master, sorted by code so
            // the dropdown reads naturally (BRG-* before HYD-* before MOT-*).
            // Active-only — admins can deactivate retired codes from the
            // admin UI without removing them from historical WOs.
            FailureCodeOptions = await _context.FailureCodes
                .Where(f => f.IsActive)
                .OrderBy(f => f.Code)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAddOperationAsync(int workOrderId, string title, int typeLookupValueId, int? craftId, decimal plannedHours, string? description)
        {
            // Sprint 12.9 PR #3 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.AddOperationAsync(
                new AddOperationRequest(workOrderId, title, typeLookupValueId, craftId, plannedHours, description),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = workOrderId });
        }

        public async Task<IActionResult> OnPostMoveOperationAsync(int operationId, string direction)
        {
            // Sprint 12.9 PR #3 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.MoveOperationAsync(
                new MoveOperationRequest(operationId, direction),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderId });
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int operationId, int statusLookupValueId)
        {
            // Sprint 12.9 PR #3 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.UpdateOperationStatusAsync(
                new UpdateOperationStatusRequest(operationId, statusLookupValueId),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderId });
        }

        public async Task<IActionResult> OnPostAddLaborAsync(int operationId, int? technicianId, decimal hours, decimal hourlyRate, string? notes)
        {
            // Sprint 12.9 PR #3.1 — delegated to IWorkOrderService (ADR-025 D5).
            // Posts labor entry + WO-LBR JE (DR MaintenanceLabor / CR AccruedLabor).
            var result = await _workOrderService.AddLaborAsync(
                new AddLaborRequest(operationId, technicianId, hours, hourlyRate, notes),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderId });
        }

        public async Task<IActionResult> OnPostAddToolAsync(int operationId, string toolName, int quantityRequired, string? notes)
        {
            // Sprint 12.9 PR #3 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.AddOperationToolAsync(
                new AddOperationToolRequest(operationId, toolName, quantityRequired, notes),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderId });
        }

        public async Task<IActionResult> OnPostAddPartAsync(int operationId, int itemId, decimal quantityPlanned, decimal unitCost, string? notes)
        {
            // Sprint 12.9 PR #3.2 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.AddOperationPartAsync(
                new AddOperationPartRequest(operationId, itemId, quantityPlanned, unitCost, notes),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderId });
        }

        // PR #106 / B-20: Operation-level parts now have a real Issue/Return
        // lifecycle that mirrors the WorkOrderPart flow from PR #89 — increments
        // QuantityIssued + QuantityUsed, decrements ItemInventory at the source
        // location, writes an ItemTransaction audit row, and posts a balanced
        // material-movement JE so finance sees the maintenance cost roll into
        // MaintenanceMaterials. Pre-PR the operation-level path was an
        // accounting orphan — Materials section JEs existed but per-operation
        // parts didn't post, leaving the WO cost rollup understated whenever
        // ops chose the operation-level surface.
        public async Task<IActionResult> OnPostIssueOperationPartAsync(int operationPartId, decimal quantityIssue)
        {
            // Sprint 12.9 PR #3.1 — delegated to IWorkOrderService (ADR-025 D5).
            // Issues part + inventory movement + ItemTransaction + WO-ISS-OP JE.
            var result = await _workOrderService.IssueOperationPartAsync(
                new IssueOperationPartRequest(operationPartId, quantityIssue, User.Identity?.Name),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderOperation!.WorkOrderId });
        }

        public async Task<IActionResult> OnPostReturnOperationPartAsync(int operationPartId, decimal quantityReturn)
        {
            // Sprint 12.9 PR #3.1 — delegated to IWorkOrderService (ADR-025 D5).
            // Reverses inventory + WO-RTN-OP reversing JE.
            var result = await _workOrderService.ReturnOperationPartAsync(
                new ReturnOperationPartRequest(operationPartId, quantityReturn),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderOperation!.WorkOrderId });
        }

        // ==================== WO-LEVEL MATERIALS (WorkOrderPart) ====================

        public async Task<IActionResult> OnPostAddPlannedMaterialAsync(int workOrderId, int itemId, decimal quantityPlanned, string? notes)
        {
            // Sprint 12.9 PR #3 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.AddPlannedMaterialAsync(
                new AddPlannedMaterialRequest(workOrderId, itemId, quantityPlanned, notes),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = workOrderId });
        }

        public async Task<IActionResult> OnPostIssueMaterialAsync(int workOrderPartId, decimal quantityIssue)
        {
            // Sprint 12.9 PR #3.2 — delegated to IWorkOrderService (ADR-025 D5).
            // Issues part + inventory movement + ItemTransaction + WO-ISS JE + ItemIssuedV1 outbox event.
            var result = await _workOrderService.IssueMaterialAsync(
                new IssueMaterialRequest(workOrderPartId, quantityIssue, User.Identity?.Name),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderId });
        }

        public async Task<IActionResult> OnPostReturnMaterialAsync(int workOrderPartId, decimal quantityReturn)
        {
            // Sprint 12.9 PR #3.2 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.ReturnMaterialAsync(
                new ReturnMaterialRequest(workOrderPartId, quantityReturn),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.WorkOrderId });
        }

        public async Task<IActionResult> OnPostRemovePlannedMaterialAsync(int workOrderPartId)
        {
            // Sprint 12.9 PR #3.2 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.RemovePlannedMaterialAsync(
                new RemovePlannedMaterialRequest(workOrderPartId),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value });
        }

        public async Task<IActionResult> OnPostLoadTemplateMaterialsAsync(int workOrderId)
        {
            // Sprint 12.9 PR #3.2 — delegated to IWorkOrderService (ADR-025 D5).
            var result = await _workOrderService.LoadTemplateMaterialsAsync(
                new LoadTemplateMaterialsRequest(workOrderId),
                HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();

            // Map structured outcome to legacy TempData slots so the UX
            // is bit-identical to the pre-refactor surface.
            var outcome = result.Value!;
            switch (outcome.Status)
            {
                case LoadTemplateMaterialsStatus.Loaded:
                    TempData["Success"] = outcome.Message;
                    break;
                case LoadTemplateMaterialsStatus.NoTemplate:
                    TempData["Error"] = outcome.Message;
                    break;
                case LoadTemplateMaterialsStatus.EmptyTemplate:
                case LoadTemplateMaterialsStatus.AllAlreadyExist:
                    TempData["Warning"] = outcome.Message;
                    break;
            }
            return RedirectToPage(new { id = outcome.WorkOrderId });
        }

        public async Task<IActionResult> OnPostCloseWorkOrderAsync(int workOrderId, string? lessonsLearned)
        {
            var username = User.Identity?.Name ?? "system";
            var result = await _closeoutService.CloseWorkOrderAsync(workOrderId, lessonsLearned, username);

            if (result.Success)
            {
                TempData["Success"] = "Work Order closed successfully with auto-generated closeout summary.";
            }
            else
            {
                TempData["Error"] = result.Error ?? "Failed to close work order.";
            }

            return RedirectToPage(new { id = workOrderId });
        }

        public async Task<IActionResult> OnPostSaveLessonAsync(int workOrderId, string lessonText, string? tags)
        {
            if (string.IsNullOrWhiteSpace(lessonText))
            {
                TempData["Error"] = "Lesson text is required.";
                return RedirectToPage(new { id = workOrderId });
            }

            var username = User.Identity?.Name ?? "system";
            var result = await _closeoutService.SaveLessonAsync(workOrderId, lessonText, tags, username);

            if (result.Success)
            {
                TempData["Success"] = "Lesson saved to knowledge base.";
            }
            else
            {
                TempData["Error"] = result.Error ?? "Failed to save lesson.";
            }

            return RedirectToPage(new { id = workOrderId });
        }

        public string GeneratePreviewSummary()
        {
            return _closeoutService.GenerateCloseoutSummary(WorkOrder, Operations);
        }

        // ==================== ATTACHMENTS (PR #94 migration) ====================
        // Allowlist + size cap ported verbatim from the legacy
        // /Maintenance/Details.cshtml.cs. Keeping these as private statics on
        // the class avoids re-declaring them anywhere — there's one upload
        // surface per page model.
        private static readonly HashSet<string> AllowedAttachmentContentTypes = new()
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf", "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain", "text/csv"
        };
        private const long MaxAttachmentFileSize = 10 * 1024 * 1024;

        public async Task<IActionResult> OnPostUploadAttachmentAsync(int id, IFormFile file, int category, string? description)
        {
            if (file == null || file.Length == 0)
                return RedirectToPage(new { id });

            if (file.Length > MaxAttachmentFileSize || !AllowedAttachmentContentTypes.Contains(file.ContentType))
            {
                TempData["Error"] = $"File rejected: type '{file.ContentType}' not allowed or size exceeds {MaxAttachmentFileSize / (1024 * 1024)}MB.";
                return RedirectToPage(new { id });
            }

            // Re-fetch with tenant scoping. The OnGetAsync already verifies
            // visibility, but every POST handler does its own scope check so a
            // future refactor that drops the GetAsync invocation can't open
            // a write-leak vector. Mirrors the pattern used elsewhere on the
            // page (see OnPostIssueMaterialAsync).
            var wo = await _context.WorkOrders
                .Where(m => m.Id == id
                    && m.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null) return NotFound();

            using var stream = file.OpenReadStream();
            await _attachmentService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                wo.AssetId,
                AttachmentSource.WorkOrder,
                id,
                (AttachmentCategory)category,
                description,
                User.Identity?.Name
            );

            TempData["Success"] = $"Uploaded {file.FileName}.";
            return RedirectToPage(new { id });
        }

        // ==================== EDIT + DISPATCH (PR #97 migration) ====================
        // Ports OnPostEditAsync from legacy /Maintenance/Details. Editable
        // while the WO is open (not Completed and not Cancelled). All field
        // updates flow through the same path as legacy so behavior is
        // bit-identical.
        public async Task<IActionResult> OnPostEditWorkOrderAsync(
            int id,
            int maintenanceTypeLookupValueId,
            int priorityLookupValueId,
            DateTime scheduledDate,
            string? workOrderNumber,
            string? vendor,
            int? technicianId,
            decimal estimatedCost,
            string? description,
            string? notes,
            int? failureCodeId)
        {
            var wo = await _context.WorkOrders
                .Where(m => m.Id == id
                    && m.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null) return NotFound();

            if (wo.Status == MaintenanceStatus.Completed || wo.Status == MaintenanceStatus.Cancelled)
            {
                TempData["Error"] = "Cannot edit a Completed or Cancelled work order.";
                return RedirectToPage(new { id });
            }

            // Type — keep enum value in sync with the lookup row (same dual-write pattern
            // used in OnPostAddOperationAsync).
            var typeLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, maintenanceTypeLookupValueId);
            if (typeLv != null)
            {
                wo.TypeLookupValueId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var typeEnumVal))
                    wo.Type = (MaintenanceType)typeEnumVal;
            }

            // Priority — same dual-write.
            var priorityLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, priorityLookupValueId);
            if (priorityLv != null)
            {
                wo.PriorityLookupValueId = priorityLv.Id;
                if (int.TryParse(priorityLv.Code, out var pEnumVal))
                    wo.Priority = (MaintenancePriority)pEnumVal;
            }

            wo.ScheduledDate = scheduledDate;
            wo.WorkOrderNumber = workOrderNumber;
            wo.Vendor = vendor;
            wo.TechnicianId = technicianId;
            wo.EstimatedCost = estimatedCost;
            wo.Description = description ?? "";
            wo.Notes = notes;

            // PR #104 (B-16): write the FK alongside the denormalized
            // FailureCode text label. Reads the master row to populate the
            // string field with its canonical Name so downstream code that
            // still consumes wo.FailureCode (legacy reports, the closeout
            // summary template) shows the operator-friendly label, not the
            // numeric Id. Null FK clears the label.
            if (failureCodeId.HasValue && failureCodeId.Value > 0)
            {
                var fc = await _context.FailureCodes
                    .FirstOrDefaultAsync(f => f.Id == failureCodeId.Value && f.IsActive);
                if (fc != null)
                {
                    wo.FailureCodeId = fc.Id;
                    wo.FailureCode = fc.Name;
                }
            }
            else
            {
                wo.FailureCodeId = null;
                wo.FailureCode = null;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Work order updated.";
            return RedirectToPage(new { id });
        }

        // Quick dispatch reassignment — narrower than Edit (just priority,
        // scheduled date, technician). Same shape as the legacy handler so
        // any external integration POSTing to ?handler=DispatchUpdate keeps
        // working. Delegates to MaintenanceService for the actual update.
        public async Task<IActionResult> OnPostDispatchUpdateAsync(
            int id,
            int priorityLookupValueId,
            DateTime scheduledDate,
            int? technicianId)
        {
            var wo = await _context.WorkOrders
                .Where(m => m.Id == id
                    && m.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null) return NotFound();

            var resolvedPriority = MaintenancePriority.Medium;
            int? resolvedPriorityLvId = null;
            var priorityLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, priorityLookupValueId);
            if (priorityLv != null && int.TryParse(priorityLv.Code, out var pEnumVal))
            {
                resolvedPriority = (MaintenancePriority)pEnumVal;
                resolvedPriorityLvId = priorityLv.Id;
            }

            var result = await _maintenanceService.UpdateDispatchAsync(id, resolvedPriority, scheduledDate, technicianId);
            if (result == null) return NotFound();

            result.PriorityLookupValueId = resolvedPriorityLvId;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Dispatch updated.";
            return RedirectToPage(new { id });
        }

        // ==================== CAPITALIZE → CIP (PR #96 migration) ====================
        // Ports OnPostCapitalizeAsync from legacy /Maintenance/Details. Same
        // financial posting: creates a CapitalImprovement row, increments
        // Asset.AcquisitionCost, refreshes the depreciation snapshot. Behavior
        // is bit-identical so legacy-vs-modern parity is guaranteed; only the
        // entry point moves.
        public async Task<IActionResult> OnPostCapitalizeAsync(int id, decimal amount, string description)
        {
            var wo = await _context.WorkOrders
                .Include(m => m.Asset)
                .Where(m => m.Id == id
                    && m.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null) return NotFound();

            // Guardrail mirrors legacy: only completed WOs with a valid asset
            // can be capitalized, and double-capitalization is refused.
            if (wo.Status != MaintenanceStatus.Completed || wo.AssetId <= 0)
            {
                TempData["Error"] = "Cannot capitalize: WO must be Completed and tied to an asset.";
                return RedirectToPage(new { id });
            }
            if (!string.IsNullOrEmpty(wo.CustomField2) && wo.CustomField2.StartsWith("IMPR:"))
            {
                TempData["Error"] = "Cannot capitalize: this WO has already been capitalized.";
                return RedirectToPage(new { id });
            }
            if (amount <= 0m)
            {
                TempData["Error"] = "Capitalization amount must be positive.";
                return RedirectToPage(new { id });
            }

            var improvementDate = wo.CompletedDate ?? DateTime.UtcNow;
            var asset = wo.Asset!; // non-null by the Include + filter above
            var assetCompanyId = asset.CompanyId ?? _tenantContext.CompanyId ?? 0;

            // Period guard — same posture as Pages/Assets/Improve.cshtml.cs
            // and Pages/Assets/Dispose.cshtml.cs. Capitalizing into a closed
            // period would silently distort prior-period AcquisitionCost and
            // depreciation; refuse fast with a user-readable reason.
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
                AssetId = wo.AssetId,
                ImprovementDate = improvementDate,
                Description = string.IsNullOrEmpty(description) ? $"WO {wo.WorkOrderNumber}: {wo.Description}" : description,
                Cost = amount,
                Vendor = wo.Vendor,
                Notes = $"Source: Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()}",
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

            wo.CustomField2 = $"IMPR:{improvement.Id}";
            await _context.SaveChangesAsync();

            // PR #102 (B-10): post the JE that finally makes this a real GL
            // event. Pre-fix, AcquisitionCost was bumped on line 1090 above
            // and the GL never saw a thing — trial balance silently drifted
            // every time someone capitalized a WO. Service posts DR AssetCost
            // / CR CipPending and respects the fiscal-period guard.
            try
            {
                await _improvementPosting.PostImprovementJeAsync(
                    improvementId: improvement.Id,
                    assetId: wo.AssetId,
                    companyId: assetCompanyId,
                    amount: amount,
                    improvementDate: improvementDate,
                    description: $"WO {wo.WorkOrderNumber} — {wo.Description}");
            }
            catch (InvalidOperationException ex)
            {
                // Period-guard refusal. The WO close above already passed the
                // period check; this is defense-in-depth. Surface the message
                // and return — the AcquisitionCost bump and improvement row
                // are already committed, so the operator sees the capitalize
                // succeeded with a GL posting refusal noted. The follow-up is
                // to either re-open the period and retry, or post a manual JE.
                TempData["Error"] = $"Capitalized to asset successfully, but the GL posting was refused: {ex.Message}";
                return RedirectToPage(new { id });
            }

            // Refresh the depreciation snapshot on Asset and each
            // AssetBookSettings so subsequent reads (asset detail, KPI
            // dashboard, schedule report) reflect the new cost basis.
            await _depBackfill.RecomputeAssetAsync(wo.AssetId, improvementDate);

            TempData["Success"] = $"Capitalized {amount:C} to asset {asset.AssetNumber} as improvement #{improvement.Id}.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteAttachmentAsync(int id, int attachmentId)
        {
            var wo = await _context.WorkOrders
                .Where(m => m.Id == id
                    && m.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null) return NotFound();

            // Verify the attachment actually belongs to this WO before
            // delete — protects against a forged form posting an attachmentId
            // from a different WO the user can see.
            var attachment = await _context.Attachments
                .Where(a => a.Id == attachmentId && a.WorkOrderId == id)
                .FirstOrDefaultAsync();
            if (attachment == null)
                return RedirectToPage(new { id });

            await _attachmentService.DeleteAsync(attachmentId);
            TempData["Success"] = "Attachment deleted.";
            return RedirectToPage(new { id });
        }
    }
}
