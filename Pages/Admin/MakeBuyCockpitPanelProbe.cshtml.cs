// Theme B7 Wave D PR-1 (2026-05-30) — admin probe for the Make-or-Buy Cockpit panel.
//
// Deterministic Lock-16 surface for _CockpitMakeBuyPanel. It seeds a worked example
// (MakeOrBuy item + fully-loaded standard cost + competitive supplier quote — same
// recipe as the PR-8 engine probe), persists a real MakeBuyDecision, then renders
// the SAME partial the live Production Cockpit Make/Buy tab uses — so the panel UX
// is exercised end-to-end independent of seed data.
//
//   1) Set up worked example   — writes (item flag + cost elements + RFQ/quote)
//   2) Decide & persist + show  — writes a MakeBuyDecision, renders the panel (BUY on cost)
//   3) Source-control + persist — flips IsSourceControlled → forced MAKE (hard gate), renders
//   4) Explain latest           — re-hydrates the most recent decision via ExplainAsync, renders
//   Reload                      — read-only refresh (siblings write → Lock-16 satisfied)

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Production;
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
    "Admin diagnostic probe for Theme B7 Wave D PR-1 Make-or-Buy Cockpit panel. Seeds a worked " +
    "example (MakeOrBuy item + standard cost + supplier quote) and renders _CockpitMakeBuyPanel " +
    "via IMakeBuyDecisionService, tenant-scoped. Writes route through the service / idempotent helpers.")]
public sealed class MakeBuyCockpitPanelProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMakeBuyDecisionService _engine;
    private readonly ILogger<MakeBuyCockpitPanelProbeModel> _logger;

    public MakeBuyCockpitPanelProbeModel(AppDbContext db, ITenantContext tenant,
        IMakeBuyDecisionService engine, ILogger<MakeBuyCockpitPanelProbeModel> logger)
    {
        _db = db; _tenant = tenant; _engine = engine; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public int CompanyId { get; private set; }
    public int? ProbeItemId { get; private set; }
    public bool ProbeSourceControlled { get; private set; }

    /// <summary>The rendered panel — the actual cockpit partial input.</summary>
    public MakeBuyCockpitPanelModel? PanelData { get; private set; }

    private const string RfqNumber = "MBCKPTPROBE-RFQ";
    private const decimal Qty = 50m;

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
    private int Company() => _tenant.VisibleCompanyIds.FirstOrDefault();
    private DateTime Due() => DateTime.UtcNow.Date.AddDays(21);

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        CompanyId = Company();
        ProbeItemId = await ProbeItemIdAsync(ct);
        if (ProbeItemId != null)
            ProbeSourceControlled = await _db.Items.Where(i => i.Id == ProbeItemId)
                .Select(i => i.IsSourceControlled).FirstOrDefaultAsync(ct);
    }

    private async Task<int?> ProbeItemIdAsync(CancellationToken ct) => await (
        from r in _db.SupplierRFQs
        where r.RfqNumber == RfqNumber && r.CompanyId == Company()
        join rl in _db.SupplierRFQLines on r.Id equals rl.SupplierRFQId
        where rl.ItemId != null
        select rl.ItemId).FirstOrDefaultAsync(ct);

    // ── 1) Setup (mirrors the PR-8 engine probe recipe) ──────────────────────
    public async Task<IActionResult> OnPostSetupAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company."); await LoadStatsAsync(ct); return Page(); }

        var pick = await (
            from rt in _db.Routings
            where rt.CompanyId == companyId
            join i in _db.Items on rt.ItemId equals i.Id
            orderby rt.IsDefault descending, rt.Id descending
            select new { ItemId = i.Id }).FirstOrDefaultAsync(ct);
        if (pick == null) { Set(false, "No routing-bearing item in this company — seed a routing first."); await LoadStatsAsync(ct); return Page(); }
        var itemId = pick.ItemId;

        var vendorId = await _db.Vendors.Where(v => v.CompanyId == companyId).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct)
                       ?? await _db.Vendors.Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (vendorId == null) { Set(false, "No Vendor to quote from — seed a vendor first."); await LoadStatsAsync(ct); return Page(); }

        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        item!.MakeBuyCode = MakeBuyCode.MakeOrBuy;
        item.MakeBuyPolicy = MakeBuyPolicy.Inherit;
        item.IsSourceControlled = false;
        await _db.SaveChangesAsync(ct);

        // Fully-loaded standard cost = $195/u (make).
        await EnsureCostAsync(itemId, companyId, CostElementType.Material, 80m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.Labor, 40m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.VariableOverhead, 20m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.Setup, 30m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.Tooling, 10m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.FixedOverhead, 15m, ct);

        // RFQ + line + competitive quote ($150/u, landed ~$157.50 < make $195 → BUY on cost).
        var rfq = await _db.SupplierRFQs.FirstOrDefaultAsync(r => r.RfqNumber == RfqNumber && r.CompanyId == companyId, ct);
        if (rfq == null)
        {
            rfq = new SupplierRFQ { CompanyId = companyId, RfqNumber = RfqNumber, Title = "Make-or-buy cockpit panel probe RFQ", Status = RfqStatus.Evaluated };
            _db.SupplierRFQs.Add(rfq);
            await _db.SaveChangesAsync(ct);
        }
        var rline = await _db.SupplierRFQLines.FirstOrDefaultAsync(l => l.SupplierRFQId == rfq.Id && l.ItemId == itemId, ct);
        if (rline == null)
        {
            rline = new SupplierRFQLine { SupplierRFQId = rfq.Id, LineNumber = 1, ItemId = itemId, Description = "Probe component", Quantity = Qty, UOM = "EA" };
            _db.SupplierRFQLines.Add(rline);
            await _db.SaveChangesAsync(ct);
        }
        var quote = await _db.SupplierQuotes.FirstOrDefaultAsync(q => q.SupplierRFQId == rfq.Id && q.VendorId == vendorId, ct);
        if (quote == null)
        {
            quote = new SupplierQuote
            {
                CompanyId = companyId, SupplierRFQId = rfq.Id, VendorId = vendorId.Value,
                Status = SupplierQuoteStatus.Received, ValidUntilDate = DateTime.UtcNow.Date.AddDays(60),
                LeadTimeDays = 10, TotalQuotedAmount = 150m * Qty, SupplierOnTimeDeliveryPct = 96m,
            };
            _db.SupplierQuotes.Add(quote);
            await _db.SaveChangesAsync(ct);
        }
        else { quote.Status = SupplierQuoteStatus.Received; quote.ValidUntilDate = DateTime.UtcNow.Date.AddDays(60); quote.SupplierOnTimeDeliveryPct = 96m; await _db.SaveChangesAsync(ct); }
        var qline = await _db.SupplierQuoteLines.FirstOrDefaultAsync(l => l.SupplierQuoteId == quote.Id && l.SupplierRFQLineId == rline.Id, ct);
        if (qline == null)
        {
            qline = new SupplierQuoteLine { SupplierQuoteId = quote.Id, SupplierRFQLineId = rline.Id, QuotedQuantity = Qty, QuotedUnitPrice = 150m, LineTotal = 150m * Qty, LeadTimeDays = 10 };
            _db.SupplierQuoteLines.Add(qline);
        }
        else { qline.QuotedUnitPrice = 150m; qline.LineTotal = 150m * Qty; qline.LeadTimeDays = 10; }
        await _db.SaveChangesAsync(ct);

        Set(true, $"Worked example ready: item #{itemId} marked MakeOrBuy ($195/u make vs $150/u quote). Now Decide & persist + show.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // ── 2) Decide & persist + render the panel ───────────────────────────────
    public async Task<IActionResult> OnPostDecidePersistAsync(CancellationToken ct)
    {
        var itemId = await ProbeItemIdAsync(ct);
        if (itemId is null or 0) { Set(false, "Run Set up worked example first."); await LoadStatsAsync(ct); return Page(); }
        var r = await _engine.DecideAsync(itemId.Value, Qty, Due(), null, MakeBuyDecisionContext.ManualWhatIf, true, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        PanelData = await BuildPanelAsync(r.Value!, ct);
        Set(true, $"Persisted decision #{r.Value!.PersistedDecisionId} → {r.Value.Outcome} (buy score {r.Value.BuyScore:0.00}). Panel rendered below.");
        return Page();
    }

    // ── 3) Toggle source-controlled + persist (forces MAKE hard gate) ────────
    public async Task<IActionResult> OnPostToggleSourceControlledAsync(CancellationToken ct)
    {
        var itemId = await ProbeItemIdAsync(ct);
        if (itemId is null or 0) { Set(false, "Run Set up worked example first."); await LoadStatsAsync(ct); return Page(); }
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        item!.IsSourceControlled = !item.IsSourceControlled;
        if (item.IsSourceControlled && string.IsNullOrWhiteSpace(item.SourceControlReason))
            item.SourceControlReason = "AS9100 source-controlled drawing";
        await _db.SaveChangesAsync(ct);
        var r = await _engine.DecideAsync(itemId.Value, Qty, Due(), null, MakeBuyDecisionContext.ManualWhatIf, true, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        PanelData = await BuildPanelAsync(r.Value!, ct);
        Set(true, $"Item source-controlled = {item.IsSourceControlled}. Decision → {r.Value!.Outcome}"
            + (r.Value.WasHardGated ? $" (hard-gated: {r.Value.HardGateReason})" : "") + ". Panel rendered below.");
        return Page();
    }

    // ── 4) Explain latest (the live cockpit's exact path) ────────────────────
    public async Task<IActionResult> OnPostExplainLatestAsync(CancellationToken ct)
    {
        var latestId = await _db.MakeBuyDecisions.Where(d => d.CompanyId == Company())
            .OrderByDescending(d => d.Id).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (latestId is null) { Set(false, "No persisted decision yet — Decide & persist first."); await LoadStatsAsync(ct); return Page(); }
        var r = await _engine.ExplainAsync(latestId.Value, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        PanelData = await BuildPanelAsync(r.Value!, ct);
        Set(true, $"Explained persisted decision #{latestId} → {r.Value!.Outcome}. Panel rendered below (the live cockpit path).");
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }

    // Resolve the panel view-model exactly like CockpitModel.LoadMakeBuyAsync.
    private async Task<MakeBuyCockpitPanelModel> BuildPanelAsync(MakeBuyDecisionResult result, CancellationToken ct)
    {
        var ident = await _db.Items
            .Where(i => i.Id == result.ItemId && _tenant.VisibleCompanyIds.Contains(i.CompanyId ?? 0))
            .Select(i => new { i.PartNumber, i.Description })
            .FirstOrDefaultAsync(ct);

        DateTime? decidedAt = null;
        MakeBuyDecisionContext? context = null;
        if (result.PersistedDecisionId != null)
        {
            var meta = await _db.MakeBuyDecisions
                .Where(d => d.Id == result.PersistedDecisionId)
                .Select(d => new { d.DecidedAtUtc, d.Context })
                .FirstOrDefaultAsync(ct);
            decidedAt = meta?.DecidedAtUtc;
            context = meta?.Context;
        }

        string? supplierName = null;
        if (result.ChosenSupplierId != null)
            supplierName = await _db.Vendors
                .Where(v => v.Id == result.ChosenSupplierId
                    && _tenant.VisibleCompanyIds.Contains(v.CompanyId ?? 0))
                .Select(v => v.Name).FirstOrDefaultAsync(ct);

        return new MakeBuyCockpitPanelModel
        {
            Result = result,
            PartNumber = ident?.PartNumber ?? $"Item #{result.ItemId}",
            Description = ident?.Description,
            DecidedAtUtc = decidedAt,
            Context = context,
            SupplierName = supplierName,
        };
    }

    private async Task EnsureCostAsync(int itemId, int companyId, CostElementType type, decimal amount, CancellationToken ct)
    {
        var ce = await _db.ItemStandardCostElements.FirstOrDefaultAsync(c => c.ItemId == itemId && c.SiteId == null && c.ElementType == type, ct);
        if (ce == null)
        {
            _db.ItemStandardCostElements.Add(new ItemStandardCostElement
            { ItemId = itemId, CompanyId = companyId, ElementType = type, Amount = amount, IsActive = true, CreatedBy = "MakeBuyCockpitPanelProbe" });
        }
        else { ce.Amount = amount; ce.IsActive = true; }
        await _db.SaveChangesAsync(ct);
    }
}
