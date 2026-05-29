// Theme B7 Wave B PR-4 — admin probe for the ItemCrystallization entity +
// structure fingerprint. Exercises the new table end-to-end (Lock-16 corollary:
// every button writes):
//
//   1) Compute fingerprint     — SHA-256 over the target PRO's as-built BOM +
//                                 as-run routing (CrystallizationFingerprint).
//   2) Create crystallization stub — persist an ItemCrystallization (Outcome=
//                                 Pending) with the computed fingerprint +
//                                 frozen as-built lineage + two-phase
//                                 CRYST-YYYY-NNNNNN number.
//   3) Reverse latest stub     — set IsReversed on the most recent record.
//   R) Reload stats
//
// PR-5 adds the IItemCrystallizationService (preview / crystallize / dedupe /
// reverse) — this probe proves the SCHEMA, the fingerprint determinism, the
// two-phase numbering, and tenant scoping first. All writes go through
// AppDbContext (tenant-scoped via ITenantContext.VisibleCompanyIds on every
// read and write), which is why this probe is ControlPlaneExempt.

using System;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B7 PR-4 ItemCrystallization (the crystallization-at-ship " +
    "audit record). Writes the ItemCrystallization table directly via AppDbContext, tenant-scoped " +
    "through ITenantContext.VisibleCompanyIds on every read and write. PR-5 adds the service layer.")]
