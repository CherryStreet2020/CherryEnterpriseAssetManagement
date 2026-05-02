using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
public class DataManagementModel : PageModel
{
    private readonly TemplateService _templateService;
    private readonly MasterDataImportService _importService;
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;

    public DataManagementModel(
        TemplateService templateService,
        MasterDataImportService importService,
        AppDbContext context,
        ITenantContext tenantContext)
    {
        _templateService = templateService;
        _importService = importService;
        _context = context;
        _tenantContext = tenantContext;
    }

    [BindProperty(SupportsGet = true, Name = "step")]
    public int CurrentStep { get; set; } = 0;

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? ActiveTab { get; set; } = "import";

    public ValidationResultDto? ValidationResult { get; set; }
    public ImportResultDto? ImportResult { get; set; }

    public void OnGet()
    {
        ActiveTab ??= "import";
        if (CurrentStep < 0) CurrentStep = 0;
        if (CurrentStep > 14) CurrentStep = 14;
    }

    public IActionResult OnGetDownloadTemplate(string template)
    {
        try
        {
            var bytes = _templateService.GenerateTemplate(template);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"CherryAI_Import_{template}_Template.xlsx");
        }
        catch
        {
            return RedirectToPage(new { tab = "import", step = CurrentStep });
        }
    }

    public IActionResult OnGetDownloadAllTemplates()
    {
        try
        {
            var bytes = _templateService.GenerateAllTemplatesZip();
            return File(bytes, "application/zip", "CherryAI_All_Import_Templates.zip");
        }
        catch
        {
            return RedirectToPage(new { tab = "import" });
        }
    }

    public IActionResult OnGetDownloadWorkbook()
    {
        try
        {
            var bytes = _templateService.GetMasterWorkbook();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ABS_Machining_EAM_Import_Workbook.xlsx");
        }
        catch
        {
            return RedirectToPage(new { tab = "import" });
        }
    }

    public async Task<IActionResult> OnPostValidateAsync(IFormFile? file, string template, int step)
    {
        CurrentStep = step;
        ActiveTab = "import";

        if (file == null || file.Length == 0)
        {
            ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                RowCount = 0,
                Errors = new List<string> { "No file selected. Please upload an .xlsx file." }
            };
            return Page();
        }

        using var stream = file.OpenReadStream();
        var result = await _importService.ValidateAsync(template, stream);
        ValidationResult = new ValidationResultDto
        {
            IsValid = result.IsValid,
            RowCount = result.TotalRows,
            Errors = result.Errors.Select(e => $"Row {e.Row}: [{e.Column}] {e.Message}").ToList()
        };
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(IFormFile? file, string template, int step)
    {
        CurrentStep = step;
        ActiveTab = "import";

        if (file == null || file.Length == 0)
        {
            return RedirectToPage(new { tab = "import", step });
        }

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(template, stream);
        ImportResult = new ImportResultDto
        {
            ImportedCount = result.ImportedCount,
            SkippedCount = result.SkippedCount,
            FailedCount = result.FailedCount
        };
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, string template, int step)
    {
        return await OnPostValidateAsync(file, template, step);
    }

    public async Task<IActionResult> OnPostExportAsync(string template)
    {
        var csv = new StringBuilder();

        switch (template)
        {
            case "Asset":
                var assets = await _context.Assets
                    .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                    .ToListAsync();
                csv.AppendLine("AssetNumber,Description,InServiceDate,AcquisitionCost,Status");
                foreach (var a in assets)
                    csv.AppendLine($"\"{a.AssetNumber}\",\"{a.Description}\",{a.InServiceDate:yyyy-MM-dd},{a.AcquisitionCost},{a.Status}");
                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{template}_Export_{DateTime.Now:yyyyMMdd}.csv");

            case "Item":
                var items = await _context.Items.ToListAsync();
                csv.AppendLine("PartNumber,Description,StandardCost,ReorderPoint,Status");
                foreach (var i in items)
                    csv.AppendLine($"\"{i.PartNumber}\",\"{i.Description}\",{i.StandardCost},{i.ReorderPoint},{i.Status}");
                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{template}_Export_{DateTime.Now:yyyyMMdd}.csv");

            case "Vendor":
                var vendors = await _context.Vendors
                    .Where(v => _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0))
                    .ToListAsync();
                csv.AppendLine("Code,Name,Email,Phone,Status");
                foreach (var v in vendors)
                    csv.AppendLine($"\"{v.Code}\",\"{v.Name}\",\"{v.Email}\",\"{v.Phone}\",{v.Status}");
                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{template}_Export_{DateTime.Now:yyyyMMdd}.csv");

            default:
                var bytes = _templateService.GenerateTemplate(template);
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{template}_Export_{DateTime.Now:yyyyMMdd}.xlsx");
        }
    }

    public class ValidationResultDto
    {
        public bool IsValid { get; set; }
        public int RowCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class ImportResultDto
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }
}
