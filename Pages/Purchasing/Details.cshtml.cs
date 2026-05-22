using System.Security.Claims;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Approvals;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Purchasing;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Purchasing
{
    // PR #105 / B-18: Purchasing detail page hosts the OnPostApproveAsync,
    // OnPostAddLine, OnPostDeleteLine, OnPostDeletePO etc. handlers — all of
    // which commit money or change order state. Restrict at the class level.
    //
    // Sprint 12.9 PR #4 — All 12 operational writes have been refactored to
    // IPurchasingService. AppDbContext is retained on the ctor for the
    // OnGetAsync + LoadLookupsAsync read-path projections only (per ADR-025
    // D1: "PageModels MAY read directly via AppDbContext for thin display
    // projections"). The ControlPlaneExempt attribute documents this for
    // the CHERRY025 analyzer + code review.
    [Authorize(Roles = "Admin,Accountant")]
    [ControlPlaneExempt("Sprint 12.9 PR #4 — all 12 operational writes refactored to IPurchasingService; AppDbContext retained for read-path projections only (ADR-025 D1)")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IPurchasingService _purchasing;

        public DetailsModel(AppDbContext context, IModuleGuardService moduleGuard, ILookupService lookupService,
            ITenantContext tenantContext, IPurchasingService purchasing)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
            _purchasing = purchasing;
        }

        // Sprint 2 PR #115: surface the current approval status to the view.
        public ApprovalDecisionResult? ApprovalStatus { get; set; }

        public PurchaseOrder PurchaseOrder { get; set; } = null!;
        public List<Vendor> Vendors { get; set; } = new();
        public List<Location> Locations { get; set; } = new();
        public List<Item> Items { get; set; } = new();
        public List<GlAccount> GlAccounts { get; set; } = new();
        public List<SelectListItem> POTypeOptions { get; set; } = new();
        // S2-11: tenant-scoped CIP project picker for the PO header.
        public List<Models.CipProject> CipProjects { get; set; } = new();
        public int? ConsumablesGlAccountId { get; set; }
        public int? SelectedLineId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int? lineId = null)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Purchasing" });

            var po = await _context.PurchaseOrders
                .Include(p => p.Vendor)
                .Include(p => p.Lines)
                    .ThenInclude(l => l.Item)
                .Include(p => p.Lines)
                    .ThenInclude(l => l.Releases)
                        .ThenInclude(r => r.ShipToLocation)
                .Where(p => p.Id == Id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (po == null)
                return NotFound();

            PurchaseOrder = po;
            SelectedLineId = lineId ?? po.Lines.FirstOrDefault()?.Id;
            await LoadLookupsAsync();

            ViewData["ReturnUrl"] = ReturnUrl;
            ViewData["Breadcrumbs"] = new List<(string Label, string Href)>
            {
                ("Materials", "/Purchasing"),
                ("Purchasing", "/Purchasing"),
                ("PO Detail", "")
            };
            ViewData["ShowBackLink"] = true;
            ViewData["BackLinkFallback"] = "/Purchasing";
            ViewData["BackLinkLabel"] = "Back to results";

            return Page();
        }

        // Sprint 12.9 PR #4 — all 11 handlers below delegate to IPurchasingService.
        // Each preserves bit-identical TempData mapping + RedirectToPage URL shape
        // so the legacy UI/integration surface is unchanged.

        public async Task<IActionResult> OnPostUpdateHeaderAsync(int id, int vendorId, int poTypeLookupValueId, DateTime orderDate, DateTime? requiredDate, string? notes, int? cipProjectId)
        {
            var result = await _purchasing.UpdateHeaderAsync(
                new UpdatePoHeaderRequest(id, vendorId, poTypeLookupValueId, orderDate, requiredDate, notes, cipProjectId),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                TempData["ErrorMessage"] = result.Error;
            }
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddLineAsync(int id, int? itemId, string description, string? partNumber,
            string? mfrPartNumber, string? vendorPartNumber, string? revision, string uom, decimal quantity,
            decimal unitPrice, int? glAccountId, string? notes)
        {
            var result = await _purchasing.AddLineAsync(
                new AddPoLineRequest(id, itemId, description, partNumber, mfrPartNumber, vendorPartNumber,
                    revision, uom, quantity, unitPrice, glAccountId, notes),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToPage(new { id });
            }
            return RedirectToPage(new { id, lineId = result.Value!.Id });
        }

        public async Task<IActionResult> OnPostDeleteLineAsync(int id, int lineId)
        {
            var result = await _purchasing.DeleteLineAsync(
                new DeletePoLineRequest(id, lineId),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                TempData["ErrorMessage"] = result.Error;
            }
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddReleaseAsync(int id, int lineId, decimal quantity, int? shipToLocationId, DateTime? dueDate, string? notes)
        {
            var result = await _purchasing.AddReleaseAsync(
                new AddPoReleaseRequest(id, lineId, quantity, shipToLocationId, dueDate, notes),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                // Quantity-exceeds-remaining and Draft-only failures both use the
                // generic Error key; status-state failures use ErrorMessage.
                TempData[result.Error?.StartsWith("Release quantity") == true ? "Error" : "ErrorMessage"] = result.Error;
            }
            return RedirectToPage(new { id, lineId });
        }

        public async Task<IActionResult> OnPostDeleteReleaseAsync(int id, int releaseId, int lineId)
        {
            var result = await _purchasing.DeleteReleaseAsync(
                new DeletePoReleaseRequest(id, lineId, releaseId),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                TempData["ErrorMessage"] = result.Error;
            }
            return RedirectToPage(new { id, lineId });
        }

        public async Task<IActionResult> OnPostSubmitForApprovalAsync(int id)
        {
            var result = await _purchasing.SubmitForApprovalAsync(id, HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string? approvalComment = null)
        {
            var approverUsername = User.Identity?.Name ?? "unknown";
            var approverRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            var result = await _purchasing.ApproveAsync(
                new ApprovePoRequest(id, approverUsername, approverRoles, approvalComment),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToPage(new { id });
            }

            var outcome = result.Value!;
            switch (outcome.Status)
            {
                case ApprovePoStatus.Approved:
                case ApprovePoStatus.PartiallyApproved:
                    TempData["StatusMessage"] = outcome.Message;
                    break;
                case ApprovePoStatus.Rejected:
                    TempData["ErrorMessage"] = outcome.Message;
                    break;
            }
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string? rejectComment = null)
        {
            var approverUsername = User.Identity?.Name ?? "unknown";
            var approverRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            var result = await _purchasing.RejectAsync(
                new RejectPoRequest(id, approverUsername, approverRoles, rejectComment),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToPage(new { id });
            }

            var outcome = result.Value!;
            switch (outcome.Status)
            {
                case RejectPoStatus.Rejected:
                    TempData["StatusMessage"] = outcome.Message;
                    break;
                case RejectPoStatus.Blocked:
                    TempData["ErrorMessage"] = outcome.Message;
                    break;
            }
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostUpdateLineAsync(int id, int lineId, decimal quantity, decimal unitPrice)
        {
            var result = await _purchasing.UpdateLineAsync(
                new UpdatePoLineRequest(id, lineId, quantity, unitPrice),
                HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                TempData["ErrorMessage"] = result.Error;
            }
            return RedirectToPage(new { id, lineId });
        }

        public async Task<IActionResult> OnPostDuplicatePOAsync(int id)
        {
            var result = await _purchasing.DuplicatePoAsync(id, HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            return RedirectToPage(new { id = result.Value!.Id });
        }

        public async Task<IActionResult> OnPostDeletePOAsync(int id)
        {
            var result = await _purchasing.DeletePoAsync(id, HttpContext.RequestAborted);
            if (result.IsFailure) return NotFound();
            if (result.Value)
            {
                return RedirectToPage("/Purchasing/Index");
            }
            return RedirectToPage(new { id });
        }

        private async Task LoadLookupsAsync()
        {
            Vendors = await _context.Vendors
                .Where(v => v.IsActive && (v.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0)))
                .OrderBy(v => v.Name)
                .ToListAsync();

            Locations = await _context.Locations
                .Where(l => l.IsActive && (l.SiteId == null || _context.Sites.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId)).Select(s => s.Id).Contains(l.SiteId ?? 0)))
                .OrderBy(l => l.Name)
                .ToListAsync();

            Items = await _context.Items
                .Where(i => i.IsActive && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0))
                .OrderBy(i => i.PartNumber)
                .Take(500)
                .ToListAsync();

            // S2-11: load active CIP projects for the header picker. Tenant-scoped.
            CipProjects = await _context.CipProjects
                .Where(p => p.Status == CipProjectStatus.Active
                    && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .OrderBy(p => p.ProjectNumber)
                .ToListAsync();

            var allGlAccounts = await _context.GlAccounts
                .Where(g => g.IsActive && (g.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(g.CompanyId ?? 0)))
                .OrderBy(g => g.AccountNumber)
                .ToListAsync();

            // DEF-N11 (PR #87): filter the PO-line GL Account dropdown to
            // accounts that can plausibly hold an inbound goods/services
            // cost. The unfiltered list let users pick "1000 — CASH" or any
            // liability/equity/revenue account on a PO line, which has no
            // legitimate accounting interpretation — the resulting GR JE
            // would credit GR-Accrued and debit Cash (i.e., undo a cash
            // receipt), corrupting the trial balance.
            //
            // Allowed for PO lines (industrial baseline: SAP MM uses an
            // account-determination strategy that restricts to specific
            // account groups; Oracle EBS uses Account Generator rules;
            // Maximo restricts to the GL_DEBIT validation hook). The
            // equivalent surgical filter here:
            //   • Any Expense account (Cost of Sales, Maintenance, OpEx).
            //   • Asset accounts in the inventory / WIP / fixed-asset /
            //     CIP categories (capitalisable acquisitions).
            // Excluded: cash & receivables, prepaids, accumulated
            // depreciation, intercompany, all liabilities/equity/revenue,
            // and contra-* accounts.
            static bool IsPoLineEligible(GlAccount g)
            {
                if (g.AccountType == GlAccountType.Expense) return true;
                if (g.AccountType == GlAccountType.Asset)
                {
                    return g.Category == GlAccountCategory.MroInventory
                        || g.Category == GlAccountCategory.WorkInProgress
                        || g.Category == GlAccountCategory.FixedAssetsLandBuildings
                        || g.Category == GlAccountCategory.FixedAssetsMachinery
                        || g.Category == GlAccountCategory.FixedAssetsVehicles
                        || g.Category == GlAccountCategory.FixedAssetsTechnology
                        || g.Category == GlAccountCategory.FixedAssetsTooling;
                }
                return false;
            }

            GlAccounts = allGlAccounts
                .Where(IsPoLineEligible)
                .GroupBy(g => g.AccountNumber)
                .Select(g => g.First())
                .ToList();

            ConsumablesGlAccountId = allGlAccounts
                .FirstOrDefault(g => g.AccountNumber == "6220" || g.Name.Contains("Consumables"))?.Id;

            POTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "PurchaseOrderType", PurchaseOrder?.POTypeLookupValueId, "");
        }
    }
}
