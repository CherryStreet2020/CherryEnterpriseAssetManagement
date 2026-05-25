using System.Security.Claims;
using System.Threading.Tasks;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.AssetImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Admin.AssetImport
{
    [Authorize(Roles = "Admin")]
    public class UploadModel : PageModel
    {
        private const long MaxFileSizeBytes = 10L * 1024 * 1024; // 10 MB

        private readonly IAssetImportService _service;
        private readonly ITenantContext _tenant;

        public UploadModel(IAssetImportService service, ITenantContext tenant)
        {
            _service = service;
            _tenant = tenant;
        }

        public int CompanyId { get; private set; }

        [BindProperty]
        public IFormFile? ExcelFile { get; set; }

        public IActionResult OnGet()
        {
            CompanyId = _tenant.CompanyId ?? 1;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            CompanyId = _tenant.CompanyId ?? 1;

            if (ExcelFile is null || ExcelFile.Length == 0)
            {
                TempData["Error"] = "Please choose an Excel file (.xlsx) to upload.";
                return RedirectToPage("Upload");
            }

            if (ExcelFile.Length > MaxFileSizeBytes)
            {
                TempData["Error"] = $"File too large ({ExcelFile.Length / 1024 / 1024} MB). Max is 10 MB.";
                return RedirectToPage("Upload");
            }

            if (!ExcelFile.FileName.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only .xlsx files are supported.";
                return RedirectToPage("Upload");
            }

            var userId = ResolveUserId();
            var username = User.Identity?.Name;

            try
            {
                using var stream = ExcelFile.OpenReadStream();
                var batch = await _service.ParseAndStageAsync(
                    excelStream: stream,
                    fileName: ExcelFile.FileName,
                    fileSizeBytes: ExcelFile.Length,
                    companyId: CompanyId,
                    organizationId: null,
                    siteId: _tenant.SiteId,
                    userId: userId,
                    username: username,
                    ct: ct);
                TempData["Success"] = $"Parsed {batch.RowCount} rows from {ExcelFile.FileName} — {batch.ValidRowCount} valid, {batch.ErrorRowCount} with errors. Preview below.";
                return Redirect($"/Admin/AssetImport/Preview/{batch.Id}");
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Parse failed: {ex.Message}";
                return RedirectToPage("Upload");
            }
        }

        private int ResolveUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : 0;
        }
    }
}
