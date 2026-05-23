using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Projects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CustomerProjects;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Hybrid page — AppDbContext for lookup-dropdown hydration only; the POST handler routes through ICustomerProjectService.CreateAsync.")]
// Sprint 13.5 PR #4 — /CustomerProjects/Create
// New project form. Posts to ICustomerProjectService.CreateAsync.
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ICustomerProjectService _service;
    private readonly ITenantContext _tenantContext;

    public CreateModel(AppDbContext db, ICustomerProjectService service, ITenantContext tenantContext)
    {
        _db = db;
        _service = service;
        _tenantContext = tenantContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IEnumerable<SelectListItem> CompanyOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> CustomerOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> ProgramOptions { get; private set; } = Array.Empty<SelectListItem>();

    public async Task OnGetAsync()
    {
        await HydrateLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await HydrateLookupsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new CreateCustomerProjectRequest(
            CompanyId:          Input.CompanyId,
            ProgramId:          Input.ProgramId,
            PrimaryCustomerId:  Input.PrimaryCustomerId,
            Code:               Input.Code,
            Name:               Input.Name,
            Description:        Input.Description,
            Mode:               Input.Mode,
            CostingMode:        Input.CostingMode,
            RevenueMode:        Input.RevenueMode,
            ContractValue:      Input.ContractValue,
            Currency:           string.IsNullOrWhiteSpace(Input.Currency) ? "CAD" : Input.Currency,
            TargetStartDate:    Input.TargetStartDate,
            TargetEndDate:      Input.TargetEndDate,
            ProjectManagerName: Input.ProjectManagerName,
            ProjectManagerId:   null,
            CustomerPoNumber:   Input.CustomerPoNumber,
            ContractType:       Input.ContractType,
            QualityProgram:     Input.QualityProgram,
            ExportControl:      Input.ExportControl,
            CreatedBy:          User?.Identity?.Name);

        var result = await _service.CreateAsync(request, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        TempData["Success"] = $"Project {result.Value!.Code} created.";
        return RedirectToPage("/CustomerProjects/Details", new { id = result.Value!.Id });
    }

    private async Task HydrateLookupsAsync()
    {
        var visible = _tenantContext.VisibleCompanyIds;

        CompanyOptions = (await _db.Companies
            .AsNoTracking()
            .Where(c => visible.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync())
            .Select(c => new SelectListItem(value: c.Id.ToString(), text: c.Name));

        CustomerOptions = (await _db.Customers
            .AsNoTracking()
            .Where(c => visible.Contains(c.CompanyId) && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync())
            .Select(c => new SelectListItem(value: c.Id.ToString(), text: c.Name));

        ProgramOptions = (await _db.Set<Abs.FixedAssets.Models.Projects.Program>()
            .AsNoTracking()
            .Where(p => p.CompanyId != null && visible.Contains(p.CompanyId.Value))
            .OrderBy(p => p.Code)
            .Select(p => new { p.Id, p.Code, p.Name })
            .ToListAsync())
            .Select(p => new SelectListItem(value: p.Id.ToString(), text: $"{p.Code} — {p.Name}"));
    }

    public sealed class InputModel
    {
        [Required, Display(Name = "Company")]
        public int CompanyId { get; set; }

        [Display(Name = "Program")]
        public int? ProgramId { get; set; }

        [Display(Name = "Primary Customer")]
        public int? PrimaryCustomerId { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public CustomerProjectMode Mode { get; set; } = CustomerProjectMode.Standard;
        public CustomerProjectCostingMode CostingMode { get; set; } = CustomerProjectCostingMode.Aggregate;
        public CustomerProjectRevenueMode RevenueMode { get; set; } = CustomerProjectRevenueMode.CompletedContract;

        [Display(Name = "Contract Value")]
        public decimal? ContractValue { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "CAD";

        [DataType(DataType.Date), Display(Name = "Target Start")]
        public DateTime? TargetStartDate { get; set; }

        [DataType(DataType.Date), Display(Name = "Target End")]
        public DateTime? TargetEndDate { get; set; }

        [StringLength(100), Display(Name = "Project Manager")]
        public string? ProjectManagerName { get; set; }

        [StringLength(100), Display(Name = "Customer PO #")]
        public string? CustomerPoNumber { get; set; }

        [Display(Name = "Contract Type")]
        public ContractType? ContractType { get; set; }

        [Display(Name = "Quality Program")]
        public QualityProgram? QualityProgram { get; set; }

        [Display(Name = "Export Control")]
        public ExportControl ExportControl { get; set; } = Abs.FixedAssets.Models.Projects.ExportControl.None;
    }
}
