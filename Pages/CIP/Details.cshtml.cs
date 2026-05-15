using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Cip;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CIP
{
    public class DetailsModel : PageModel
    {
        private readonly CipService _cipService;
        private readonly CipCostService _cipCostService;
        private readonly CipCapitalizationService _cipCapService;
        private readonly CipTraceQueryService _traceService;
        private readonly AttachmentService _attachmentService;
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public DetailsModel(CipService cipService,
            CipCostService cipCostService,
            CipCapitalizationService cipCapService,
            CipTraceQueryService traceService,
            AttachmentService attachmentService,
            AppDbContext context,
            ILookupService lookupService,
            ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _cipService = cipService;
            _cipCostService = cipCostService;
            _cipCapService = cipCapService;
            _traceService = traceService;
            _attachmentService = attachmentService;
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public CipProject Project { get; set; } = null!;
        public List<CipCost> Costs { get; set; } = new();
        public List<Attachment> Attachments { get; set; } = new();
        public List<ProjectManager> ProjectManagers { get; set; } = new();
        public List<Location> Locations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<GlAccount> GlAccounts { get; set; } = new();
        public List<SelectListItem> CipCostTypeOptions { get; set; } = new();
        public List<SelectListItem> CipStatusOptions { get; set; } = new();
        public decimal PercentComplete { get; set; }
        public int? ActiveStatusLvId { get; set; }
        public int? OnHoldStatusLvId { get; set; }
        public int? CompletedStatusLvId { get; set; }

        public CipCostSummary CostSummary { get; set; } = new();
        public List<MaintenanceEvent> RelatedWorkOrders { get; set; } = new();
        public List<PurchaseOrder> RelatedPurchaseOrders { get; set; } = new();
        public List<VendorInvoice> RelatedInvoices { get; set; } = new();
        public List<JournalEntry> RelatedJournals { get; set; } = new();
        public List<Asset> RelatedAssets { get; set; } = new();
        public CipCapitalizationPreview? CapPreview { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("projects"))
                return RedirectToPage("/ModuleDisabled", new { module = "CIP" });

            var project = await _cipService.GetProjectAsync(id);
            if (project == null)
                return NotFound();

            if (_tenantContext.SiteId.HasValue && project.SiteId != _tenantContext.SiteId.Value)
                return NotFound();

            Project = project;
            Costs = await _cipService.GetProjectCostsAsync(id);
            Attachments = await _attachmentService.GetByCipProjectAsync(id);
            var visibleIds = _tenantContext.VisibleCompanyIds;

            ProjectManagers = await _context.ProjectManagers.Where(pm => pm.Active).OrderBy(pm => pm.Name).ToListAsync();
            Locations = await _context.Locations.Where(l => l.CompanyId == null || visibleIds.Contains(l.CompanyId ?? 0)).OrderBy(l => l.Name).ToListAsync();
            Departments = await _context.Departments.Where(d => d.CompanyId == null || visibleIds.Contains(d.CompanyId ?? 0)).OrderBy(d => d.Name).ToListAsync();
            GlAccounts = await _context.GlAccounts.Where(g => g.CompanyId == null || visibleIds.Contains(g.CompanyId ?? 0)).OrderBy(g => g.AccountNumber).ToListAsync();

            CostSummary = await _cipCostService.ComputeTotalsAsync(id);

            PercentComplete = Project.BudgetAmount > 0 
                ? (CostSummary.TotalSpent / Project.BudgetAmount * 100) 
                : 0;

            CipCostTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipCostType", null, "");
            CipStatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipProjectStatus", Project.StatusLookupValueId, "");

            var statusValues = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipProjectStatus");
            ActiveStatusLvId = statusValues.FirstOrDefault(v => v.Name.Equals("Active", StringComparison.OrdinalIgnoreCase))?.Id;
            OnHoldStatusLvId = statusValues.FirstOrDefault(v => v.Name.Equals("On Hold", StringComparison.OrdinalIgnoreCase))?.Id;
            CompletedStatusLvId = statusValues.FirstOrDefault(v => v.Name.Equals("Completed", StringComparison.OrdinalIgnoreCase))?.Id;

            RelatedWorkOrders = await _traceService.GetRelatedWorkOrdersAsync(id);
            RelatedPurchaseOrders = await _traceService.GetRelatedPurchaseOrdersAsync(id);
            RelatedInvoices = await _traceService.GetRelatedVendorInvoicesAsync(id);
            RelatedJournals = await _traceService.GetRelatedJournalsAsync(id);
            RelatedAssets = await _traceService.GetAssetLinksAsync(id);

            if (Project.Status == CipProjectStatus.Completed && !Project.IsCapitalized)
                CapPreview = await _cipCapService.PreviewAsync(id);

            return Page();
        }

        public async Task<IActionResult> OnPostAddCostAsync(
            int id,
            int costTypeLookupValueId,
            string description,
            decimal amount,
            string? vendor,
            string? invoiceNumber,
            DateTime transactionDate,
            bool isCapitalizable)
        {
            int? resolvedCostTypeLvId = null;
            var resolvedCostType = CipCostType.Other;
            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, costTypeLookupValueId);
            if (lv != null)
            {
                resolvedCostTypeLvId = lv.Id;
                if (System.Enum.TryParse<CipCostType>(lv.Code, true, out var parsed))
                    resolvedCostType = parsed;
                else if (int.TryParse(lv.Code, out var enumVal) && System.Enum.IsDefined(typeof(CipCostType), enumVal))
                    resolvedCostType = (CipCostType)enumVal;
            }

            var cost = new CipCost
            {
                CipProjectId = id,
                CostType = resolvedCostType,
                CostTypeLookupValueId = resolvedCostTypeLvId,
                Description = description,
                Amount = amount,
                Vendor = vendor,
                InvoiceNumber = invoiceNumber,
                TransactionDate = DateTime.SpecifyKind(transactionDate, DateTimeKind.Utc),
                IsCapitalizable = isCapitalizable,
                SourceType = "Manual",
                SourceDisplayRef = "Manual entry",
                EnteredBy = User.Identity?.Name ?? "system",
                CreatedByUserId = User.Identity?.Name ?? "system"
            };

            await _cipService.AddCostAsync(cost);
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, int statusLookupValueId)
        {
            var project = await _cipService.GetProjectAsync(id);
            if (project == null)
                return NotFound();
            if (_tenantContext.SiteId.HasValue && project.SiteId != _tenantContext.SiteId.Value)
                return NotFound();

            var statusLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, statusLookupValueId);
            if (statusLv != null)
            {
                project.StatusLookupValueId = statusLv.Id;
                if (Enum.TryParse<CipProjectStatus>(statusLv.Name.Replace(" ", ""), true, out var parsed))
                    project.Status = parsed;
            }
            else
            {
                project.Status = (CipProjectStatus)statusLookupValueId;
            }

            if (project.Status == CipProjectStatus.Completed)
                project.ActualCompletionDate = DateTime.UtcNow;

            await _cipService.UpdateProjectAsync(project);
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCapitalizeAsync(
            int id,
            string assetNumber,
            string assetDescription,
            int usefulLifeMonths,
            decimal salvageValue = 0m,
            int? assetCategoryId = null)
        {
            try
            {
                var (asset, cap) = await _cipCapService.CapitalizeAsync(
                    id,
                    assetNumber,
                    assetDescription,
                    usefulLifeMonths: usefulLifeMonths,
                    salvageValue: salvageValue,
                    assetCategoryId: assetCategoryId,
                    userId: User.Identity?.Name ?? "system");

                return RedirectToPage(new { id });
            }
            catch (InvalidOperationException)
            {
                return RedirectToPage(new { id });
            }
            catch (ArgumentException ex)
            {
                TempData["Warning"] = ex.Message;
                return RedirectToPage(new { id });
            }
        }

        public async Task<IActionResult> OnPostEditProjectAsync(
            int id,
            string projectNumber,
            string name,
            string? description,
            int? projectManagerId,
            string? location,
            string? department,
            DateTime startDate,
            DateTime? estimatedCompletionDate,
            DateTime? actualCompletionDate,
            decimal budgetAmount,
            string? glAccount)
        {
            var project = await _cipService.GetProjectAsync(id);
            if (project == null)
                return NotFound();
            if (_tenantContext.SiteId.HasValue && project.SiteId != _tenantContext.SiteId.Value)
                return NotFound();

            project.ProjectNumber = projectNumber;
            project.Name = name;
            project.Description = description;
            project.ProjectManagerId = projectManagerId;
            project.Location = location;
            project.Department = department;
            project.StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            project.EstimatedCompletionDate = estimatedCompletionDate.HasValue 
                ? DateTime.SpecifyKind(estimatedCompletionDate.Value, DateTimeKind.Utc) 
                : null;
            project.ActualCompletionDate = actualCompletionDate.HasValue 
                ? DateTime.SpecifyKind(actualCompletionDate.Value, DateTimeKind.Utc) 
                : null;
            project.BudgetAmount = budgetAmount;
            project.GlAccount = glAccount;
            project.UpdatedAt = DateTime.UtcNow;

            await _cipService.UpdateProjectAsync(project);
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

            var project = await _cipService.GetProjectAsync(id);
            if (project == null)
            {
                return RedirectToPage(new { id });
            }
            if (_tenantContext.SiteId.HasValue && project.SiteId != _tenantContext.SiteId.Value)
                return RedirectToPage(new { id });

            var assetId = project.ConvertedAssetId;

            using var stream = file.OpenReadStream();
            await _attachmentService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                assetId,
                AttachmentSource.CipProject,
                id,
                (AttachmentCategory)category,
                description,
                User.Identity?.Name
            );

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteAttachmentAsync(int id, int attachmentId)
        {
            var projectScopeIds = _tenantContext.VisibleCompanyIds;
            var projectExists = await _context.CipProjects.AnyAsync(p => p.Id == id && projectScopeIds.Contains(p.CompanyId ?? 0)
                && (!_tenantContext.SiteId.HasValue || p.SiteId == _tenantContext.SiteId.Value));
            if (!projectExists)
                return NotFound();

            var attachment = await _context.Attachments
                .Where(a => a.Id == attachmentId && a.CipProjectId == id)
                .FirstOrDefaultAsync();
            if (attachment == null)
                return RedirectToPage(new { id });

            await _attachmentService.DeleteAsync(attachmentId);
            return RedirectToPage(new { id });
        }
    }
}
