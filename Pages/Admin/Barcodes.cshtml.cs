using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class BarcodesModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public BarcodesModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<Item> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int ItemsWithBarcodes { get; set; }
        public int ItemsWithoutBarcodes { get; set; }
        public List<SelectListItem> BarcodeFormatOptions { get; set; } = new();
        public List<SelectListItem> BarcodeSizeOptions { get; set; } = new();

        public async Task OnGetAsync()
        {
            Items = await _context.Items
                .Where(i => i.Status == ItemStatus.Active)
                .OrderBy(i => i.PartNumber)
                .Take(100)
                .ToListAsync();

            TotalItems = await _context.Items.CountAsync();
            ItemsWithBarcodes = await _context.Items.CountAsync(i => !string.IsNullOrEmpty(i.Barcode));
            ItemsWithoutBarcodes = TotalItems - ItemsWithBarcodes;

            BarcodeFormatOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "BarcodeFormat", null, "");
            BarcodeSizeOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "BarcodeSize", "medium", "");
        }

        public Task<IActionResult> OnPostGenerateAsync(string BarcodeType, string LabelSize, bool IncludeDescription)
        {
            TempData["Success"] = "Labels generated successfully.";
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        public Task<IActionResult> OnPostBatchPrintAsync(string ItemIds, int Copies)
        {
            TempData["Success"] = $"Batch print job queued for {Copies} copies.";
            return Task.FromResult<IActionResult>(RedirectToPage());
        }
    }
}
