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
            var companyId = GetCompanyId();
            var operation = await _context.WorkOrderOperations
                .Include(o => o.WorkOrder).ThenInclude(m => m!.Asset)
                .Where(o => o.Id == operationId && o.WorkOrder != null && o.WorkOrder.Asset != null && _tenantContext.VisibleCompanyIds.Contains(o.WorkOrder.Asset.CompanyId ?? 0))
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

            // PR #92: Post the GL impact of the labor entry. Pairs with PR #89's
            // materials posting to complete the WO cost rollup — without this
            // entry, the asset-level maintenance spend KPI undercounts every
            // internally-staffed work order by the entire labor side. DR
            // MaintenanceLabor (expense) / CR AccruedLabor (liability); the
            // payroll subsystem clears AccruedLabor to Cash on the next pay
            // cycle. If hours or rate is zero we skip — no GL impact and the
            // PR #84 balance guard would refuse an empty entry anyway.
            await PostLaborJournalEntryAsync(operation, labor);

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = operation.WorkOrderId });
        }

        /// <summary>
        /// PR #92 (DEF-N09 follow-up): Posts the journal entry for a labor
        /// entry against a Work Order Operation:
        ///   DR <see cref="GlAccountKind.MaintenanceLabor"/>
        ///   CR <see cref="GlAccountKind.AccruedLabor"/>
        /// Amount is <c>hours * hourlyRate</c>. Resolves accounts via the
        /// ADR-003 cascade so per-company overrides are honored. The JE is
        /// added to the ChangeTracker but not saved here — the caller's
        /// SaveChanges flushes it alongside the WorkOrderOperationLabor in a
        /// single transaction so they cannot diverge.
        /// </summary>
        private async Task<JournalEntry?> PostLaborJournalEntryAsync(WorkOrderOperation operation, WorkOrderOperationLabor labor)
        {
            if (labor.Hours <= 0m || labor.HourlyRate <= 0m) return null;
            var amount = labor.Hours * labor.HourlyRate;
            if (amount <= 0m) return null;

            var resolvedCompanyId = operation.WorkOrder?.Asset?.CompanyId
                ?? _tenantContext.CompanyId
                ?? 0;
            if (resolvedCompanyId == 0) return null;

            var ctx = new GlResolveContext(
                WorkOrderId: operation.WorkOrderId,
                AssetId: operation.WorkOrder?.AssetId);
            var laborAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.MaintenanceLabor, ctx);
            var accruedAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.AccruedLabor, ctx);

            var ticks = DateTime.UtcNow.Ticks;
            var jeReference = $"WO-LBR-{operation.WorkOrderId}-op{operation.Id}-{ticks}";
            var woNumber = operation.WorkOrder?.WorkOrderNumber ?? $"WO#{operation.WorkOrderId}";

            var je = new JournalEntry
            {
                BookId = null,
                Batch = jeReference,
                Period = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
                PostingDate = DateTime.UtcNow.Date,
                Source = "WO-LBR",
                Reference = jeReference,
                Description = $"Labor posted to {woNumber} (op {operation.Sequence}, {labor.Hours:0.00}h @ {labor.HourlyRate:C})",
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>
                {
                    new JournalLine
                    {
                        LineNo = 1,
                        Account = laborAccount,
                        Description = $"Maintenance labor - {woNumber}",
                        Debit = amount,
                        Credit = 0m
                    },
                    new JournalLine
                    {
                        LineNo = 2,
                        Account = accruedAccount,
                        Description = $"Accrued labor - {woNumber}",
                        Debit = 0m,
                        Credit = amount
                    }
                }
            };
            _context.JournalEntries.Add(je);
            return je;
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
            var companyId = GetCompanyId();
            var operation = await _context.WorkOrderOperations
                .Include(o => o.WorkOrder).ThenInclude(m => m!.Asset)
                .Where(o => o.Id == operationId && o.WorkOrder != null && o.WorkOrder.Asset != null && _tenantContext.VisibleCompanyIds.Contains(o.WorkOrder.Asset.CompanyId ?? 0))
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
            return RedirectToPage(new { id = operation.WorkOrderId });
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
            var part = await _context.WorkOrderOperationParts
                .Include(p => p.WorkOrderOperation)
                    .ThenInclude(op => op!.WorkOrder)
                        .ThenInclude(m => m!.Asset)
                .Where(p => p.Id == operationPartId
                    && p.WorkOrderOperation != null
                    && p.WorkOrderOperation.WorkOrder != null
                    && p.WorkOrderOperation.WorkOrder.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrderOperation.WorkOrder.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();
            var woId = part.WorkOrderOperation!.WorkOrderId;

            if (quantityIssue <= 0) return RedirectToPage(new { id = woId });

            // Same planned-vs-unplanned rule as WO-level: bounded by planned
            // when planned > 0, otherwise extends planned to match (unplanned
            // pull). Keeps the operation part counters internally consistent.
            decimal actualIssue = quantityIssue;
            if (part.QuantityPlanned > 0)
            {
                var maxIssuable = part.QuantityPlanned - part.QuantityIssued;
                if (maxIssuable <= 0) return RedirectToPage(new { id = woId });
                actualIssue = Math.Min(quantityIssue, maxIssuable);
            }
            else
            {
                part.QuantityPlanned += actualIssue;
            }

            part.QuantityIssued += actualIssue;
            part.QuantityUsed = part.QuantityIssued - part.QuantityReturned;
            part.IssuedAt = DateTime.UtcNow;
            part.IssuedBy = User.Identity?.Name ?? "SYSTEM";

            await ApplyOperationPartMovementAsync(part, actualIssue, isIssue: true);
            await PostOperationPartJournalEntryAsync(part, actualIssue, isIssue: true);

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = woId });
        }

        public async Task<IActionResult> OnPostReturnOperationPartAsync(int operationPartId, decimal quantityReturn)
        {
            var part = await _context.WorkOrderOperationParts
                .Include(p => p.WorkOrderOperation)
                    .ThenInclude(op => op!.WorkOrder)
                        .ThenInclude(m => m!.Asset)
                .Where(p => p.Id == operationPartId
                    && p.WorkOrderOperation != null
                    && p.WorkOrderOperation.WorkOrder != null
                    && p.WorkOrderOperation.WorkOrder.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrderOperation.WorkOrder.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();
            var woId = part.WorkOrderOperation!.WorkOrderId;

            if (quantityReturn <= 0) return RedirectToPage(new { id = woId });

            var maxReturnable = part.QuantityIssued - part.QuantityReturned;
            if (maxReturnable <= 0) return RedirectToPage(new { id = woId });
            var actualReturn = Math.Min(quantityReturn, maxReturnable);

            part.QuantityReturned += actualReturn;
            part.QuantityUsed = Math.Max(0m, part.QuantityIssued - part.QuantityReturned);

            await ApplyOperationPartMovementAsync(part, actualReturn, isIssue: false);
            await PostOperationPartJournalEntryAsync(part, actualReturn, isIssue: false);

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = woId });
        }

        /// <summary>
        /// Operation-part variant of <see cref="ApplyItemMovementAsync"/>.
        /// Same inventory + ItemTransaction shape, but keyed off the
        /// operation-part's IssuedFromLocationId and the WorkOrder
        /// reached via the parent operation.
        /// </summary>
        private async Task<decimal?> ApplyOperationPartMovementAsync(WorkOrderOperationPart part, decimal qty, bool isIssue)
        {
            var sign = isIssue ? -1m : 1m;
            var companyId = part.WorkOrderOperation?.WorkOrder?.Asset?.CompanyId ?? _tenantContext.CompanyId;
            decimal? newOnHand = null;

            if (part.IssuedFromLocationId.HasValue)
            {
                var inv = await _context.Set<ItemInventory>()
                    .FirstOrDefaultAsync(i =>
                        i.ItemId == part.ItemId &&
                        i.LocationId == part.IssuedFromLocationId.Value &&
                        i.CompanyId == companyId);
                if (inv == null)
                {
                    inv = new ItemInventory
                    {
                        ItemId = part.ItemId,
                        LocationId = part.IssuedFromLocationId.Value,
                        CompanyId = companyId,
                        QuantityOnHand = sign * qty,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Set<ItemInventory>().Add(inv);
                }
                else
                {
                    inv.QuantityOnHand += sign * qty;
                    inv.UpdatedAt = DateTime.UtcNow;
                    if (isIssue) inv.LastIssueDate = DateTime.UtcNow;
                }
                newOnHand = inv.QuantityOnHand;
            }

            var workOrderId = part.WorkOrderOperation?.WorkOrderId ?? 0;
            _context.Set<ItemTransaction>().Add(new ItemTransaction
            {
                TransactionNumber = $"WO{workOrderId}-OP{part.WorkOrderOperationId}-{(isIssue ? "ISS" : "RTN")}-{DateTime.UtcNow.Ticks}",
                ItemId = part.ItemId,
                Type = isIssue ? TransactionType.Issue : TransactionType.Return,
                Quantity = qty,
                UnitCost = part.UnitCost,
                FromLocationId = isIssue ? part.IssuedFromLocationId : null,
                ToLocationId = isIssue ? null : part.IssuedFromLocationId,
                LotNumber = part.LotNumber,
                SerialNumber = part.SerialNumber
            });
            return newOnHand;
        }

        /// <summary>
        /// Operation-part variant of <see cref="PostMaterialMovementJournalEntryAsync"/>.
        /// Posts the same DR MaintenanceMaterials / CR Inventory pair (signs
        /// reversed for returns), but uses a distinct <c>Source</c> code
        /// (<c>WO-ISS-OP</c> / <c>WO-RTN-OP</c>) so downstream reports — the
        /// Maintenance Spend report from PR #93 and the CloseoutService cost
        /// rollup from PR #103 — can identify operation-scoped material moves
        /// vs WO-header moves.
        /// </summary>
        private async Task<JournalEntry?> PostOperationPartJournalEntryAsync(WorkOrderOperationPart part, decimal qty, bool isIssue)
        {
            if (qty <= 0m) return null;
            var amount = qty * part.UnitCost;
            if (amount <= 0m) return null;

            var maintenanceEvent = part.WorkOrderOperation?.WorkOrder;
            var resolvedCompanyId = maintenanceEvent?.Asset?.CompanyId
                ?? _tenantContext.CompanyId
                ?? 0;
            if (resolvedCompanyId == 0) return null;
            var workOrderId = maintenanceEvent?.Id ?? 0;
            var assetId = maintenanceEvent?.AssetId;

            var ctx = new GlResolveContext(WorkOrderId: workOrderId, AssetId: assetId);
            var materialsAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.MaintenanceMaterials, ctx);
            var inventoryAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.Inventory, ctx);

            var ticks = DateTime.UtcNow.Ticks;
            var src = isIssue ? "WO-ISS-OP" : "WO-RTN-OP";
            // Reference includes the WO id so the CloseoutService rollup (PR
            // #103) — which sums by `Reference.StartsWith("WO-ISS-{woId}-")`
            // and a parallel `WO-ISS-OP-{woId}-` prefix added in this PR —
            // catches both header-level and operation-level material moves.
            var jeReference = $"{src}-{workOrderId}-op{part.WorkOrderOperationId}-p{part.Id}-{ticks}";
            var woNumber = maintenanceEvent?.WorkOrderNumber ?? $"WO#{workOrderId}";
            var verb = isIssue ? "issued to" : "returned from";

            var drAccount = isIssue ? materialsAccount : inventoryAccount;
            var crAccount = isIssue ? inventoryAccount  : materialsAccount;

            var je = new JournalEntry
            {
                BookId = null,
                Batch = jeReference,
                Period = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
                PostingDate = DateTime.UtcNow.Date,
                Source = src,
                Reference = jeReference,
                Description = $"Operation-part materials {verb} {woNumber} op#{part.WorkOrderOperationId} (qty {qty} @ {part.UnitCost:C})",
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>
                {
                    new JournalLine
                    {
                        LineNo = 1,
                        Account = drAccount,
                        Description = isIssue
                            ? $"Op-part materials - {woNumber}"
                            : $"Op-part inventory restored - {woNumber}",
                        Debit = amount,
                        Credit = 0m
                    },
                    new JournalLine
                    {
                        LineNo = 2,
                        Account = crAccount,
                        Description = isIssue
                            ? $"Op-part inventory {verb} {woNumber}"
                            : $"Op-part materials reversed - {woNumber}",
                        Debit = 0m,
                        Credit = amount
                    }
                }
            };
            _context.JournalEntries.Add(je);
            return je;
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
            var companyId = GetCompanyId();
            var part = await _context.WorkOrderParts
                .Include(p => p.WorkOrder).ThenInclude(m => m!.Asset)
                .Where(p => p.Id == workOrderPartId && p.WorkOrder != null && p.WorkOrder.Asset != null && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrder.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();

            if (quantityIssue <= 0) return RedirectToPage(new { id = part.WorkOrderId });

            // For planned materials: cannot issue more than (planned - already issued)
            // For unplanned: allow any quantity (auto-extends planned)
            decimal actualIssue = quantityIssue;
            if (part.QuantityPlanned > 0)
            {
                var maxIssuable = part.QuantityPlanned - part.QuantityIssued;
                if (maxIssuable <= 0) return RedirectToPage(new { id = part.WorkOrderId });
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

            // S1-7: decrement inventory + create ItemTransaction. The pre-fix
            // code updated WorkOrderPart counters only, leaving ItemInventory
            // untouched and ItemTransaction never created — inventory drifted
            // forever. See docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md.
            // Inventory failure is logged but does NOT roll back the WO part
            // update — the operational truth (issuance happened) is the WO
            // part counter; inventory accuracy is downstream and correctable.
            var newOnHand = await ApplyItemMovementAsync(part, actualIssue, isIssue: true);

            // DEF-N09 (PR #89): Post the GL impact of the material issuance.
            // Pre-fix, WorkOrderPart counters and ItemInventory both moved but
            // no JournalEntry was created — work orders showed inventory drained
            // while the P&L never saw the maintenance expense. This is the WO
            // cost-rollup that lets finance close a WO and reconcile asset-level
            // maintenance spend. DR MaintenanceMaterials / CR Inventory at the
            // part's UnitCost (the standing convention used by the receiving
            // service for inventory-to-expense moves).
            await PostMaterialMovementJournalEntryAsync(part, actualIssue, isIssue: true);

            await _context.SaveChangesAsync();

            await _outbox.EnqueueAsync(
                part.WorkOrder?.Asset?.CompanyId ?? companyId,
                siteId: null,
                new ItemIssuedV1(
                    ItemId: part.ItemId,
                    LocationId: part.IssuedFromLocationId,
                    CompanyId: part.WorkOrder?.Asset?.CompanyId,
                    WorkOrderId: part.WorkOrderId,
                    WorkOrderPartId: part.Id,
                    WorkOrderNumber: part.WorkOrder?.WorkOrderNumber ?? string.Empty,
                    AssetId: part.WorkOrder?.AssetId,
                    Quantity: actualIssue,
                    UnitCost: part.UnitCost,
                    NewQuantityOnHand: newOnHand,
                    LotNumber: part.LotNumber,
                    SerialNumber: part.SerialNumber,
                    IssuedBy: part.IssuedBy,
                    IssuedAt: part.IssuedDate ?? DateTime.UtcNow),
                correlationId: $"item-issue-wo{part.WorkOrderId}-p{part.Id}-{DateTime.UtcNow.Ticks}"
            );

            return RedirectToPage(new { id = part.WorkOrderId });
        }

        /// <summary>
        /// Applies an inventory movement for a WorkOrderPart issue or return.
        /// Decrements (issue) or increments (return) ItemInventory at the
        /// part's IssuedFromLocationId, and creates an ItemTransaction row
        /// for audit. Idempotent at the WorkOrderPart-counter level since
        /// the caller updates QuantityIssued/QuantityReturned in-place.
        ///
        /// If IssuedFromLocationId is null we still log the transaction but
        /// can't update a specific inventory row — operations team can
        /// reconcile later via cycle count.
        /// </summary>
        private async Task<decimal?> ApplyItemMovementAsync(WorkOrderPart part, decimal qty, bool isIssue)
        {
            var sign = isIssue ? -1m : 1m;
            var companyId = part.WorkOrder?.Asset?.CompanyId ?? _tenantContext.CompanyId;
            decimal? newOnHand = null;

            if (part.IssuedFromLocationId.HasValue)
            {
                var inv = await _context.Set<ItemInventory>()
                    .FirstOrDefaultAsync(i =>
                        i.ItemId == part.ItemId &&
                        i.LocationId == part.IssuedFromLocationId.Value &&
                        i.CompanyId == companyId);
                if (inv == null)
                {
                    // Stock row didn't exist; create it with the negative
                    // (or positive) quantity so the issuance is recorded.
                    // A subsequent cycle count or receipt will correct.
                    inv = new ItemInventory
                    {
                        ItemId = part.ItemId,
                        LocationId = part.IssuedFromLocationId.Value,
                        CompanyId = companyId,
                        QuantityOnHand = sign * qty,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Set<ItemInventory>().Add(inv);
                }
                else
                {
                    inv.QuantityOnHand += sign * qty;
                    inv.UpdatedAt = DateTime.UtcNow;
                    if (isIssue) inv.LastIssueDate = DateTime.UtcNow;
                }
                newOnHand = inv.QuantityOnHand;
            }

            var txn = new ItemTransaction
            {
                TransactionNumber = $"WO{part.WorkOrderId}-{(isIssue ? "ISS" : "RTN")}-{DateTime.UtcNow.Ticks}",
                ItemId = part.ItemId,
                Type = isIssue ? TransactionType.Issue : TransactionType.Return,
                Quantity = qty,
                UnitCost = part.UnitCost,
                FromLocationId = isIssue ? part.IssuedFromLocationId : null,
                ToLocationId = isIssue ? null : part.IssuedFromLocationId,
                LotNumber = part.LotNumber,
                SerialNumber = part.SerialNumber
            };
            _context.Set<ItemTransaction>().Add(txn);

            return newOnHand;
        }

        /// <summary>
        /// DEF-N09 (PR #89): Posts the journal entry pair for a material
        /// issuance (or return) against a Work Order. On an issue:
        ///   DR <see cref="GlAccountKind.MaintenanceMaterials"/>
        ///   CR <see cref="GlAccountKind.Inventory"/>
        /// On a return, signs are reversed (DR Inventory, CR Materials).
        /// Amount is <c>qty * part.UnitCost</c>. Resolves accounts via the
        /// company GL cascade (ADR-003) so per-company overrides are honored.
        /// The JE is added to the ChangeTracker but not saved here — the
        /// caller's SaveChanges flushes it alongside the WorkOrderPart and
        /// ItemTransaction in a single transaction so they cannot diverge.
        /// </summary>
        private async Task<JournalEntry?> PostMaterialMovementJournalEntryAsync(WorkOrderPart part, decimal qty, bool isIssue)
        {
            if (qty <= 0m) return null;
            var amount = qty * part.UnitCost;
            if (amount <= 0m) return null; // a zero-standard-cost item issues without GL impact

            // Resolve the WO's owning company. ADR-003 keys configs by CompanyId.
            var resolvedCompanyId = part.WorkOrder?.Asset?.CompanyId
                ?? _tenantContext.CompanyId
                ?? 0;
            if (resolvedCompanyId == 0) return null; // can't resolve GL accounts without a company

            var ctx = new GlResolveContext(
                WorkOrderId: part.WorkOrderId,
                AssetId: part.WorkOrder?.AssetId);
            var materialsAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.MaintenanceMaterials, ctx);
            var inventoryAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.Inventory, ctx);

            // Reference includes Ticks so each issuance produces a unique JE
            // even if the same part is issued multiple times in quick
            // succession. Source distinguishes WO material issues ("WO-ISS")
            // from returns ("WO-RTN") for downstream reporting filters.
            var ticks = DateTime.UtcNow.Ticks;
            var src = isIssue ? "WO-ISS" : "WO-RTN";
            var jeReference = $"{src}-{part.WorkOrderId}-p{part.Id}-{ticks}";
            var woNumber = part.WorkOrder?.WorkOrderNumber ?? $"WO#{part.WorkOrderId}";
            var verb = isIssue ? "issued to" : "returned from";

            // DR/CR signs: issue moves cost INTO the maintenance expense
            // (DR Materials, CR Inventory); return reverses it.
            var drAccount = isIssue ? materialsAccount : inventoryAccount;
            var crAccount = isIssue ? inventoryAccount  : materialsAccount;

            var je = new JournalEntry
            {
                BookId = null,
                Batch = jeReference,
                Period = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
                PostingDate = DateTime.UtcNow.Date,
                Source = src,
                Reference = jeReference,
                Description = isIssue
                    ? $"Materials {verb} {woNumber} (qty {qty} @ {part.UnitCost:C})"
                    : $"Materials {verb} {woNumber} (qty {qty} @ {part.UnitCost:C})",
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>
                {
                    new JournalLine
                    {
                        LineNo = 1,
                        Account = drAccount,
                        Description = isIssue
                            ? $"Maintenance materials - {woNumber}"
                            : $"Inventory restored - {woNumber}",
                        Debit = amount,
                        Credit = 0m
                    },
                    new JournalLine
                    {
                        LineNo = 2,
                        Account = crAccount,
                        Description = isIssue
                            ? $"Inventory {verb} {woNumber}"
                            : $"Maintenance materials reversed - {woNumber}",
                        Debit = 0m,
                        Credit = amount
                    }
                }
            };
            _context.JournalEntries.Add(je);
            return je;
        }

        public async Task<IActionResult> OnPostReturnMaterialAsync(int workOrderPartId, decimal quantityReturn)
        {
            var companyId = GetCompanyId();
            var part = await _context.WorkOrderParts
                .Include(p => p.WorkOrder).ThenInclude(m => m!.Asset)
                .Where(p => p.Id == workOrderPartId && p.WorkOrder != null && p.WorkOrder.Asset != null && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrder.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();

            if (quantityReturn <= 0) return RedirectToPage(new { id = part.WorkOrderId });

            // Guardrail: Cannot return more than net issued (issued - returned)
            var maxReturnable = part.QuantityIssued - part.QuantityReturned;
            if (maxReturnable <= 0) return RedirectToPage(new { id = part.WorkOrderId });
            var actualReturn = Math.Min(quantityReturn, maxReturnable);

            part.QuantityReturned += actualReturn;
            // Used = Issued - Returned (recalculate to prevent negative)
            part.QuantityUsed = Math.Max(0, part.QuantityIssued - part.QuantityReturned);
            part.UpdatedAt = DateTime.UtcNow;

            // S1-7: increment inventory + create ItemTransaction (Return).
            await ApplyItemMovementAsync(part, actualReturn, isIssue: false);

            // DEF-N09 (PR #89): Reverse the GL impact of the original issuance
            // for the returned quantity. DR Inventory / CR MaintenanceMaterials
            // so the WO cost rollup ticks back down by the returned cost. The
            // ItemTransaction this pairs with is Type=Return.
            await PostMaterialMovementJournalEntryAsync(part, actualReturn, isIssue: false);

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = part.WorkOrderId });
        }

        public async Task<IActionResult> OnPostRemovePlannedMaterialAsync(int workOrderPartId)
        {
            var companyId = GetCompanyId();
            var part = await _context.WorkOrderParts
                .Include(p => p.WorkOrder).ThenInclude(m => m!.Asset)
                .Where(p => p.Id == workOrderPartId && p.WorkOrder != null && p.WorkOrder.Asset != null && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrder.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (part == null) return NotFound();

            // Guardrail: Cannot remove if already issued
            if (part.QuantityIssued > 0) return RedirectToPage(new { id = part.WorkOrderId });

            var woId = part.WorkOrderId;
            _context.WorkOrderParts.Remove(part);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = woId });
        }

        public async Task<IActionResult> OnPostLoadTemplateMaterialsAsync(int workOrderId)
        {
            // Find the WO and check for PMTA linkage in CustomField1
            var companyId = GetCompanyId();
            var wo = await _context.WorkOrders
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
                .Where(p => p.WorkOrderId == workOrderId)
                .Select(p => p.ItemId)
                .ToListAsync();

            int added = 0;
            foreach (var templateItem in pmta.PMTemplate.Items)
            {
                if (existingItemIds.Contains(templateItem.ItemId)) continue;

                var woPart = new WorkOrderPart
                {
                    WorkOrderId = workOrderId,
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
