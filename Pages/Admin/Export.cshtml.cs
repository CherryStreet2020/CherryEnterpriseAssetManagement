using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using System.Text;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ExportModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public ExportModel(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

        public int AssetCount { get; set; }
        public int ItemCount { get; set; }
        public int VendorCount { get; set; }
        public int MaintenanceCount { get; set; }
        public List<BookInfo> Books { get; set; } = new();

        public async Task OnGetAsync()
        {
            var companyId = GetCompanyId();
            AssetCount = await _context.Assets.Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0)).CountAsync();
            ItemCount = await _context.Items.CountAsync();
            VendorCount = await _context.Vendors.Where(v => _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0)).CountAsync();
            MaintenanceCount = await _context.MaintenanceEvents.CountAsync();
            
            Books = await _context.Books
                .Where(b => _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0))
                .Select(b => new BookInfo { Id = b.Id, Name = b.Name ?? $"Book {b.Id}" })
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostExportAssetsAsync(string Format)
        {
            var assets = await _context.Assets.Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0)).ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("AssetNumber,Description,InServiceDate,AcquisitionCost,Status");
            foreach (var a in assets)
            {
                csv.AppendLine($"\"{a.AssetNumber}\",\"{a.Description}\",{a.InServiceDate:yyyy-MM-dd},{a.AcquisitionCost},{a.Status}");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "assets_export.csv");
        }

        public async Task<IActionResult> OnPostExportItemsAsync(string Format)
        {
            var items = await _context.Items.ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("PartNumber,Description,StandardCost,ReorderPoint,Status");
            foreach (var i in items)
            {
                csv.AppendLine($"\"{i.PartNumber}\",\"{i.Description}\",{i.StandardCost},{i.ReorderPoint},{i.Status}");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "items_export.csv");
        }

        public async Task<IActionResult> OnPostExportVendorsAsync(string Format)
        {
            var vendors = await _context.Vendors.Where(v => _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0)).ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("Code,Name,Email,Phone,Status");
            foreach (var v in vendors)
            {
                csv.AppendLine($"\"{v.Code}\",\"{v.Name}\",\"{v.Email}\",\"{v.Phone}\",{v.Status}");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "vendors_export.csv");
        }

        public async Task<IActionResult> OnPostExportMaintenanceAsync(DateTime? StartDate, DateTime? EndDate)
        {
            var companyId = GetCompanyId();
            var query = _context.MaintenanceEvents.Include(m => m.Asset).Where(m => _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0));
            if (StartDate.HasValue) query = query.Where(m => m.ScheduledDate >= StartDate.Value);
            if (EndDate.HasValue) query = query.Where(m => m.ScheduledDate <= EndDate.Value);
            
            var events = await query.ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("Id,AssetId,Type,Status,Description,ScheduledDate,CompletedDate");
            foreach (var e in events)
            {
                csv.AppendLine($"{e.Id},{e.AssetId},{e.Type},{e.Status},\"{e.Description}\",{e.ScheduledDate:yyyy-MM-dd},{e.CompletedDate:yyyy-MM-dd}");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "maintenance_export.csv");
        }

        public Task<IActionResult> OnPostExportDepreciationAsync(int? BookId)
        {
            TempData["Info"] = "Depreciation export functionality coming soon.";
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        public class BookInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }
    }
}
