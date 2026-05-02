using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
public class ImportWizardModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly TemplateService _templateService;
    private readonly MasterDataImportService _importService;

    public ImportWizardModel(AppDbContext context, TemplateService templateService, MasterDataImportService importService)
    {
        _context = context;
        _templateService = templateService;
        _importService = importService;
    }

    public int CurrentStep { get; set; } = 0;
    public Dictionary<string, int> EntityCounts { get; set; } = new();
    public Dictionary<string, string> StepStates { get; set; } = new();
    public ImportValidationResult? ValidationResult { get; set; }
    public MasterImportResult? LastImportResult { get; set; }
    public string? ActiveEntity { get; set; }

    public async Task OnGetAsync(int? step)
    {
        CurrentStep = step ?? 0;
        if (CurrentStep >= 0 && CurrentStep < TemplateService.Steps.Length)
            ActiveEntity = TemplateService.Steps[CurrentStep].Key;
        await LoadEntityCounts();
        LoadStepStates();
    }

    public async Task<IActionResult> OnGetDownloadTemplateAsync(string entity)
    {
        try
        {
            var bytes = _templateService.GenerateTemplate(entity);
            var step = TemplateService.Steps.FirstOrDefault(s => s.Key == entity);
            var fileName = $"CherryAI_Import_{entity}_Template.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostValidateAsync(string entity, int step, IFormFile file)
    {
        CurrentStep = step;
        ActiveEntity = entity;
        await LoadEntityCounts();
        LoadStepStates();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a file to upload.";
            return Page();
        }

        using var stream = file.OpenReadStream();
        ValidationResult = await _importService.ValidateAsync(entity, stream);

        if (ValidationResult.IsValid)
            TempData["ValidationPassed"] = "true";

        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(string entity, int step, IFormFile file)
    {
        CurrentStep = step;
        ActiveEntity = entity;

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a file to upload.";
            await LoadEntityCounts();
            LoadStepStates();
            return Page();
        }

        using var stream = file.OpenReadStream();
        LastImportResult = await _importService.ImportAsync(entity, stream);

        if (LastImportResult.ImportedCount > 0)
        {
            SetStepState(entity, "completed");
            TempData["Success"] = $"Successfully imported {LastImportResult.ImportedCount} {entity} records." +
                (LastImportResult.SkippedCount > 0 ? $" {LastImportResult.SkippedCount} skipped (already exist)." : "") +
                (LastImportResult.FailedCount > 0 ? $" {LastImportResult.FailedCount} failed." : "");
        }
        else if (LastImportResult.SkippedCount > 0)
        {
            SetStepState(entity, "completed");
            TempData["Success"] = $"All {LastImportResult.SkippedCount} records already exist — skipped.";
        }
        else
        {
            SetStepState(entity, "haserrors");
            TempData["Error"] = "No records were imported. Check the errors below.";
        }

        await LoadEntityCounts();
        LoadStepStates();
        return Page();
    }

    public async Task<IActionResult> OnPostSkipAsync(string entity, int step)
    {
        SetStepState(entity, "skipped");
        return RedirectToPage(new { step = step + 1 });
    }

    private async Task LoadEntityCounts()
    {
        EntityCounts["Company"] = await _context.Companies.CountAsync();
        EntityCounts["Site"] = await _context.Sites.CountAsync();
        EntityCounts["Location"] = await _context.Locations.CountAsync();
        EntityCounts["Department"] = await _context.Departments.CountAsync();
        EntityCounts["GlAccount"] = await _context.GlAccounts.CountAsync();
        EntityCounts["AssetCategory"] = await _context.AssetCategories.CountAsync();
        EntityCounts["Vendor"] = await _context.Vendors.CountAsync();
        EntityCounts["Manufacturer"] = await _context.Set<Models.Manufacturer>().CountAsync();
        EntityCounts["Item"] = await _context.Items.CountAsync();
        EntityCounts["ApprovedVendorList"] = await _context.Set<Models.ItemApprovedVendor>().CountAsync();
        EntityCounts["Asset"] = await _context.Assets.CountAsync();
        EntityCounts["DepreciationBook"] = await _context.Books.CountAsync();
        EntityCounts["Technician"] = await _context.Technicians.CountAsync();
        EntityCounts["PMTemplate"] = await _context.PMTemplates.CountAsync();
        EntityCounts["CIPProject"] = await _context.CipProjects.CountAsync();
    }

    private void LoadStepStates()
    {
        foreach (var s in TemplateService.Steps)
        {
            var key = $"ImportWizard_State_{s.Key}";
            if (TempData.Peek(key) is string state)
                StepStates[s.Key] = state;
        }
    }

    private void SetStepState(string entity, string state)
    {
        TempData[$"ImportWizard_State_{entity}"] = state;
        StepStates[entity] = state;
    }
}
