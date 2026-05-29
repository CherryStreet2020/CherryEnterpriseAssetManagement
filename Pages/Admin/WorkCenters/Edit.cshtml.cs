using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.WorkCenters;

// /Admin/WorkCenters/Edit       — create a new work center
// /Admin/WorkCenters/Edit/{id}  — edit an existing one
//
// Writes go THROUGH IWorkCenterService (the control-plane boundary); _db is used only
// for tenant-scoped picker projections (Locations / Calendars / Departments). Field
// groups follow the dept-wc-machine research: Identity & Organization, Scheduling &
// Dispatch, Cost.
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Work-center create/edit form. All writes route through IWorkCenterService; AppDbContext " +
    "is used only for read-only picker projections (locations/calendars/departments).")]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IWorkCenterService _svc;

    public EditModel(AppDbContext db, ITenantContext tenant, IWorkCenterService svc)
    {
        _db = db; _tenant = tenant; _svc = svc;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    public bool IsNew => Id is null or 0;
    public string PageTitle => IsNew ? "New Work Center" : $"Edit {Code}";
    public string? ErrorMessage { get; private set; }

    // ── Identity & organization ─────────────────────────────────
    [BindProperty] public string Code { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public WorkCenterType Type { get; set; } = WorkCenterType.Machine;
    [BindProperty] public WorkCenterCapacityModel CapacityModel { get; set; } = WorkCenterCapacityModel.SingleResource;
    [BindProperty] public int LocationId { get; set; }
    [BindProperty] public int? OwningDepartmentId { get; set; }
    [BindProperty] public int? CalendarId { get; set; }

    // ── Scheduling & dispatch ───────────────────────────────────
    [BindProperty] public bool SchedulingEnabled { get; set; } = true;
    [BindProperty] public bool BottleneckFlag { get; set; }
    [BindProperty] public int? ConstraintPriority { get; set; }
    [BindProperty] public int? SimultaneousOperationsMax { get; set; }
    [BindProperty] public WorkCenterDispatchRule DispatchRule { get; set; } = WorkCenterDispatchRule.FirstInFirstOut;
    [BindProperty] public WorkCenterSetupFamilyRule SetupFamilyRule { get; set; } = WorkCenterSetupFamilyRule.None;
    [BindProperty] public string? SetupFamilyCode { get; set; }
    [BindProperty] public decimal EfficiencyPct { get; set; } = 100m;
    [BindProperty] public decimal UtilizationPct { get; set; } = 100m;

    // ── Cost ────────────────────────────────────────────────────
    [BindProperty] public decimal StandardCostRatePerHour { get; set; }
    [BindProperty] public decimal OverheadRatePerHour { get; set; }
    [BindProperty] public string CurrencyCode { get; set; } = "USD";

    // Pickers
    public List<SelectListItem> LocationOptions { get; private set; } = new();
    public List<SelectListItem> CalendarOptions { get; private set; } = new();
    public List<SelectListItem> DepartmentOptions { get; private set; } = new();

    private int Company() => _tenant.VisibleCompanyIds.FirstOrDefault();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadPickersAsync(ct);

        if (!IsNew)
        {
            var w = await _db.WorkCenters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id, ct);
            if (w == null) { ErrorMessage = "Work center not found."; return Page(); }
            Code = w.Code; Name = w.Name; Description = w.Description; Type = w.Type;
            CapacityModel = w.CapacityModel; LocationId = w.LocationId;
            OwningDepartmentId = w.OwningDepartmentId; CalendarId = w.CalendarId;
            SchedulingEnabled = w.SchedulingEnabled ?? true; BottleneckFlag = w.BottleneckFlag;
            ConstraintPriority = w.ConstraintPriority; SimultaneousOperationsMax = w.SimultaneousOperationsMax;
            DispatchRule = w.DispatchRule; SetupFamilyRule = w.SetupFamilyRule; SetupFamilyCode = w.SetupFamilyCode;
            EfficiencyPct = w.EfficiencyPct; UtilizationPct = w.UtilizationPct;
            StandardCostRatePerHour = w.StandardCostRatePerHour; OverheadRatePerHour = w.OverheadRatePerHour;
            CurrencyCode = w.CurrencyCode;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { ErrorMessage = "No company in your tenant scope."; await LoadPickersAsync(ct); return Page(); }

        if (IsNew)
        {
            var req = new CreateWorkCenterRequest(
                companyId, LocationId, Code, Name, Description, Type, CapacityModel, CalendarId,
                OwningDepartmentId, StandardCostRatePerHour, OverheadRatePerHour, CurrencyCode, "WorkCenterEdit",
                SchedulingEnabled, BottleneckFlag, ConstraintPriority, SimultaneousOperationsMax,
                DispatchRule, SetupFamilyRule, SetupFamilyCode);
            var r = await _svc.CreateAsync(req, ct);
            if (r.IsFailure) { ErrorMessage = r.Error; await LoadPickersAsync(ct); return Page(); }
            return RedirectToPage("Details", new { id = r.Value!.Id });
        }
        else
        {
            var req = new UpdateWorkCenterHeaderRequest(
                Id!.Value, LocationId, Name, Description, Type, CapacityModel, CalendarId, OwningDepartmentId,
                EfficiencyPct, UtilizationPct, StandardCostRatePerHour, OverheadRatePerHour, CurrencyCode, "WorkCenterEdit",
                SchedulingEnabled, BottleneckFlag, ConstraintPriority, SimultaneousOperationsMax,
                DispatchRule, SetupFamilyRule, SetupFamilyCode);
            var r = await _svc.UpdateHeaderAsync(req, ct);
            if (r.IsFailure) { ErrorMessage = r.Error; await LoadPickersAsync(ct); return Page(); }
            return RedirectToPage("Details", new { id = Id.Value });
        }
    }

    private async Task LoadPickersAsync(CancellationToken ct)
    {
        var companyId = Company();
        LocationOptions = await _db.Locations.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .OrderBy(l => l.Name)
            .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Name })
            .ToListAsync(ct);
        CalendarOptions = await _db.WorkCalendars.AsNoTracking()
            .Where(c => c.IsActive && (c.CompanyId == companyId || c.CompanyId == null))
            .OrderBy(c => c.Code)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Code + " — " + c.Name })
            .ToListAsync(ct);
        DepartmentOptions = await _db.Departments.AsNoTracking()
            .Where(d => d.CompanyId == companyId)
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync(ct);
    }
}
