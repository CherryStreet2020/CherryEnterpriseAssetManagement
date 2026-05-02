using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Abs.FixedAssets.Data;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ImportModel : PageModel
    {
        private readonly AppDbContext _context;

        public ImportModel(AppDbContext context)
        {
            _context = context;
        }

        public List<ImportHistoryItem> ImportHistory { get; set; } = new();

        public void OnGet()
        {
            ImportHistory = new List<ImportHistoryItem>();
        }

        public Task<IActionResult> OnPostImportAssetsAsync(IFormFile File, bool HasHeader)
        {
            if (File != null && File.Length > 0)
            {
                TempData["Success"] = $"Asset import started for {File.FileName}";
            }
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        public Task<IActionResult> OnPostImportItemsAsync(IFormFile File)
        {
            if (File != null && File.Length > 0)
            {
                TempData["Success"] = $"Item import started for {File.FileName}";
            }
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        public Task<IActionResult> OnPostImportVendorsAsync(IFormFile File)
        {
            if (File != null && File.Length > 0)
            {
                TempData["Success"] = $"Vendor import started for {File.FileName}";
            }
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        public class ImportHistoryItem
        {
            public DateTime Date { get; set; }
            public string Type { get; set; } = "";
            public string FileName { get; set; } = "";
            public int RecordCount { get; set; }
            public bool Success { get; set; }
            public string User { get; set; } = "";
        }
    }
}
