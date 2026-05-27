using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B8 PR-PRO-7 (2026-05-27) — admin probe for IOperationReadinessService.
// "Can I Run This?" 8-check readiness engine + Material Supply Link management.
//
// NINE WRITE BUTTONS per Lock 16 corollary:
//   1. Check Operation Readiness (READ — 8 checks)
//   2. Check Full PRO Readiness (READ — all ops)
//   3. Link Supply (CREATE INSERT)
//   4. Unlink Supply (UPDATE — clears fields)
//   5. Update Supply Status (UPDATE)
//   6. Refresh Supply Links (UPDATE — recompute derived fields)
//   7. Get Supply Links for Op (READ)
//   8. Get Supply Links for PRO (READ)
//   9. Check Material Readiness (READ — detailed BOM-line analysis)
[Authorize(Roles = "Admin")]
public sealed class OperationReadinessProbeModel : PageModel
{
    private readonly IOperationReadinessService _svc;
    private readonly ILogger<OperationReadinessProbeModel> _logger;

    public OperationReadinessProbeModel(IOperationReadinessService svc, ILogger<OperationReadinessProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Check Operation ---
    [BindProperty] public int CheckOpId { get; set; } = 1;

    // --- Check PRO ---
    [BindProperty] public int CheckProId { get; set; } = 1;

    // --- Link Supply ---
    [BindProperty] public int LinkCompanyId { get; set; } = 1;
    [BindProperty] public int LinkBomLineId { get; set; } = 1;
    [BindProperty] public MaterialSupplyType LinkSupplyType { get; set; } = MaterialSupplyType.PurchaseToJob;
    [BindProperty] public LinkedSupplyRecordType LinkRecordType { get; set; } = LinkedSupplyRecordType.PurchaseOrder;
    [BindProperty] public int? LinkRecordId { get; set; } = 1;
    [BindProperty] public int? LinkLineId { get; set; } = 1;
    [BindProperty] public string? LinkRecordNumber { get; set; } = "PO 45678-001";
    [BindProperty] public string? LinkSupplier { get; set; } = "SKF USA Inc. — Gothenburg Distribution";
    [BindProperty] public string? LinkBuyer { get; set; } = "M. Chen — Strategic Procurement, Bearings";
    [BindProperty] public DateTime? LinkRequiredDate { get; set; } = DateTime.UtcNow.AddDays(14);
    [BindProperty] public DateTime? LinkPromisedDate { get; set; } = DateTime.UtcNow.AddDays(12);
    [BindProperty] public decimal LinkQtyRequired { get; set; } = 50;
    [BindProperty] public decimal LinkQtySupplied { get; set; } = 50;
    [BindProperty] public string? LinkNotes { get; set; } = "SKF 6205-2RSH deep groove ball bearings, C3 clearance, sealed both sides.";

    // --- Unlink ---
    [BindProperty] public int UnlinkBomLineId { get; set; }

    // --- Update Status ---
    [BindProperty] public int UpdBomLineId { get; set; } = 1;
    [BindProperty] public MaterialSupplyStatus UpdStatus { get; set; } = MaterialSupplyStatus.Ordered;
    [BindProperty] public SupplyRisk UpdRisk { get; set; } = SupplyRisk.Warning;
    [BindProperty] public decimal UpdQtyReceived { get; set; } = 38;
    [BindProperty] public decimal UpdQtyRemaining { get; set; } = 12;
    [BindProperty] public bool UpdLateToNeed { get; set; }
    [BindProperty] public string? UpdNotes { get; set; } = "Partial receipt — 38/50. Balance ETA June 12.";

    // --- Refresh ---
    [BindProperty] public int RefreshProId { get; set; } = 1;

    // --- Get Links ---
    [BindProperty] public int GetOpLinksId { get; set; } = 1;
    [BindProperty] public int GetProLinksId { get; set; } = 1;

    // --- Material Check ---
    [BindProperty] public int MatCheckOpId { get; set; } = 1;

    // --- Outcome ---
    public string? Outcome { get; set; }
    public bool OutcomeIsError { get; set; }

    public void OnGet() { }

    // 1. Check single operation
    public async Task<IActionResult> OnPostCheckOperation(CancellationToken ct)
    {
        var r = await _svc.CheckOperationReadinessAsync(CheckOpId, ct);
        if (r.IsFailure) { Outcome = r.Error; OutcomeIsError = true; return Page(); }
        var v = r.Value!;
        var sb = new StringBuilder();
        sb.AppendLine($"Op {v.SequenceNumber} ({v.OperationDescription}) — Overall: {v.OverallStatus}");
        foreach (var c in v.Checks)
            sb.AppendLine($"  [{c.Status}] {c.CheckName}: {c.Description}");
        if (v.MaterialDetails.Count > 0)
        {
            sb.AppendLine($"  Material details ({v.MaterialDetails.Count} at-risk/blocked lines):");
            foreach (var m in v.MaterialDetails)
                sb.AppendLine($"    {m.ChildPartNumber}: {m.Description}");
        }
        Outcome = sb.ToString();
        return Page();
    }

    // 2. Check full PRO
    public async Task<IActionResult> OnPostCheckOrder(CancellationToken ct)
    {
        var r = await _svc.CheckOrderReadinessAsync(CheckProId, ct);
        if (r.IsFailure) { Outcome = r.Error; OutcomeIsError = true; return Page(); }
        var v = r.Value!;
        var sb = new StringBuilder();
        sb.AppendLine($"PRO {v.OrderNumber} — Overall: {v.OverallStatus} (Pass:{v.PassCount} Warn:{v.WarningCount} Fail:{v.FailCount})");
        foreach (var op in v.Operations)
        {
            sb.AppendLine($"  Op {op.SequenceNumber} ({op.OperationDescription}): {op.OverallStatus}");
            foreach (var c in op.Checks.Where(c => c.Status != ReadinessStatus.Pass))
                sb.AppendLine($"    [{c.Status}] {c.CheckName}: {c.Description}");
        }
        Outcome = sb.ToString();
        return Page();
    }

    // 3. Link supply
    public async Task<IActionResult> OnPostLinkSupply(CancellationToken ct)
    {
        var r = await _svc.LinkSupplyAsync(new LinkSupplyRequest(
            LinkCompanyId, LinkBomLineId, LinkSupplyType, LinkRecordType,
            LinkRecordId, LinkLineId, LinkRecordNumber, LinkSupplier, LinkBuyer,
            LinkRequiredDate, LinkPromisedDate, LinkQtyRequired, LinkQtySupplied,
            LinkNotes, "admin-probe"), ct);
        if (r.IsFailure) { Outcome = r.Error; OutcomeIsError = true; return Page(); }
        var v = r.Value!;
        Outcome = $"Linked BOM line {v.Id} ({v.ChildPartNumber}) to {v.LinkedSupplyRecordType} " +
                  $"{v.LinkedSupplyRecordNumber}. Status: {v.MaterialSupplyStatus}, Risk: {v.SupplyRisk}.";
        return Page();
    }

    // 4. Unlink supply
    public async Task<IActionResult> OnPostUnlinkSupply(CancellationToken ct)
    {
        var r = await _svc.UnlinkSupplyAsync(UnlinkBomLineId, ct);
        if (r.IsFailure) { Outcome = r.Error; OutcomeIsError = true; return Page(); }
        Outcome = $"Unlinked supply from BOM line {UnlinkBomLineId}. Status reset to Available.";
        return Page();
    }

    // 5. Update status
    public async Task<IActionResult> OnPostUpdateStatus(CancellationToken ct)
    {
        var r = await _svc.UpdateSupplyStatusAsync(new UpdateSupplyStatusRequest(
            UpdBomLineId, UpdStatus, UpdRisk, UpdQtyReceived, UpdQtyRemaining,
            null, UpdLateToNeed, UpdNotes, "admin-probe"), ct);
        if (r.IsFailure) { Outcome = r.Error; OutcomeIsError = true; return Page(); }
        var v = r.Value!;
        Outcome = $"Updated BOM line {v.Id} ({v.ChildPartNumber}). Status: {v.MaterialSupplyStatus}, " +
                  $"Risk: {v.SupplyRisk}. Received: {v.SupplyQuantityReceived}, Remaining: {v.SupplyQuantityRemaining}.";
        return Page();
    }

    // 6. Refresh supply links
    public async Task<IActionResult> OnPostRefreshSupply(CancellationToken ct)
    {
        var r = await _svc.RefreshSupplyLinksAsync(RefreshProId, ct);
        if (r.IsFailure) { Outcome = r.Error; OutcomeIsError = true; return Page(); }
        Outcome = $"Refreshed {r.Value} supply links for PRO {RefreshProId}.";
        return Page();
    }

    // 7. Get links for operation
    public async Task<IActionResult> OnPostGetOpLinks(CancellationToken ct)
    {
        var links = await _svc.GetSupplyLinksForOperationAsync(GetOpLinksId, ct);
        if (links.Count == 0) { Outcome = $"No BOM lines for operation {GetOpLinksId}."; return Page(); }
        var sb = new StringBuilder();
        sb.AppendLine($"{links.Count} BOM line(s) for Op {GetOpLinksId}:");
        foreach (var l in links)
            sb.AppendLine($"  [{l.Id}] {l.ChildPartNumber} — Supply: {l.MaterialSupplyStatus}, " +
                          $"Risk: {l.SupplyRisk}, Linked: {l.LinkedSupplyRecordType} {l.LinkedSupplyRecordNumber ?? "(none)"}");
        Outcome = sb.ToString();
        return Page();
    }

    // 8. Get links for PRO
    public async Task<IActionResult> OnPostGetProLinks(CancellationToken ct)
    {
        var links = await _svc.GetSupplyLinksForOrderAsync(GetProLinksId, ct);
        if (links.Count == 0) { Outcome = $"No BOM lines for PRO {GetProLinksId}."; return Page(); }
        var sb = new StringBuilder();
        sb.AppendLine($"{links.Count} BOM line(s) for PRO {GetProLinksId}:");
        foreach (var l in links)
            sb.AppendLine($"  [{l.Id}] Seq {l.Sequence} — {l.ChildPartNumber}, Op {l.ConsumingOperationSequence}, " +
                          $"Supply: {l.MaterialSupplyStatus}, Risk: {l.SupplyRisk}");
        Outcome = sb.ToString();
        return Page();
    }

    // 9. Check material readiness
    public async Task<IActionResult> OnPostCheckMaterials(CancellationToken ct)
    {
        var r = await _svc.CheckMaterialReadinessAsync(MatCheckOpId, ct);
        if (r.IsFailure) { Outcome = r.Error; OutcomeIsError = true; return Page(); }
        var details = r.Value!;
        if (details.Count == 0) { Outcome = $"All materials ready for operation {MatCheckOpId} (or no materials assigned)."; return Page(); }
        var sb = new StringBuilder();
        sb.AppendLine($"{details.Count} material issue(s) for Op {MatCheckOpId}:");
        foreach (var m in details)
            sb.AppendLine($"  {m.ChildPartNumber}: [{m.SupplyStatus}/{m.Risk}] {m.Description}");
        Outcome = sb.ToString();
        return Page();
    }
}
