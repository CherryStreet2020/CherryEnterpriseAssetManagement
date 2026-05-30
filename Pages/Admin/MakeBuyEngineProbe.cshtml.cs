// Theme B7 Wave C PR-8 — admin probe for the make-or-buy decision engine (the money-shot).
// Lock-16: Setup writes (tags an item MakeOrBuy + cost elements + RFQ/quote); Decide&persist
// writes a MakeBuyDecision; Toggle-source-controlled writes the item flag. What-if/Explain read.
//
//   1) Set up worked example — picks a routing-bearing item, marks it MakeOrBuy, seeds a
//      fully-loaded standard cost ($195/u) + a competitive supplier quote ($150/u, landed
//      ~$157), so cost favors BUY; F2 reads the routing WCs' REAL R4 Load%.
//   2) Decide (what-if)      — DecideAsync(persist:false): renders outcome + F1-F6 + rationale.
//   3) Decide & persist      — writes the MakeBuyDecision audit row.
//   4) Toggle source-controlled + decide — flips the item flag → forced MAKE (hard gate).
//   5) Explain latest        — ExplainAsync re-hydrates the most recent persisted decision.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
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
    "Admin diagnostic probe for Theme B7 Wave C PR-8 make-or-buy engine. Seeds a worked " +
    "example (MakeOrBuy item + standard cost + supplier quote) and runs IMakeBuyDecisionService, " +
    "tenant-scoped. Writes route through the service / idempotent ensure-helpers.")]
public sealed class MakeBuyEngineProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMakeBuyDecisionService _engine;
    private readonly ILogger<MakeBuyEngineProbeModel> _logger;

    public MakeBuyEngineProbeModel(AppDbContext db, ITenantContext tenant,
        IMakeBuyDecisionService engine, ILogger<MakeBuyEngineProbeModel> logger)
    {
        _db = db; _tenant = tenant; _engine = engine; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public int CompanyId { get; private set; }
    public int? ProbeItemId { get; private set; }
    public bool ProbeSourceControlled { get; private set; }
    public MakeBuyDecisionResult? Result { get; private set; }

    private const string RfqNumber = "MBPROBE-RFQ";
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
            ProbeSourceControlled = await _db.Items.Where(i => i.Id == ProbeItemId).Select(i => i.IsSourceControlled).FirstOrDefaultAsync(ct);
    }

    // The probe item = the one referenced by the MBPROBE-RFQ's line (set during Setup).
    private async Task<int?> ProbeItemIdAsync(CancellationToken ct) => await (
        from r in _db.SupplierRFQs
        where r.RfqNumber == RfqNumber && r.CompanyId == Company()
        join rl in _db.SupplierRFQLines on r.Id equals rl.SupplierRFQId
        where rl.ItemId != null
        select rl.ItemId).FirstOrDefaultAsync(ct);

    // ── 1) Setup ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostSetupAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company."); await LoadStatsAsync(ct); return Page(); }

        // A routing-bearing item gives F2 real work centers to load.
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

        // Mark the item MakeOrBuy and clear source-control so both paths evaluate.
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        item!.MakeBuyCode = MakeBuyCode.MakeOrBuy;
        item.MakeBuyPolicy = MakeBuyPolicy.Inherit;
        item.IsSourceControlled = false;
        await _db.SaveChangesAsync(ct);

        // Fully-loaded standard cost = $195/unit (make).
        await EnsureCostAsync(itemId, companyId, CostElementType.Material, 80m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.Labor, 40m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.VariableOverhead, 20m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.Setup, 30m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.Tooling, 10m, ct);
        await EnsureCostAsync(itemId, companyId, CostElementType.FixedOverhead, 15m, ct);

        // RFQ + line + a competitive quote ($150/u, landed ~$157.50 < make $195 → BUY on cost).
        var rfq = await _db.SupplierRFQs.FirstOrDefaultAsync(r => r.RfqNumber == RfqNumber && r.CompanyId == companyId, ct);
        if (rfq == null)
        {
            rfq = new SupplierRFQ { CompanyId = companyId, RfqNumber = RfqNumber, Title = "Make-or-buy engine probe RFQ", Status = RfqStatus.Evaluated };
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

        Set(true, $"Worked example ready: item #{itemId} marked MakeOrBuy with a $195/u fully-loaded standard cost and a " +
                  $"$150/u supplier quote (landed ~$157.50). F2 will read the routing's real R4 Load%. Now Decide (what-if).");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDecideWhatIfAsync(CancellationToken ct) => await DecideAsync(false, ct);
    public async Task<IActionResult> OnPostDecidePersistAsync(CancellationToken ct) => await DecideAsync(true, ct);

    private async Task<IActionResult> DecideAsync(bool persist, CancellationToken ct)
    {
        var itemId = await ProbeItemIdAsync(ct);
        if (itemId is null or 0) { Set(false, "Run Set up worked example first."); await LoadStatsAsync(ct); return Page(); }
        var r = await _engine.DecideAsync(itemId.Value, Qty, Due(), null, MakeBuyDecisionContext.ManualWhatIf, persist, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        Result = r.Value;
        Set(true, $"{(persist ? "Persisted decision" : "What-if")} → {Result!.Outcome} (buy score {Result.BuyScore:0.00}, confidence {Result.Confidence:0.00})" +
                  (Result.WasHardGated ? " — hard-gated" : "") + (Result.PersistedDecisionId != null ? $" · #{Result.PersistedDecisionId}" : "") + ".");
        return Page();
    }

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
        Result = r.Value;
        Set(true, $"Item source-controlled = {item.IsSourceControlled}. Decision → {Result!.Outcome}" +
                  (Result.WasHardGated ? $" (hard-gated: {Result.HardGateReason})" : "") + ".");
        return Page();
    }

    public async Task<IActionResult> OnPostExplainLatestAsync(CancellationToken ct)
    {
        var latestId = await _db.MakeBuyDecisions.Where(d => d.CompanyId == Company())
            .OrderByDescending(d => d.Id).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (latestId is null) { Set(false, "No persisted decision yet — Decide & persist first."); await LoadStatsAsync(ct); return Page(); }
        var r = await _engine.ExplainAsync(latestId.Value, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        Result = r.Value;
        Set(true, $"Explained persisted decision #{latestId} → {Result!.Outcome} (buy score {Result.BuyScore:0.00}).");
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }

    private async Task EnsureCostAsync(int itemId, int companyId, CostElementType type, decimal amount, CancellationToken ct)
    {
        var ce = await _db.ItemStandardCostElements.FirstOrDefaultAsync(c => c.ItemId == itemId && c.SiteId == null && c.ElementType == type, ct);
        if (ce == null)
        {
            _db.ItemStandardCostElements.Add(new ItemStandardCostElement
            { ItemId = itemId, CompanyId = companyId, ElementType = type, Amount = amount, IsActive = true, CreatedBy = "MakeBuyEngineProbe" });
        }
        else { ce.Amount = amount; ce.IsActive = true; }
        await _db.SaveChangesAsync(ct);
    }
}
