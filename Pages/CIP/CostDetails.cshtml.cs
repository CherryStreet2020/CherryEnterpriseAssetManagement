using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.CIP
{
    public class CostDetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;
        public CostDetailsModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public CipProject Project { get; set; } = null!;
        public CipCost Cost { get; set; } = null!;
        public bool IsEditing { get; set; }
        public List<SelectListItem> CostTypeOptions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int projectId, int costId, bool edit = false)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("projects"))
                return RedirectToPage("/ModuleDisabled", new { module = "CIP" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var project = await _db.CipProjects
                .Where(p => p.Id == projectId && visibleIds.Contains(p.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || p.SiteId == _tenantContext.SiteId.Value))
                .FirstOrDefaultAsync();
            if (project == null) return NotFound();

            var cost = await _db.CipCosts.Where(c => c.Id == costId && c.CipProjectId == projectId).OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (cost == null) return NotFound();

            Project = project;
            Cost = cost;
            IsEditing = edit;

            ViewData["ReturnUrl"] = ReturnUrl;
            ViewData["Breadcrumbs"] = new List<(string Label, string Href)>
            {
                ("Projects", "/CIP"),
                ("CIP", "/CIP"),
                ("Cost Details", "")
            };
            ViewData["ShowBackLink"] = true;
            ViewData["BackLinkFallback"] = "/CIP";
            ViewData["BackLinkLabel"] = "Back to results";

            CostTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, null, "CipCostType", Cost.CostTypeLookupValueId, "");

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int projectId, int costId,
            int costTypeLookupValueId, DateTime transactionDate, string description, decimal amount,
            string? vendor, string? invoiceNumber, string? purchaseOrderNumber, string? glAccount,
            bool isCapitalizable = false)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var projectExists = await _db.CipProjects.AnyAsync(p => p.Id == projectId && visibleIds.Contains(p.CompanyId ?? 0)
                && (!_tenantContext.SiteId.HasValue || p.SiteId == _tenantContext.SiteId.Value));
            if (!projectExists) return NotFound();

            var cost = await _db.CipCosts.Where(c => c.Id == costId && c.CipProjectId == projectId).OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (cost == null) return NotFound();

            var oldAmount = cost.Amount;

            var resolvedCostTypeLvId = costTypeLookupValueId > 0 ? costTypeLookupValueId : (int?)null;
            var resolvedCostType = CipCostType.Materials;
            var costTypeLv = await _lookupService.GetValueByIdAsync(null, null, costTypeLookupValueId);
            if (costTypeLv != null)
            {
                resolvedCostTypeLvId = costTypeLv.Id;
                if (Enum.TryParse<CipCostType>(costTypeLv.Code, true, out var parsed))
                    resolvedCostType = parsed;
            }

            cost.CostType = resolvedCostType;
            cost.CostTypeLookupValueId = resolvedCostTypeLvId;
            cost.TransactionDate = transactionDate;
            cost.Description = description;
            cost.Amount = amount;
            cost.Vendor = vendor;
            cost.InvoiceNumber = invoiceNumber;
            cost.PurchaseOrderNumber = purchaseOrderNumber;
            cost.GlAccount = glAccount;
            cost.IsCapitalizable = isCapitalizable;

            var project = await _db.CipProjects.Include(p => p.Costs).FirstOrDefaultAsync(p => p.Id == projectId);
            if (project != null)
            {
                project.TotalCosts = project.Costs?.Sum(c => c.Amount) ?? 0m;
            }

            await _db.SaveChangesAsync();

            return RedirectToPage("/CIP/CostDetails", new { projectId, costId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int projectId, int costId)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var projectExists = await _db.CipProjects.AnyAsync(p => p.Id == projectId && visibleIds.Contains(p.CompanyId ?? 0)
                && (!_tenantContext.SiteId.HasValue || p.SiteId == _tenantContext.SiteId.Value));
            if (!projectExists) return NotFound();

            var cost = await _db.CipCosts.Where(c => c.Id == costId && c.CipProjectId == projectId).OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (cost == null) return NotFound();

            _db.CipCosts.Remove(cost);

            var project = await _db.CipProjects.Include(p => p.Costs).FirstOrDefaultAsync(p => p.Id == projectId);
            if (project != null)
            {
                project.TotalCosts = project.Costs?.Where(c => c.Id != costId).Sum(c => c.Amount) ?? 0m;
            }
            await _db.SaveChangesAsync();

            return RedirectToPage("/CIP/Details", new { id = projectId });
        }
    }
}
