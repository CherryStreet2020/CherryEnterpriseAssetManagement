// Theme B11 Wave R1-1 — admin probe for the production-org Department backbone.
// Exercises the extended Department entity + the closed WorkCenter.OwningDepartmentId
// FK (Lock-16 corollary: every button writes):
//
//   1) Create production department — a real shop department (IsProductionDepartment).
//   2) Nest under a parent          — create/ensure a parent "Manufacturing Operations"
//                                      dept and set the latest dept's ParentDepartmentId.
//   3) Assign a Work Center         — set a tenant-visible WC's OwningDepartmentId to the
//                                      latest production department (closes the orphan FK).
//   R) Reload
//
// All writes go through AppDbContext (tenant-scoped via ITenantContext.VisibleCompanyIds),
// so this probe is ControlPlaneExempt.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B11 R1-1 production-org Department backbone. Reads/writes " +
    "Department + WorkCenter.OwningDepartmentId via AppDbContext, tenant-scoped through " +
    "ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class DepartmentOrgProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<DepartmentOrgProbeModel> _logger;

    public DepartmentOrgProbeModel(AppDbContext db, ITenantContext tenant, ILogger<DepartmentOrgProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalDepartments { get; private set; }
    public int ProductionDepartments { get; private set; }
    public int NestedDepartments { get; private set; }
    public int WorkCentersWithDept { get; private set; }
    public IReadOnlyList<Row> Sample { get; private set; } = Array.Empty<Row>();

    public sealed record Row(int Id, string Code, string Name, DepartmentType Type, bool IsProduction, int? ParentId, int? SiteId);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<Department> Scoped() =>
        _db.Departments.Where(d => d.CompanyId == null || _tenant.VisibleCompanyIds.Contains(d.CompanyId.Value));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalDepartments = await Scoped().CountAsync(ct);
        ProductionDepartments = await Scoped().CountAsync(d => d.IsProductionDepartment, ct);
        NestedDepartments = await Scoped().CountAsync(d => d.ParentDepartmentId != null, ct);
        WorkCentersWithDept = await _db.WorkCenters
            .CountAsync(w => w.OwningDepartmentId != null && _tenant.VisibleCompanyIds.Contains(w.CompanyId), ct);

        Sample = await Scoped()
            .OrderByDescending(d => d.Id).Take(15)
            .Select(d => new Row(d.Id, d.Code, d.Name, d.Type, d.IsProductionDepartment, d.ParentDepartmentId, d.SiteId))
            .ToListAsync(ct);
    }

    private int ResolveCompanyId() => _tenant.VisibleCompanyIds.FirstOrDefault();

    // 1) CREATE a production department
    public async Task<IActionResult> OnPostCreateDeptAsync(CancellationToken ct)
    {
        var companyId = ResolveCompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company in scope."); await LoadStatsAsync(ct); return Page(); }

        // Real shop department fixture (no fake data).
        var code = $"CNC-MACH-{DateTime.UtcNow:HHmmss}";
        var dept = new Department
        {
            Code = code,
            Name = "CNC Machining",
            Description = "5-axis + turning cell — Ti-6Al-4V / Inconel 718 ETO work.",
            Type = DepartmentType.Production,
            IsProductionDepartment = true,
            CompanyId = companyId,
            IsActive = true,
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Created production department #{dept.Id} '{dept.Name}' ({dept.Code}). Use button 2 to nest it under Manufacturing Operations.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) NEST the latest production department under a parent "Manufacturing Operations"
    public async Task<IActionResult> OnPostNestAsync(CancellationToken ct)
    {
        var companyId = ResolveCompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company in scope."); await LoadStatsAsync(ct); return Page(); }

        var child = await Scoped()
            .Where(d => d.IsProductionDepartment && d.ParentDepartmentId == null)
            .OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (child == null) { Set(false, "No un-nested production department — create one with button 1 first."); await LoadStatsAsync(ct); return Page(); }

        // Ensure a parent "Manufacturing Operations" department exists.
        var parent = await Scoped().FirstOrDefaultAsync(d => d.Code == "MFG-OPS", ct);
        if (parent == null)
        {
            parent = new Department
            {
                Code = "MFG-OPS",
                Name = "Manufacturing Operations",
                Description = "Plant manufacturing org — parent of the production cells.",
                Type = DepartmentType.Operations,
                IsProductionDepartment = true,
                CompanyId = companyId,
                IsActive = true,
            };
            _db.Departments.Add(parent);
            await _db.SaveChangesAsync(ct);
        }

        if (parent.Id == child.Id) { Set(false, "Resolved parent == child — create a distinct child first."); await LoadStatsAsync(ct); return Page(); }

        child.ParentDepartmentId = parent.Id;
        await _db.SaveChangesAsync(ct);

        Set(true, $"Nested '{child.Name}' (#{child.Id}) under '{parent.Name}' (#{parent.Id}) — Site→Dept→sub-Dept hierarchy via ParentDepartmentId.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) ASSIGN a Work Center to the latest production department (close the orphan FK)
    public async Task<IActionResult> OnPostAssignWcAsync(CancellationToken ct)
    {
        var companyId = ResolveCompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company in scope."); await LoadStatsAsync(ct); return Page(); }

        var dept = await Scoped()
            .Where(d => d.IsProductionDepartment)
            .OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (dept == null) { Set(false, "No production department — create one with button 1 first."); await LoadStatsAsync(ct); return Page(); }

        var wc = await _db.WorkCenters
            .Where(w => _tenant.VisibleCompanyIds.Contains(w.CompanyId))
            .OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center to assign."); await LoadStatsAsync(ct); return Page(); }

        wc.OwningDepartmentId = dept.Id;
        await _db.SaveChangesAsync(ct);

        Set(true, $"Assigned Work Center #{wc.Id} ({wc.Code}) → department '{dept.Name}' (#{dept.Id}). The OwningDepartmentId orphan FK is now live.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Stats reloaded.");
        return Page();
    }
}
