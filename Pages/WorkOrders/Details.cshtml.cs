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

namespace Abs.FixedAssets.Pages.WorkOrders
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ICloseoutService _closeoutService;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public DetailsModel(AppDbContext context, ICloseoutService closeoutService, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _closeoutService = closeoutService;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public MaintenanceEvent WorkOrder { get; set; } = null!;
        public List<WorkOrderOperation> Operations { get; set; } = new();
        public List<WorkOrderPart> LegacyParts { get; set; } = new();
        public List<Technician> Technicians { get; set; } = new();
        public List<Craft> Crafts { get; set; } = new();
        public List<LaborType> LaborTypes { get; set; } = new();
        public List<Item> Items { get; set; } = new();
        public List<SelectListItem> OperationStatusOptions { get; set; } = new();
        public List<SelectListItem> OperationTypeOptions { get; set; } = new();

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
            var wo = await _context.MaintenanceEvents
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
                .Where(p => p.MaintenanceEventId == id)
                .OrderBy(p => p.Id)
                .ToListAsync();

            Technicians = await _context.Technicians.Where(t => t.Active).OrderBy(t => t.Name).ToListAsync();
            Crafts = await _context.Crafts.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            LaborTypes = await _context.LaborTypes.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
            Items = await _context.Items.Where(i => i.IsActive).OrderBy(i => i.PartNumber).Take(100).ToListAsync();

            OperationStatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "OperationStatus", null, "");
            OperationTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "OperationType", null, "");

            return Page();
        }

        public async Task<IActionResult> OnPostAddOperationAsync(int workOrderId, string title, int typeLookupValueId, int? craftId, decimal plannedHours, string? description)
        {
            var maxSeq = await _context.WorkOrderOperations
                .Where(o => o.MaintenanceEventId == workOrderId)
                .MaxAsync(o => (int?)o.Sequence) ?? 0;

            var nextNum = await _context.WorkOrderOperations
                .Where(o => o.MaintenanceEventId == workOrderId)
                .CountAsync() + 1;

            var resolvedType = OperationType.Mechanical;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (OperationType)enumVal;
            }

            var operation = new WorkOrderOperation
            {
                MaintenanceEventId = workOrderId,
                OperationNumber = $"OP-{nextNum:D3}",
                Sequence = maxSeq + 10,
                Title = title?.ToUpper() ?? "NEW OPERATION",
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                CraftId = craftId,
                PlannedHours = plannedHours,
                Description = description?.ToUpper(),
                Status = OperationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkOrderOperations.Add(operation);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id = workOrderId });
        }

        public async Task<IActionResult> OnPostMoveOperationAsync(int operationId, string direction)
        {
            var companyId = GetCompanyId();
            var operation = await _context.WorkOrderOperations
                .Include(o => o.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(o => o.Id == operationId && o.MaintenanceEvent != null && o.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(o.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (operation == null) return NotFound();

            var allOps = await _context.WorkOrderOperations
                .Where(o => o.MaintenanceEventId == operation.MaintenanceEventId)
                .OrderBy(o => o.Sequence)
                .ToListAsync();

            var idx = allOps.FindIndex(o => o.Id == operationId);
            if (direction == "up" && idx > 0)
            {
                var temp = allOps[idx].Sequence;
                allOps[idx].Sequence = allOps[idx - 1].Sequence;
                allOps[idx - 1].Sequence = temp;
            }
            else if (direction == "down" && idx < allOps.Count - 1)
            {
                var temp = allOps[idx].Sequence;
                allOps[idx].Sequence = allOps[idx + 1].Sequence;
                allOps[idx + 1].Sequence = temp;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = operation.MaintenanceEventId });
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int operationId, int statusLookupValueId)
        {
            var companyId = GetCompanyId();
            var operation = await _context.WorkOrderOperations
                .Include(o => o.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(o => o.Id == operationId && o.MaintenanceEvent != null && o.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(o.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (operation == null) return NotFound();

            var resolvedStatus = OperationStatus.Pending;
            int? resolvedStatusLvId = statusLookupValueId > 0 ? statusLookupValueId : (int?)null;
            var statusLv = await _lookupService.GetValueByIdAsync(null, null, statusLookupValueId);
            if (statusLv != null)
            {
                resolvedStatusLvId = statusLv.Id;
                if (int.TryParse(statusLv.Code, out var enumVal))
                    resolvedStatus = (OperationStatus)enumVal;
            }

            operation.Status = resolvedStatus;
            operation.StatusLookupValueId = resolvedStatusLvId;
            if (resolvedStatus == OperationStatus.InProgress && operation.ActualStartDate == null)
                operation.ActualStartDate = DateTime.UtcNow;
            if (resolvedStatus == OperationStatus.Completed)
            {
                operation.ActualEndDate = DateTime.UtcNow;
                operation.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = operation.MaintenanceEventId });
        }

        public async Task<IActionResult> OnPostAddLaborAsync(int operationId, int? technicianId, decimal hours, decimal hourlyRate, string? notes)
        {
            var companyId = GetCompanyId();
            var operation = await _context.WorkOrderOperations
                .Include(o => o.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(o => o.Id == operationId && o.MaintenanceEvent != null && o.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(o.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (operation == null) return NotFound();

            var labor = new WorkOrderOperationLabor
            {
                WorkOrderOperationId = operationId,
                TechnicianId = technicianId,
                WorkDate = DateTime.UtcNow.Date,
                Hours = hours,
                HourlyRate = hourlyRate,
                Notes = notes?.ToUpper(),
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkOrderOperationLabors.Add(labor);
            
            operation.ActualHours += hours;
            
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = operation.MaintenanceEventId });
        }

        public async Task<IActionResult> OnPostAddToolAsync(int operationId, string toolName, int quantityRequired, string? notes)
        {
            var companyId = GetCompanyId();
            var operation = await _context.WorkOrderOperations
                .Include(o => o.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(o => o.Id == operationId && o.MaintenanceEvent != null && o.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(o.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (operation == null) return NotFound();

            var tool = new WorkOrderOperationTool
            {
                WorkOrderOperationId = operationId,
                ToolName = toolName?.ToUpper() ?? "TOOL",
                QuantityRequired = quantityRequired,
                Notes = notes?.ToUpper(),
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkOrderOperationTools.Add(tool);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = operation.MaintenanceEventId });
        }

        public async Task<IActionResult> OnPostAddPartAsync(int operationId, int itemId, decimal quantityPlanned, decimal unitCost, string? notes)
        {
            var companyId = GetCompanyId();
            var operation = await _context.WorkOrderOperations
                .Include(o => o.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(o => o.Id == operationId && o.MaintenanceEvent != null && o.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(o.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (operation == null) return NotFound();

            var part = new WorkOrderOperationPart
            {
                WorkOrderOperationId = operationId,
                ItemId = itemId,
                QuantityPlanned = quantityPlanned,
                UnitCost = unitCost,
                Notes = notes?.ToUpper(),
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkOrderOperationParts.Add(part);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = operation.MaintenanceEventId });
        }

        // ==================== WO-LEVEL MATERIALS (WorkOrderPart) ====================

        public async Task<IActionResult> OnPostAddPlannedMaterialAsync(int workOrderId, int itemId, decimal quantityPlanned, string? notes)
        {
            var item = await _context.Items.Where(i => i.Id == itemId).FirstOrDefaultAsync();
            if (item == null) return NotFound();

            var part = new WorkOrderPart
            {
                MaintenanceEventId = workOrderId,
                ItemId = itemId,
                QuantityPlanned = quantityPlanned,
                UnitCost = item.StandardCost,
                Notes = notes?.ToUpper(),
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkOrderParts.Add(part);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = workOrderId });
        }

        public async Task<IActionResult> OnPostIssueMaterialAsync(int workOrderPartId, decimal quantityIssue)
        {
            var companyId = GetCompanyId();
            var part = await _context.WorkOrderParts
                .Include(p => p.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(p => p.Id == workOrderPartId && p.MaintenanceEvent != null && p.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(p.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();

            if (quantityIssue <= 0) return RedirectToPage(new { id = part.MaintenanceEventId });

            // For planned materials: cannot issue more than (planned - already issued)
            // For unplanned: allow any quantity (auto-extends planned)
            decimal actualIssue = quantityIssue;
            if (part.QuantityPlanned > 0)
            {
                var maxIssuable = part.QuantityPlanned - part.QuantityIssued;
                if (maxIssuable <= 0) return RedirectToPage(new { id = part.MaintenanceEventId });
                actualIssue = Math.Min(quantityIssue, maxIssuable);
            }
            else
            {
                // Unplanned issue: extend planned to match
                part.QuantityPlanned += actualIssue;
            }

            part.QuantityIssued += actualIssue;
            // Used = Issued - Returned (recalculate, don't add incrementally)
            part.QuantityUsed = part.QuantityIssued - part.QuantityReturned;
            part.IssuedDate = DateTime.UtcNow;
            part.IssuedBy = User.Identity?.Name ?? "SYSTEM";
            part.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = part.MaintenanceEventId });
        }

        public async Task<IActionResult> OnPostReturnMaterialAsync(int workOrderPartId, decimal quantityReturn)
        {
            var companyId = GetCompanyId();
            var part = await _context.WorkOrderParts
                .Include(p => p.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(p => p.Id == workOrderPartId && p.MaintenanceEvent != null && p.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(p.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();

            if (quantityReturn <= 0) return RedirectToPage(new { id = part.MaintenanceEventId });

            // Guardrail: Cannot return more than net issued (issued - returned)
            var maxReturnable = part.QuantityIssued - part.QuantityReturned;
            if (maxReturnable <= 0) return RedirectToPage(new { id = part.MaintenanceEventId });
            var actualReturn = Math.Min(quantityReturn, maxReturnable);

            part.QuantityReturned += actualReturn;
            // Used = Issued - Returned (recalculate to prevent negative)
            part.QuantityUsed = Math.Max(0, part.QuantityIssued - part.QuantityReturned);
            part.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = part.MaintenanceEventId });
        }

        public async Task<IActionResult> OnPostRemovePlannedMaterialAsync(int workOrderPartId)
        {
            var companyId = GetCompanyId();
            var part = await _context.WorkOrderParts
                .Include(p => p.MaintenanceEvent).ThenInclude(m => m!.Asset)
                .Where(p => p.Id == workOrderPartId && p.MaintenanceEvent != null && p.MaintenanceEvent.Asset != null && _tenantContext.VisibleCompanyIds.Contains(p.MaintenanceEvent.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();

            // Guardrail: Cannot remove if already issued
            if (part.QuantityIssued > 0) return RedirectToPage(new { id = part.MaintenanceEventId });

            var woId = part.MaintenanceEventId;
            _context.WorkOrderParts.Remove(part);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = woId });
        }

        public async Task<IActionResult> OnPostLoadTemplateMaterialsAsync(int workOrderId)
        {
            // Find the WO and check for PMTA linkage in CustomField1
            var companyId = GetCompanyId();
            var wo = await _context.MaintenanceEvents
                .Include(m => m.Asset)
                .Where(m => m.Id == workOrderId && m.Asset != null && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null) return NotFound();

            // S1-2: prefer PMTemplateAssetId FK; fall back to legacy CustomField1
            // marker for in-flight rows that predate the migration.
            int? pmtaId = wo.PMTemplateAssetId;
            if (pmtaId == null
                && !string.IsNullOrEmpty(wo.CustomField1)
                && wo.CustomField1.StartsWith("PMTA:"))
            {
                int.TryParse(wo.CustomField1.Substring(5), out var parsed);
                pmtaId = parsed > 0 ? parsed : null;
            }

            if (pmtaId == null)
            {
                TempData["Error"] = "No PM Template linked to this Work Order.";
                return RedirectToPage(new { id = workOrderId });
            }

            // Get the PMTemplateId from the PMTemplateAsset
            var pmta = await _context.PMTemplateAssets
                .Include(a => a.PMTemplate)
                    .ThenInclude(t => t!.Items!)
                        .ThenInclude(i => i.Item)
                .FirstOrDefaultAsync(a => a.Id == pmtaId);

            if (pmta?.PMTemplate?.Items == null || !pmta.PMTemplate.Items.Any())
            {
                TempData["Warning"] = "PM Template has no materials defined.";
                return RedirectToPage(new { id = workOrderId });
            }

            // Get existing WO parts to avoid duplicates
            var existingItemIds = await _context.WorkOrderParts
                .Where(p => p.MaintenanceEventId == workOrderId)
                .Select(p => p.ItemId)
                .ToListAsync();

            int added = 0;
            foreach (var templateItem in pmta.PMTemplate.Items)
            {
                if (existingItemIds.Contains(templateItem.ItemId)) continue;

                var woPart = new WorkOrderPart
                {
                    MaintenanceEventId = workOrderId,
                    ItemId = templateItem.ItemId,
                    QuantityPlanned = templateItem.Quantity,
                    UnitCost = templateItem.Item?.StandardCost ?? 0,
                    Notes = templateItem.Notes?.ToUpper(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.WorkOrderParts.Add(woPart);
                added++;
            }

            if (added > 0)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Loaded {added} material(s) from PM Template.";
            }
            else
            {
                TempData["Warning"] = "All template materials already exist on this Work Order.";
            }

            return RedirectToPage(new { id = workOrderId });
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
    }
}
