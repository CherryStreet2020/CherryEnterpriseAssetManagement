// Theme B11 Wave R2-4 — admin probe for the ProductionResource ↔ Asset bridge
// (opens Wave R2). Exercises the schedulable resource record + the EAM bridge +
// WC assignment (Lock-16 corollary: every button writes):
//
//   1) Create machine resource — bridged to a same-company Asset + assigned to
//                                the latest WC (primary, finite, costed).
//   2) Create labor resource    — no Asset (ResourceKind.Labor), assigned to the WC.
//   3) Set effective window      — stamp EffectiveFrom + IsPrimary on the latest resource.
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
    "Admin diagnostic probe for Theme B11 R2-4 ProductionResource ↔ Asset bridge. Reads/writes " +
    "ProductionResource via AppDbContext, tenant-scoped through ITenantContext.VisibleCompanyIds " +
    "on every read and write.")]
public sealed class ProductionResourceProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProductionResourceProbeModel> _logger;

    public ProductionResourceProbeModel(AppDbContext db, ITenantContext tenant, ILogger<ProductionResourceProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalResources { get; private set; }
    public int MachineCount { get; private set; }
    public int LaborCount { get; private set; }
    public int WithAssetCount { get; private set; }
    public int AssignedToWcCount { get; private set; }
    public IReadOnlyList<Row> Sample { get; private set; } = Array.Empty<Row>();

    public sealed record Row(int Id, string Code, string Name, ResourceKind Kind, int? AssetId,
        int? WorkCenterId, bool IsPrimary, bool Finite, decimal CostRate, ProductionResourceStatus Status);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<ProductionResource> Scoped() =>
        _db.ProductionResources.Where(r => _tenant.VisibleCompanyIds.Contains(r.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalResources = await Scoped().CountAsync(ct);
        MachineCount = await Scoped().CountAsync(r => r.ResourceKind == ResourceKind.Machine, ct);
        LaborCount = await Scoped().CountAsync(r => r.ResourceKind == ResourceKind.Labor, ct);
        WithAssetCount = await Scoped().CountAsync(r => r.AssetId != null, ct);
        AssignedToWcCount = await Scoped().CountAsync(r => r.WorkCenterId != null, ct);

        Sample = await Scoped()
            .OrderByDescending(r => r.Id).Take(15)
            .Select(r => new Row(r.Id, r.Code, r.Name, r.ResourceKind, r.AssetId, r.WorkCenterId,
                r.IsPrimary, r.FiniteCapacityFlag, r.CostRatePerHour, r.Status))
            .ToListAsync(ct);
    }

    private Task<WorkCenter?> LatestWcAsync(CancellationToken ct) =>
        _db.WorkCenters.Where(w => _tenant.VisibleCompanyIds.Contains(w.CompanyId))
            .OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);

    // 1) CREATE a machine resource bridged to a same-company Asset
    public async Task<IActionResult> OnPostCreateMachineAsync(CancellationToken ct)
    {
        var wc = await LatestWcAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center to assign."); await LoadStatsAsync(ct); return Page(); }

        // Company-scoped Asset bridge (best-effort — AssetId is nullable).
        var asset = await _db.Set<Asset>()
            .Where(a => a.CompanyId != null && a.CompanyId == wc.CompanyId)
            .OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);

        var res = new ProductionResource
        {
            CompanyId = wc.CompanyId,
            SiteId = wc.SiteId,
            ResourceKind = ResourceKind.Machine,
            Code = $"HAAS-UMC750-{DateTime.UtcNow:HHmmss}",
            Name = "Haas UMC-750 5-axis machining center",
            Description = "5-axis VMC — Ti-6Al-4V / Inconel 718 ETO work.",
            AssetId = asset?.Id,
            WorkCenterId = wc.Id,
            IsPrimary = true,
            EffectiveFromUtc = DateTime.UtcNow,
            Status = ProductionResourceStatus.Active,
            FiniteCapacityFlag = true,
            CostRatePerHour = 175.00m,
            EfficiencyPct = 100,
            UtilizationPct = 85,
        };
        _db.ProductionResources.Add(res);
        await _db.SaveChangesAsync(ct);

        var bridge = asset != null ? $"bridged to Asset #{asset.Id}" : "no Asset in company (bridge left null)";
        Set(true, $"Created machine resource #{res.Id} ({res.Code}) → WC #{wc.Id} ({wc.Code}), {bridge}, finite, $175/hr. " +
                  "This is the dual identity: scheduling resource ↔ EAM asset, never duplicated.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) CREATE a labor resource (no Asset)
    public async Task<IActionResult> OnPostCreateLaborAsync(CancellationToken ct)
    {
        var wc = await LatestWcAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center to assign."); await LoadStatsAsync(ct); return Page(); }

        var res = new ProductionResource
        {
            CompanyId = wc.CompanyId,
            SiteId = wc.SiteId,
            ResourceKind = ResourceKind.Labor,
            Code = $"WELDER-AWS-AL-{DateTime.UtcNow:HHmmss}",
            Name = "AWS-certified aluminum TIG welder (labor pool)",
            Description = "Labor resource — no Asset; bridges to Employee/Craft in R2-5.",
            AssetId = null,
            WorkCenterId = wc.Id,
            IsPrimary = false,
            Status = ProductionResourceStatus.Active,
            FiniteCapacityFlag = true,
            CostRatePerHour = 62.00m,
        };
        _db.ProductionResources.Add(res);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Created labor resource #{res.Id} ({res.Code}) → WC #{wc.Id} ({wc.Code}), no Asset, $62/hr. " +
                  "A labor/tool/vendor resource schedules without being a fixed asset.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) SET effective window + primary on the latest resource
    public async Task<IActionResult> OnPostSetEffectiveAsync(CancellationToken ct)
    {
        var res = await Scoped().OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        if (res == null) { Set(false, "No resource yet — create one with button 1 or 2."); await LoadStatsAsync(ct); return Page(); }

        res.EffectiveFromUtc = DateTime.UtcNow;
        res.EffectiveToUtc = DateTime.UtcNow.AddYears(1);
        res.IsPrimary = true;
        res.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        Set(true, $"Resource #{res.Id} ({res.Code}): effective {res.EffectiveFromUtc:yyyy-MM-dd} → {res.EffectiveToUtc:yyyy-MM-dd}, primary. " +
                  "Effective dating lets a resource move work centers over time.");
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
