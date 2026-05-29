// Theme B11 Wave R2-5 — admin probe for Tool/Fixture + the Labor/Vendor/Tool
// resource bridges. Exercises the new Tool master and the ProductionResource
// bridge FKs (Lock-16 corollary: every button writes):
//
//   1) Create tool (fixture)        — a real Tool master record.
//   2) Create tool resource          — ProductionResource(Tool) bridging the latest Tool.
//   3) Bridge labor resource         — ProductionResource(Labor) → a same-company Employee.
//   4) Bridge vendor resource        — ProductionResource(Vendor) → a same-company Vendor.
//   R) Reload
//
// Company-scoped pickers (R1-2 Codex lesson). ControlPlaneExempt (writes via AppDbContext).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B11 R2-5 Tool/Fixture + Labor/Vendor/Tool resource bridges. " +
    "Reads/writes Tool + ProductionResource via AppDbContext, tenant-scoped through " +
    "ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class ToolResourceProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ToolResourceProbeModel> _logger;

    public ToolResourceProbeModel(AppDbContext db, ITenantContext tenant, ILogger<ToolResourceProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalTools { get; private set; }
    public int ToolResourceCount { get; private set; }
    public int LaborBridgedCount { get; private set; }
    public int VendorBridgedCount { get; private set; }
    public IReadOnlyList<ResRow> ResourceSample { get; private set; } = Array.Empty<ResRow>();

    public sealed record ResRow(int Id, string Code, ResourceKind Kind, int? AssetId, int? EmployeeId, int? VendorId, int? ToolId);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<ProductionResource> ScopedRes() =>
        _db.ProductionResources.Where(r => _tenant.VisibleCompanyIds.Contains(r.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalTools = await _db.Tools.CountAsync(t => _tenant.VisibleCompanyIds.Contains(t.CompanyId), ct);
        ToolResourceCount = await ScopedRes().CountAsync(r => r.ToolId != null, ct);
        LaborBridgedCount = await ScopedRes().CountAsync(r => r.EmployeeId != null, ct);
        VendorBridgedCount = await ScopedRes().CountAsync(r => r.VendorId != null, ct);

        ResourceSample = await ScopedRes()
            .OrderByDescending(r => r.Id).Take(15)
            .Select(r => new ResRow(r.Id, r.Code, r.ResourceKind, r.AssetId, r.EmployeeId, r.VendorId, r.ToolId))
            .ToListAsync(ct);
    }

    private int CompanyId() => _tenant.VisibleCompanyIds.FirstOrDefault();

    private Task<WorkCenter?> LatestWcAsync(int companyId, CancellationToken ct) =>
        _db.WorkCenters.Where(w => w.CompanyId == companyId).OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);

    // 1) CREATE a tool (fixture)
    public async Task<IActionResult> OnPostCreateToolAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var tool = new Tool
        {
            CompanyId = companyId,
            ToolType = ToolType.Fixture,
            Code = $"FIX-TI-BRKT-{DateTime.UtcNow:HHmmss}",
            Name = "Ti-6Al-4V engine-mount bracket 5-axis workholding fixture",
            Description = "Dedicated 5-axis fixture for the ETO Ti bracket family.",
            CribLocation = "CRIB-A-12",
            IsControlled = true,
            CalibrationRequired = false,
            Status = ToolStatus.Available,
        };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Created Tool #{tool.Id} ({tool.Code}) — {tool.Name}. Crib {tool.CribLocation}, controlled. " +
                  "This replaces the loose CSV RequiredToolingIds with a real record.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) CREATE a tool resource bridging the latest tool
    public async Task<IActionResult> OnPostCreateToolResourceAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var tool = await _db.Tools.Where(t => t.CompanyId == companyId)
            .OrderByDescending(t => t.Id).FirstOrDefaultAsync(ct);
        if (tool == null) { Set(false, "No Tool yet — create one with button 1."); await LoadStatsAsync(ct); return Page(); }

        var wc = await LatestWcAsync(companyId, ct);
        var res = new ProductionResource
        {
            CompanyId = companyId,
            SiteId = wc?.SiteId,
            ResourceKind = ResourceKind.Tool,
            // Independent timestamped code (not RES-{tool.Code}) — stays under
            // ProductionResource.Code MaxLength(50) and avoids a same-second
            // (CompanyId, Code) collision off the same tool.
            Code = $"RES-TOOL-{DateTime.UtcNow:HHmmss}",
            Name = $"Tool resource: {tool.Name}",
            ToolId = tool.Id,
            WorkCenterId = wc?.Id,
            Status = ProductionResourceStatus.Active,
            FiniteCapacityFlag = true,
            ExclusiveUse = true,   // a dedicated fixture can't run two jobs at once
        };
        _db.ProductionResources.Add(res);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Created tool resource #{res.Id} bridging Tool #{tool.Id} ({tool.Code}), exclusive-use. " +
                  "The Tool master is now a schedulable resource.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) BRIDGE a labor resource to a same-company Employee
    public async Task<IActionResult> OnPostBridgeLaborAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var emp = await _db.Employees.Where(e => e.CompanyId == companyId)
            .OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        var wc = await LatestWcAsync(companyId, ct);

        var res = new ProductionResource
        {
            CompanyId = companyId,
            SiteId = wc?.SiteId,
            ResourceKind = ResourceKind.Labor,
            Code = $"LAB-{DateTime.UtcNow:HHmmss}",
            Name = emp != null ? $"Labor: {emp.FullName}" : "Labor resource (no Employee in company)",
            EmployeeId = emp?.Id,
            WorkCenterId = wc?.Id,
            Status = ProductionResourceStatus.Active,
            FiniteCapacityFlag = true,
            CostRatePerHour = 62.00m,
        };
        _db.ProductionResources.Add(res);
        await _db.SaveChangesAsync(ct);

        var bridge = emp != null ? $"bridged to Employee #{emp.Id} ({emp.FullName})" : "no Employee in company (bridge null)";
        Set(true, $"Created labor resource #{res.Id} {bridge}. Labor pool reuses the existing Employee master (PRA-8), no new HR model.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) BRIDGE a vendor resource to a same-company Vendor
    public async Task<IActionResult> OnPostBridgeVendorAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var vendor = await _db.Vendors.Where(v => v.CompanyId != null && v.CompanyId == companyId)
            .OrderByDescending(v => v.Id).FirstOrDefaultAsync(ct);
        var wc = await LatestWcAsync(companyId, ct);

        var res = new ProductionResource
        {
            CompanyId = companyId,
            SiteId = wc?.SiteId,
            ResourceKind = ResourceKind.Vendor,
            Code = $"VEN-{DateTime.UtcNow:HHmmss}",
            Name = vendor != null ? $"Vendor: {vendor.Name}" : "Vendor resource (no Vendor in company)",
            VendorId = vendor?.Id,
            WorkCenterId = wc?.Id,
            Status = ProductionResourceStatus.Active,
            FiniteCapacityFlag = false,   // outside processing — treat as infinite by default
        };
        _db.ProductionResources.Add(res);
        await _db.SaveChangesAsync(ct);

        var bridge = vendor != null ? $"bridged to Vendor #{vendor.Id} ({vendor.Name})" : "no Vendor in company (bridge null)";
        Set(true, $"Created vendor resource #{res.Id} {bridge}. Subcontract capacity reuses the existing Vendor master + SubcontractOperation.");
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
