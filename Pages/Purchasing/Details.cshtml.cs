using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Purchasing
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IOutboxWriter _outbox;

        public DetailsModel(AppDbContext context, IModuleGuardService moduleGuard, ILookupService lookupService,
            ITenantContext tenantContext, IOutboxWriter outbox)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
            _outbox = outbox;
        }

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

        public async Task<IActionResult> OnPostUpdateHeaderAsync(int id, int vendorId, int poTypeLookupValueId, DateTime orderDate, DateTime? requiredDate, string? notes, int? cipProjectId)
        {
            var po = await _context.PurchaseOrders
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (po == null) return NotFound();

            if (po.Status != POStatus.Draft)
            {
                TempData["ErrorMessage"] = "Only Draft purchase orders can be edited.";
                return RedirectToPage(new { id });
            }

            po.VendorId = vendorId;

            if (poTypeLookupValueId > 0)
            {
                var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, poTypeLookupValueId);
                if (lv != null)
                {
                    po.POTypeLookupValueId = lv.Id;
                    if (int.TryParse(lv.Code, out var enumVal))
                        po.POType = (POType)enumVal;
                }
            }

            po.OrderDate = orderDate;
            po.RequiredDate = requiredDate;
            po.Notes = notes;

            // S2-11: PO header may link to a CIP project so PO-line cost
            // routing (S1-3 wiring) flows through CipAutoCostPostingService
            // when the receipt + invoice arrive. Tenant-scope the lookup so
            // a foreign CipProjectId can't be attached.
            if (cipProjectId.HasValue && cipProjectId.Value > 0)
            {
                var validProject = await _context.CipProjects
                    .AnyAsync(p => p.Id == cipProjectId.Value
                        && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0));
                po.CipProjectId = validProject ? cipProjectId : null;
            }
            else
            {
                po.CipProjectId = null;
            }

            po.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddLineAsync(int id, int? itemId, string description, string? partNumber, 
            string? mfrPartNumber, string? vendorPartNumber, string? revision, string uom, decimal quantity, 
            decimal unitPrice, int? glAccountId, string? notes)
        {
            var po = await _context.PurchaseOrders.Include(p => p.Lines)
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (po == null) return NotFound();

            if (po.Status != POStatus.Draft)
            {
                TempData["ErrorMessage"] = "Only Draft purchase orders can be modified.";
                return RedirectToPage(new { id });
            }

            var lineNumber = po.Lines.Any() ? po.Lines.Max(l => l.LineNumber) + 1 : 1;
            var lineTotal = quantity * unitPrice;

            var line = new PurchaseOrderLine
            {
                PurchaseOrderId = id,
                LineNumber = lineNumber,
                ItemId = itemId,
                IsNonItemMaster = itemId == null,
                Description = description,
                PartNumber = partNumber,
                ManufacturerPartNumber = mfrPartNumber,
                VendorPartNumber = vendorPartNumber,
                Revision = revision,
                UOM = uom,
                QuantityOrdered = quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
                GlAccountId = glAccountId,
                Notes = notes
            };

            po.Lines.Add(line);
            
            po.Subtotal = po.Lines.Sum(l => l.LineTotal);
            po.Total = po.Subtotal + po.TaxAmount + po.ShippingAmount;
            po.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id, lineId = line.Id });
        }

        public async Task<IActionResult> OnPostDeleteLineAsync(int id, int lineId)
        {
            var line = await _context.PurchaseOrderLines
                .Include(l => l.Releases)
                .Include(l => l.PurchaseOrder)
                .Where(l => l.Id == lineId && _tenantContext.VisibleCompanyIds.Contains(l.PurchaseOrder.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (line == null) return NotFound();

            if (line.PurchaseOrder.Status != POStatus.Draft)
            {
                TempData["ErrorMessage"] = "Only Draft purchase orders can be modified.";
                return RedirectToPage(new { id });
            }

            _context.PurchaseOrderReleases.RemoveRange(line.Releases);
            _context.PurchaseOrderLines.Remove(line);

            var po = await _context.PurchaseOrders.Include(p => p.Lines)
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (po != null)
            {
                po.Subtotal = po.Lines.Where(l => l.Id != lineId).Sum(l => l.LineTotal);
                po.Total = po.Subtotal + po.TaxAmount + po.ShippingAmount;
                po.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddReleaseAsync(int id, int lineId, decimal quantity, int? shipToLocationId, DateTime? dueDate, string? notes)
        {
            var line = await _context.PurchaseOrderLines
                .Include(l => l.Releases)
                .Include(l => l.PurchaseOrder)
                .Where(l => l.Id == lineId && _tenantContext.VisibleCompanyIds.Contains(l.PurchaseOrder.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (line == null) return NotFound();

            if (line.PurchaseOrder.Status != POStatus.Draft)
            {
                TempData["ErrorMessage"] = "Only Draft purchase orders can be modified.";
                return RedirectToPage(new { id, lineId });
            }

            var currentReleaseQty = line.Releases.Sum(r => r.Quantity);
            if (currentReleaseQty + quantity > line.QuantityOrdered)
            {
                TempData["Error"] = $"Release quantity ({quantity:N0}) exceeds remaining ({line.QuantityOrdered - currentReleaseQty:N0})";
                return RedirectToPage(new { id, lineId });
            }

            var releaseNumber = line.Releases.Any() ? line.Releases.Max(r => r.ReleaseNumber) + 1 : 1;

            var release = new PurchaseOrderRelease
            {
                PurchaseOrderLineId = lineId,
                ReleaseNumber = releaseNumber,
                Quantity = quantity,
                ShipToLocationId = shipToLocationId,
                DueDate = dueDate,
                Notes = notes
            };

            _context.PurchaseOrderReleases.Add(release);
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id, lineId });
        }

        public async Task<IActionResult> OnPostDeleteReleaseAsync(int id, int releaseId, int lineId)
        {
            var release = await _context.PurchaseOrderReleases
                .Include(r => r.PurchaseOrderLine)
                    .ThenInclude(l => l.PurchaseOrder)
                .Where(r => r.Id == releaseId && _tenantContext.VisibleCompanyIds.Contains(r.PurchaseOrderLine.PurchaseOrder.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (release != null)
            {
                if (release.PurchaseOrderLine.PurchaseOrder.Status != POStatus.Draft)
                {
                    TempData["ErrorMessage"] = "Only Draft purchase orders can be modified.";
                    return RedirectToPage(new { id, lineId });
                }

                _context.PurchaseOrderReleases.Remove(release);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { id, lineId });
        }

        private async Task SyncStatusFkAsync(PurchaseOrder po, POStatus status)
        {
            po.Status = status;
            var lv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)status).ToString());
            if (lv != null)
                po.StatusLookupValueId = lv.Id;
        }

        public async Task<IActionResult> OnPostSubmitForApprovalAsync(int id)
        {
            var po = await _context.PurchaseOrders
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (po == null) return NotFound();

            if (po.Status == POStatus.Draft)
            {
                await SyncStatusFkAsync(po, POStatus.PendingApproval);
                po.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var po = await _context.PurchaseOrders
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (po == null) return NotFound();

            if (po.Status == POStatus.PendingApproval)
            {
                await SyncStatusFkAsync(po, POStatus.Approved);
                po.ApprovedAt = DateTime.UtcNow;
                po.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _outbox.EnqueueAsync(
                    po.CompanyId ?? 0,
                    siteId: po.ShipToSiteId,
                    new PoApprovedV1(
                        PurchaseOrderId: po.Id,
                        PoNumber: po.PONumber,
                        VendorId: po.VendorId,
                        CompanyId: po.CompanyId,
                        CipProjectId: po.CipProjectId,
                        Total: po.Total,
                        OrderDate: po.OrderDate,
                        RequiredDate: po.RequiredDate,
                        ApprovedAt: po.ApprovedAt!.Value,
                        ApproverUsername: User.Identity?.Name),
                    correlationId: $"po-approve-{po.Id}"
                );
            }

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostUpdateLineAsync(int id, int lineId, decimal quantity, decimal unitPrice)
        {
            var line = await _context.PurchaseOrderLines
                .Include(l => l.PurchaseOrder)
                .Where(l => l.Id == lineId && _tenantContext.VisibleCompanyIds.Contains(l.PurchaseOrder.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (line == null) return NotFound();

            if (line.PurchaseOrder.Status != POStatus.Draft)
            {
                TempData["ErrorMessage"] = "Only Draft purchase orders can be modified.";
                return RedirectToPage(new { id, lineId });
            }

            line.QuantityOrdered = quantity;
            line.UnitPrice = unitPrice;
            line.LineTotal = quantity * unitPrice;

            var po = await _context.PurchaseOrders.Include(p => p.Lines)
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (po != null)
            {
                po.Subtotal = po.Lines.Sum(l => l.LineTotal);
                po.Total = po.Subtotal + po.TaxAmount + po.ShippingAmount;
                po.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id, lineId });
        }

        public async Task<IActionResult> OnPostDuplicatePOAsync(int id)
        {
            var source = await _context.PurchaseOrders
                .Include(p => p.Lines)
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            
            if (source == null) return NotFound();

            var lastPO = await _context.PurchaseOrders
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            var nextNum = 1;
            if (lastPO != null && lastPO.PONumber.Contains("-"))
            {
                var parts = lastPO.PONumber.Split('-');
                if (parts.Length >= 2 && int.TryParse(parts[^1], out var num))
                    nextNum = num + 1;
            }
            var newPONumber = $"PO-{DateTime.Now:yy}-{nextNum:D5}";

            var draftLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)POStatus.Draft).ToString());

            var newPO = new PurchaseOrder
            {
                PONumber = newPONumber,
                POType = source.POType,
                POTypeLookupValueId = source.POTypeLookupValueId,
                Status = POStatus.Draft,
                StatusLookupValueId = draftLv?.Id,
                VendorId = source.VendorId,
                OrderDate = DateTime.Today,
                RequiredDate = source.RequiredDate,
                Currency = source.Currency,
                Notes = $"Duplicated from {source.PONumber}",
                CompanyId = source.CompanyId
            };

            _context.PurchaseOrders.Add(newPO);
            await _context.SaveChangesAsync();

            foreach (var srcLine in source.Lines)
            {
                var newLine = new PurchaseOrderLine
                {
                    PurchaseOrderId = newPO.Id,
                    LineNumber = srcLine.LineNumber,
                    ItemId = srcLine.ItemId,
                    IsNonItemMaster = srcLine.IsNonItemMaster,
                    Description = srcLine.Description,
                    PartNumber = srcLine.PartNumber,
                    ManufacturerPartNumber = srcLine.ManufacturerPartNumber,
                    VendorPartNumber = srcLine.VendorPartNumber,
                    Revision = srcLine.Revision,
                    UOM = srcLine.UOM,
                    QuantityOrdered = srcLine.QuantityOrdered,
                    UnitPrice = srcLine.UnitPrice,
                    LineTotal = srcLine.LineTotal,
                    GlAccountId = srcLine.GlAccountId,
                    CostCenterId = srcLine.CostCenterId
                };
                _context.PurchaseOrderLines.Add(newLine);
            }

            newPO.Subtotal = source.Subtotal;
            newPO.Total = source.Total;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id = newPO.Id });
        }

        public async Task<IActionResult> OnPostDeletePOAsync(int id)
        {
            var po = await _context.PurchaseOrders
                .Include(p => p.Lines)
                    .ThenInclude(l => l.Releases)
                .Where(p => p.Id == id && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            
            if (po == null) return NotFound();

            if (po.Status == POStatus.Draft)
            {
                foreach (var line in po.Lines)
                {
                    _context.PurchaseOrderReleases.RemoveRange(line.Releases);
                }
                _context.PurchaseOrderLines.RemoveRange(po.Lines);
                _context.PurchaseOrders.Remove(po);
                await _context.SaveChangesAsync();
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
            
            GlAccounts = allGlAccounts
                .GroupBy(g => g.AccountNumber)
                .Select(g => g.First())
                .ToList();

            ConsumablesGlAccountId = allGlAccounts
                .FirstOrDefault(g => g.AccountNumber == "6220" || g.Name.Contains("Consumables"))?.Id;

            POTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "PurchaseOrderType", PurchaseOrder?.POTypeLookupValueId, "");
        }
    }
}