public sealed class CrystallizationFingerprintProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CrystallizationFingerprintProbeModel> _logger;

    public CrystallizationFingerprintProbeModel(
        AppDbContext db,
        ITenantContext tenant,
        ILogger<CrystallizationFingerprintProbeModel> logger)
    {
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    [BindProperty] public int ProId { get; set; }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalCrystallizations { get; private set; }
    public int PendingCount { get; private set; }
    public int ReversedCount { get; private set; }
    public IReadOnlyList<Row> Sample { get; private set; } = Array.Empty<Row>();

    public sealed record Row(
        int Id, string Number, int SourceProId, CrystallizationOutcome Outcome,
        string? Fingerprint, string? AsBuiltPartNumber, bool IsReversed, DateTime CreatedAtUtc);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<ItemCrystallization> Scoped() =>
        _db.ItemCrystallizations.Where(c => _tenant.VisibleCompanyIds.Contains(c.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalCrystallizations = await Scoped().CountAsync(ct);
        PendingCount = await Scoped().CountAsync(c => c.Outcome == CrystallizationOutcome.Pending, ct);
        ReversedCount = await Scoped().CountAsync(c => c.IsReversed, ct);

        Sample = await Scoped()
            .OrderByDescending(c => c.Id)
            .Take(15)
            .Select(c => new Row(
                c.Id, c.CrystallizationNumber, c.SourceProductionOrderId, c.Outcome,
                c.StructureFingerprintHash, c.AsBuiltPartNumber, c.IsReversed, c.CreatedAtUtc))
            .ToListAsync(ct);
    }

    // Resolve the target PRO id (input, or default to the latest tenant-visible PoFirst PRO).
    private async Task<int?> ResolveProIdAsync(CancellationToken ct)
    {
        if (ProId > 0) return ProId;
        return await _db.ProductionOrders
            .Where(p => _tenant.VisibleCompanyIds.Contains(p.CompanyId) && p.IsPoFirst)
            .OrderByDescending(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<ProductionOrder?> LoadVisibleProAsync(int proId, CancellationToken ct) =>
        await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == proId && _tenant.VisibleCompanyIds.Contains(p.CompanyId), ct);

    private async Task<string> ComputeFingerprintAsync(int proId, CancellationToken ct)
    {
        var bom = await _db.ProductionMaterialStructures
            .AsNoTracking()
            .Where(s => s.ProductionOrderId == proId)
            .ToListAsync(ct);
        var ops = await _db.ProductionOperations
            .AsNoTracking()
            .Where(o => o.ProductionOrderId == proId)
            .ToListAsync(ct);
        return CrystallizationFingerprint.Compute(bom, ops);
    }

    // 1) COMPUTE fingerprint (read — surfaces the dedupe key + line counts)
    public async Task<IActionResult> OnPostComputeFingerprintAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id (or create a PoFirst PRO first)."); await LoadStatsAsync(ct); return Page(); }
        var pro = await LoadVisibleProAsync(id.Value, ct);
        if (pro == null) { Set(false, $"PRO {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var bomCount = await _db.ProductionMaterialStructures.CountAsync(s => s.ProductionOrderId == id.Value, ct);
        var opCount = await _db.ProductionOperations.CountAsync(o => o.ProductionOrderId == id.Value, ct);
        var fp = await ComputeFingerprintAsync(id.Value, ct);

        Set(true, $"PRO #{id} ({pro.OrderNumber}) structural fingerprint = {fp} " +
                  $"(over {bomCount} as-built BOM line(s) + {opCount} as-run operation(s)). " +
                  "Identical structures hash identically — that equality is the dedupe key.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) CREATE crystallization stub (WRITE)
    public async Task<IActionResult> OnPostCreateStubAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id (or create a PoFirst PRO first)."); await LoadStatsAsync(ct); return Page(); }
        var pro = await LoadVisibleProAsync(id.Value, ct);
        if (pro == null) { Set(false, $"PRO {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var by = User.Identity?.Name ?? "B7-PR4-probe";
        var fp = await ComputeFingerprintAsync(id.Value, ct);
        var nowUtc = DateTime.UtcNow;

        // Two-phase numbering — placeholder first, patch after Id is assigned.
        var rec = new ItemCrystallization
        {
            CompanyId = pro.CompanyId,
            SiteId = pro.LocationId,
            SourceProductionOrderId = pro.Id,
            CrystallizationNumber = $"CRYST-PEND-{Guid.NewGuid():N}",
            Outcome = CrystallizationOutcome.Pending,
            StructureFingerprintHash = fp,
            CostSource = CrystallizationCostSource.FirstActual,
            // Freeze the as-built configuration lineage from the PoFirst order's
            // as-planned identity (the compliance anchor — PR-5 enforces that a
            // mint cannot contradict these).
            AsBuiltPartNumber = pro.AsPlannedPartNumber,
            AsBuiltDrawingNumber = pro.AsPlannedDrawingNumber,
            AsBuiltDrawingRev = pro.AsPlannedDrawingRev,
            RationaleText = $"PR-4 probe stub for PRO {pro.OrderNumber}: fingerprinted as-built structure, " +
                            "outcome Pending (PR-5 service resolves to CreatedNewItem / LinkedToExisting / Rejected).",
            CrystallizedAtUtc = nowUtc,
            CrystallizedBy = by,
            CreatedAtUtc = nowUtc,
            CreatedBy = by,
        };
        _db.ItemCrystallizations.Add(rec);
        await _db.SaveChangesAsync(ct);

        rec.CrystallizationNumber = $"CRYST-{nowUtc:yyyy}-{rec.Id:D6}";
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "B7-PR4 probe: created ItemCrystallization #{Id} {Num} for PRO {ProId} (fingerprint {Fp}).",
            rec.Id, rec.CrystallizationNumber, pro.Id, fp);

        Set(true, $"Crystallization stub {rec.CrystallizationNumber} created for PRO #{id} " +
                  $"(Outcome=Pending, fingerprint {fp[..12]}…). PR-5 will resolve it to a minted master or a deduped link.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) REVERSE latest stub (WRITE)
    public async Task<IActionResult> OnPostReverseLatestAsync(CancellationToken ct)
    {
        // Scope to PENDING stubs only (Codex P2). This probe must never flip
        // IsReversed on a real CreatedNewItem / LinkedToExisting crystallization
        // (which PR-5's service writes) — that would mark the audit row reversed
        // while leaving the minted item / dedupe link active. Reversing a real
        // crystallization is the service's job (ReverseCrystallizationAsync),
        // which also undoes the master-data change.
        var rec = await Scoped()
            .Where(c => !c.IsReversed && c.Outcome == CrystallizationOutcome.Pending)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);
        if (rec == null) { Set(false, "No un-reversed Pending stub in your tenant scope — create a stub first (button 2)."); await LoadStatsAsync(ct); return Page(); }

        var by = User.Identity?.Name ?? "B7-PR4-probe";
        rec.IsReversed = true;
        rec.ReversedAtUtc = DateTime.UtcNow;
        rec.ReversedBy = by;
        rec.ReversalReason = "PR-4 probe reversal demo — reversible, never rewrites as-built history.";
        rec.UpdatedAtUtc = DateTime.UtcNow;
        rec.UpdatedBy = by;
        await _db.SaveChangesAsync(ct);

        Set(true, $"Crystallization {rec.CrystallizationNumber} reversed (the as-built source records are untouched).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R) RELOAD
    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Stats reloaded.");
        return Page();
    }
}
