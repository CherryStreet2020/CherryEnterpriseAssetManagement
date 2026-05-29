// Theme B7 Wave A PR-2 — admin probe for the master-optional (PoFirst) PO
// release path. Exercises the new ProductionOrder fields (IsPoFirst,
// AsPlanned* identity, CrystallizedItemId) AND the two new guards:
//   • PoFirst release readiness  (drawing # + rev mandatory at release)
//   • PoFirst-has-no-principal-item (master crystallizes at ship, not at create)
//
// All mutations go through IProductionOrderService (canonical write path);
// the BOM-line seed + CrystallizedItemId set write through AppDbContext
// directly (tenant-scoped), which is why this probe is ControlPlaneExempt.
// Lock-16 corollary: every button exercises a write.
//
//   1) Create a master-less PoFirst PRO (no item, NO drawing yet)
//   2) Attempt release without a drawing  → guard rejection demo
//   3) Set as-planned drawing # + rev, then release → success
//   4) Generate supply demands from the master-less PRO (proves the supply
//      flow runs off the PO BOM independent of a header Item Master)
//   5) Set CrystallizedItemId (simulate the Wave-B crystallization link)
//   R) Reload stats

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
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
    "Admin diagnostic probe for Theme B7 PR-2 master-optional (PoFirst) PO release. " +
    "ProductionOrder mutations route through IProductionOrderService; the BOM-line " +
    "seed + CrystallizedItemId set write via AppDbContext, tenant-scoped through " +
    "ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class MasterOptionalReleaseProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionOrderService _orders;
    private readonly IProductionSupplyDemandService _demands;
    private readonly ILogger<MasterOptionalReleaseProbeModel> _logger;

    public MasterOptionalReleaseProbeModel(
        AppDbContext db,
        ITenantContext tenant,
        IProductionOrderService orders,
        IProductionSupplyDemandService demands,
        ILogger<MasterOptionalReleaseProbeModel> logger)
    {
        _db = db;
        _tenant = tenant;
        _orders = orders;
        _demands = demands;
        _logger = logger;
    }

    // ── Inputs ──
    [BindProperty] public int ProductionOrderId { get; set; }
    [BindProperty] public string AsPlannedDrawingNumber { get; set; } = "DWG-ETO-TI64-44120";
    [BindProperty] public string AsPlannedDrawingRev { get; set; } = "B";

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int PoFirstCount { get; private set; }
    public int MasterlessCount { get; private set; }     // IsPoFirst && ItemId == null
    public int PoFirstReleasedCount { get; private set; }
    public int PoFirstCrystallizedCount { get; private set; }
    public int? LatestPoFirstId { get; private set; }

    public IReadOnlyList<ProRow> SamplePoFirst { get; private set; } = Array.Empty<ProRow>();

    public sealed record ProRow(
        int Id, string OrderNumber, ProductionOrderStatus Status, bool IsPoFirst,
        bool HasMaster, string? DrawingNumber, string? DrawingRev,
        string? AsPlannedPartNumber, int? CrystallizedItemId);

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<ProductionOrder> ScopedOrders() =>
        _db.ProductionOrders.Where(p => _tenant.VisibleCompanyIds.Contains(p.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        PoFirstCount = await ScopedOrders().CountAsync(p => p.IsPoFirst, ct);
        MasterlessCount = await ScopedOrders().CountAsync(p => p.IsPoFirst && p.ItemId == null, ct);
        PoFirstReleasedCount = await ScopedOrders()
            .CountAsync(p => p.IsPoFirst && p.Status != ProductionOrderStatus.Planned
                          && p.Status != ProductionOrderStatus.Firmed, ct);
        PoFirstCrystallizedCount = await ScopedOrders()
            .CountAsync(p => p.IsPoFirst && p.CrystallizedItemId != null, ct);

        LatestPoFirstId = await ScopedOrders()
            .Where(p => p.IsPoFirst)
            .OrderByDescending(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

        SamplePoFirst = await ScopedOrders()
            .Where(p => p.IsPoFirst)
            .OrderByDescending(p => p.Id)
            .Take(15)
            .Select(p => new ProRow(
                p.Id, p.OrderNumber, p.Status, p.IsPoFirst, p.ItemId != null,
                p.AsPlannedDrawingNumber, p.AsPlannedDrawingRev,
                p.AsPlannedPartNumber, p.CrystallizedItemId))
            .ToListAsync(ct);
    }

    // Resolve a tenant-visible Location so CreateAsync can tenant-scope the PRO.
    private async Task<int?> FirstVisibleLocationIdAsync(CancellationToken ct) =>
        await _db.Locations
            .Where(l => l.CompanyId != null && _tenant.VisibleCompanyIds.Contains(l.CompanyId.Value))
            .OrderBy(l => l.Id)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync(ct);

    private async Task<(int Id, string PartNumber)?> FirstVisibleItemAsync(CancellationToken ct)
    {
        var row = await _db.Items
            .Where(i => i.CompanyId == null || _tenant.VisibleCompanyIds.Contains(i.CompanyId ?? 0))
            .OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.PartNumber })
            .FirstOrDefaultAsync(ct);
        return row == null ? null : (row.Id, row.PartNumber);
    }

    private async Task<ProductionOrder?> LatestPoFirstAsync(CancellationToken ct) =>
        await ScopedOrders().Where(p => p.IsPoFirst)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

    private static string NewOrderNumber() =>
        $"PRO-POF-{DateTime.UtcNow:yyMMdd-HHmmss}";

    // Real ETO fixture — a 5-axis-machined Ti-6Al-4V engine-mount bracket built
    // to a customer drawing, no catalog Item Master (crystallizes at ship).
    private CreateProductionOrderRequest BuildPoFirstRequest(
        int locationId, bool withDrawing) =>
        new(
            OrderNumber: NewOrderNumber(),
            Type: ProductionType.JobShop,
            Title: "ETO engine-mount bracket — Ti-6Al-4V (PO-as-Standard)",
            Description: "Master-optional ETO build; standard crystallizes at ship.",
            ItemId: null,                       // master-optional
            LocationId: locationId,
            CustomerId: null,
            QuantityOrdered: 4m,
            Uom: "EA",
            ScheduledStart: DateTime.UtcNow.Date,
            ScheduledEnd: DateTime.UtcNow.Date.AddDays(21),
            Priority: 50,
            MasterProductionOrderId: null,
            MaterialStructureId: null,
            CreatedBy: "B7-PR2-probe",
            IsPoFirst: true,
            AsPlannedPartNumber: "ETO-BRKT-TI64-44120",
            AsPlannedDrawingNumber: withDrawing ? "DWG-ETO-TI64-44120" : null,
            AsPlannedDrawingRev: withDrawing ? "B" : null,
            AsPlannedDescription: "Engine-mount bracket, Ti-6Al-4V, 5-axis, NADCAP anodize.");

    // 1) CREATE master-less PoFirst PRO (no item, NO drawing yet)
    public async Task<IActionResult> OnPostCreatePoFirstAsync(CancellationToken ct)
    {
        var locId = await FirstVisibleLocationIdAsync(ct);
        if (locId == null)
        {
            Set(false, "No tenant-visible Location found — seed a Location first.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var result = await _orders.CreateAsync(BuildPoFirstRequest(locId.Value, withDrawing: false), ct);
        if (result.IsFailure)
            Set(false, $"Create failed: {result.Error}");
        else
            Set(true, $"Created master-optional PoFirst PRO #{result.Value!.Id} " +
                      $"({result.Value.OrderNumber}) — ItemId is null, no drawing yet. " +
                      "Use button 2 to see the release guard fire, or 3 to set a drawing + release.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) ATTEMPT release without a drawing (guard rejection demo) — self-contained
    public async Task<IActionResult> OnPostAttemptReleaseNoDrawingAsync(CancellationToken ct)
    {
        var locId = await FirstVisibleLocationIdAsync(ct);
        if (locId == null)
        {
            Set(false, "No tenant-visible Location found — seed a Location first.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var create = await _orders.CreateAsync(BuildPoFirstRequest(locId.Value, withDrawing: false), ct);
        if (create.IsFailure)
        {
            Set(false, $"Setup create failed: {create.Error}");
            await LoadStatsAsync(ct);
            return Page();
        }

        var release = await _orders.UpdateStatusAsync(
            new UpdateProductionOrderStatusRequest(create.Value!.Id, ProductionOrderStatus.Released, "B7-PR2-probe"), ct);

        if (release.IsFailure)
            Set(true, $"Guard correctly REJECTED release of PoFirst PRO #{create.Value.Id} " +
                      $"({create.Value.OrderNumber}): {release.Error}");
        else
            Set(false, $"UNEXPECTED: PoFirst PRO #{create.Value.Id} released without a drawing — " +
                       "the release readiness guard did not fire.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) SET drawing # + rev, then release (success)
    public async Task<IActionResult> OnPostSetDrawingAndReleaseAsync(CancellationToken ct)
    {
        var po = ProductionOrderId > 0
            ? await ScopedOrders().FirstOrDefaultAsync(p => p.Id == ProductionOrderId, ct)
            : await LatestPoFirstAsync(ct);
        if (po == null)
        {
            Set(false, "No PoFirst PRO found — create one with button 1 first.");
            await LoadStatsAsync(ct);
            return Page();
        }

        // Full-header overwrite contract — re-supply current values + the new drawing.
        var header = await _orders.UpdateHeaderAsync(new UpdateProductionOrderHeaderRequest(
            ProductionOrderId: po.Id,
            Title: po.Title,
            Description: po.Description,
            ItemId: null,                          // PoFirst — no principal item
            LocationId: po.LocationId,
            CustomerId: po.CustomerId,
            QuantityOrdered: po.QuantityOrdered,
            Uom: po.Uom,
            ScheduledStart: po.ScheduledStart,
            ScheduledEnd: po.ScheduledEnd,
            Priority: po.Priority,
            MasterProductionOrderId: po.MasterProductionOrderId,
            MaterialStructureId: po.MaterialStructureId,
            ModifiedBy: "B7-PR2-probe",
            IsPoFirst: true,
            AsPlannedPartNumber: po.AsPlannedPartNumber,
            AsPlannedDrawingNumber: string.IsNullOrWhiteSpace(AsPlannedDrawingNumber) ? "DWG-ETO-TI64-44120" : AsPlannedDrawingNumber,
            AsPlannedDrawingRev: string.IsNullOrWhiteSpace(AsPlannedDrawingRev) ? "B" : AsPlannedDrawingRev,
            AsPlannedDescription: po.AsPlannedDescription), ct);
        if (header.IsFailure)
        {
            Set(false, $"Set-drawing failed: {header.Error}");
            await LoadStatsAsync(ct);
            return Page();
        }

        var release = await _orders.UpdateStatusAsync(
            new UpdateProductionOrderStatusRequest(po.Id, ProductionOrderStatus.Released, "B7-PR2-probe"), ct);
        if (release.IsFailure)
            Set(false, $"Release still rejected after setting drawing: {release.Error}");
        else
            Set(true, $"PoFirst PRO #{po.Id} ({po.OrderNumber}) released with as-planned " +
                      $"drawing {header.Value!.AsPlannedDrawingNumber} Rev {header.Value.AsPlannedDrawingRev} — " +
                      $"status now {release.Value!.Status}. No Item Master required.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) GENERATE demands from the master-less PRO (supply flows off the PO BOM)
    public async Task<IActionResult> OnPostGenerateDemandsAsync(CancellationToken ct)
    {
        var po = ProductionOrderId > 0
            ? await ScopedOrders().FirstOrDefaultAsync(p => p.Id == ProductionOrderId, ct)
            : await LatestPoFirstAsync(ct);
        if (po == null)
        {
            Set(false, "No PoFirst PRO found — create one with button 1 first.");
            await LoadStatsAsync(ct);
            return Page();
        }

        // Seed one BOM line if the PRO has none, so GenerateDemands has something
        // to walk. ChildItemId is required (the component IS catalogued — a
        // master-optional ASSEMBLY can still consume catalog raw stock like
        // Ti-6Al-4V bar). The point being proven: the PRO HEADER carries no
        // master (ItemId null) yet the supply flow still runs.
        var hasLine = await _db.Set<ProductionMaterialStructure>()
            .AnyAsync(b => b.ProductionOrderId == po.Id, ct);
        if (!hasLine)
        {
            var child = await FirstVisibleItemAsync(ct);
            if (child == null)
            {
                Set(false, "No tenant-visible Item to use as a BOM component — seed an Item first.");
                await LoadStatsAsync(ct);
                return Page();
            }

            _db.Set<ProductionMaterialStructure>().Add(new ProductionMaterialStructure
            {
                CompanyId = po.CompanyId,
                ProductionOrderId = po.Id,
                ChildItemId = child.Value.Id,
                ChildPartNumber = string.IsNullOrWhiteSpace(child.Value.PartNumber)
                    ? "TI64-BAR-1.500" : child.Value.PartNumber,
                ChildRevision = "A",
                Sequence = 10,
                QuantityPer = 2m,
                Uom = "EA",
                MaterialSupplyType = MaterialSupplyType.PurchaseToJob,
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }

        var gen = await _demands.GenerateDemandsFromProAsync(po.Id, "B7-PR2-probe", ct);
        if (gen.IsFailure)
            Set(false, $"Demand generation failed: {gen.Error}");
        else
            Set(true, $"Master-less PoFirst PRO #{po.Id} ({po.OrderNumber}, ItemId=null): " +
                      $"{gen.Value!.Message} — supply flows off the PO BOM, no header Item Master needed.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) SET CrystallizedItemId (simulate the Wave-B crystallization link)
    public async Task<IActionResult> OnPostSetCrystallizedAsync(CancellationToken ct)
    {
        var po = ProductionOrderId > 0
            ? await ScopedOrders().FirstOrDefaultAsync(p => p.Id == ProductionOrderId, ct)
            : await LatestPoFirstAsync(ct);
        if (po == null)
        {
            Set(false, "No PoFirst PRO found — create one with button 1 first.");
            await LoadStatsAsync(ct);
            return Page();
        }

        var item = await FirstVisibleItemAsync(ct);
        if (item == null)
        {
            Set(false, "No tenant-visible Item to crystallize into — seed an Item first.");
            await LoadStatsAsync(ct);
            return Page();
        }

        po.CrystallizedItemId = item.Value.Id;
        po.ModifiedAt = DateTime.UtcNow;
        po.ModifiedBy = "B7-PR2-probe";
        await _db.SaveChangesAsync(ct);
        Set(true, $"PoFirst PRO #{po.Id} ({po.OrderNumber}) CrystallizedItemId → " +
                  $"#{item.Value.Id} ({item.Value.PartNumber}). (Wave B does this for real at ship.)");
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
