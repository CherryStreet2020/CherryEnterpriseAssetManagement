// Theme B7 Wave C PR-9 — admin probe for make-or-buy → supply integration (CLOSES Wave C).
// Lock-16: Setup writes (item + cost + quote + parent order + demand); Fulfill writes (decision +
// demand SupplyPolicy + child order on MAKE); Toggle writes the item flag.
//
//   1) Set up worked example — routing-bearing item marked MakeOrBuy ($195/u make) + a $150/u
//      quote, a parent production order, and a ProductionSupplyDemand line for the item.
//   2) Fulfill demand        — FulfillDemandAsync → BUY routes the demand to procurement
//      (SupplyPolicy=BuyDirectToJob); decision linked to the demand.
//   3) Toggle source-controlled + fulfill — forces MAKE → creates the child production order.
//   R) Reload.

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
    "Admin diagnostic probe for Theme B7 Wave C PR-9 make-or-buy supply integration. Seeds a demand " +
    "for a MakeOrBuy item and runs IMakeBuyFulfillmentService (decide → apply verdict), tenant-scoped.")]
public sealed class MakeBuyFulfillProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMakeBuyFulfillmentService _fulfill;
    private readonly ILogger<MakeBuyFulfillProbeModel> _logger;

    public MakeBuyFulfillProbeModel(AppDbContext db, ITenantContext tenant,
        IMakeBuyFulfillmentService fulfill, ILogger<MakeBuyFulfillProbeModel> logger)
    {
        _db = db; _tenant = tenant; _fulfill = fulfill; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public int CompanyId { get; private set; }
    public int? DemandId { get; private set; }
    public int? DemandItemId { get; private set; }
    public bool ItemSourceControlled { get; private set; }
    public SupplyPolicy? DemandPolicy { get; private set; }
    public BuyerActionState? DemandBuyerState { get; private set; }
    public MakeBuyFulfillmentResult? Result { get; private set; }

    private const string DemandNo = "MBFULFILL-DEMAND";
    private const string ParentNo = "MBFULFILL-PARENT";
    private const string RfqNo = "MBFULFILL-RFQ";
    private const decimal Qty = 50m;

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
    private int Company() => _tenant.VisibleCompanyIds.FirstOrDefault();

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        CompanyId = Company();
        var demand = await _db.ProductionSupplyDemands
            .Where(d => d.CompanyId == CompanyId && d.DemandNumber == DemandNo)
            .Select(d => new { d.Id, d.ItemId, d.SupplyPolicy, d.BuyerActionState }).FirstOrDefaultAsync(ct);
        if (demand != null)
        {
            DemandId = demand.Id; DemandItemId = demand.ItemId;
            DemandPolicy = demand.SupplyPolicy; DemandBuyerState = demand.BuyerActionState;
            if (demand.ItemId != null)
                ItemSourceControlled = await _db.Items.Where(i => i.Id == demand.ItemId).Select(i => i.IsSourceControlled).FirstOrDefaultAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostSetupAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company."); await LoadStatsAsync(ct); return Page(); }
        var locationId = await _db.Locations.Where(l => l.CompanyId == companyId).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        if (locationId == null) { Set(false, "No Location — seed master data first."); await LoadStatsAsync(ct); return Page(); }

        var pick = await (from rt in _db.Routings where rt.CompanyId == companyId
                          join i in _db.Items on rt.ItemId equals i.Id
                          orderby rt.IsDefault descending, rt.Id descending select new { i.Id }).FirstOrDefaultAsync(ct);
        if (pick == null) { Set(false, "No routing-bearing item — seed a routing first."); await LoadStatsAsync(ct); return Page(); }
        var itemId = pick.Id;
        var vendorId = await _db.Vendors.Where(v => v.CompanyId == companyId).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct)
                       ?? await _db.Vendors.Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (vendorId == null) { Set(false, "No Vendor — seed a vendor first."); await LoadStatsAsync(ct); return Page(); }

        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        item!.MakeBuyCode = MakeBuyCode.MakeOrBuy; item.MakeBuyPolicy = MakeBuyPolicy.Inherit; item.IsSourceControlled = false;
        await _db.SaveChangesAsync(ct);

        foreach (var (t, a) in new[] { (CostElementType.Material, 80m), (CostElementType.Labor, 40m),
            (CostElementType.VariableOverhead, 20m), (CostElementType.Setup, 30m), (CostElementType.Tooling, 10m), (CostElementType.FixedOverhead, 15m) })
        {
            var ce = await _db.ItemStandardCostElements.FirstOrDefaultAsync(c => c.ItemId == itemId && c.SiteId == null && c.ElementType == t, ct);
            if (ce == null) _db.ItemStandardCostElements.Add(new ItemStandardCostElement { ItemId = itemId, CompanyId = companyId, ElementType = t, Amount = a, IsActive = true, CreatedBy = "MakeBuyFulfillProbe" });
            else { ce.Amount = a; ce.IsActive = true; }
        }
        await _db.SaveChangesAsync(ct);

        var rfq = await _db.SupplierRFQs.FirstOrDefaultAsync(r => r.RfqNumber == RfqNo && r.CompanyId == companyId, ct);
        if (rfq == null) { rfq = new SupplierRFQ { CompanyId = companyId, RfqNumber = RfqNo, Title = "Make-or-buy fulfillment probe RFQ", Status = RfqStatus.Evaluated }; _db.SupplierRFQs.Add(rfq); await _db.SaveChangesAsync(ct); }
        var rl = await _db.SupplierRFQLines.FirstOrDefaultAsync(l => l.SupplierRFQId == rfq.Id && l.ItemId == itemId, ct);
        if (rl == null) { rl = new SupplierRFQLine { SupplierRFQId = rfq.Id, LineNumber = 1, ItemId = itemId, Description = "Probe component", Quantity = Qty, UOM = "EA" }; _db.SupplierRFQLines.Add(rl); await _db.SaveChangesAsync(ct); }
        var q = await _db.SupplierQuotes.FirstOrDefaultAsync(x => x.SupplierRFQId == rfq.Id && x.VendorId == vendorId, ct);
        if (q == null) { q = new SupplierQuote { CompanyId = companyId, SupplierRFQId = rfq.Id, VendorId = vendorId.Value, Status = SupplierQuoteStatus.Received, ValidUntilDate = DateTime.UtcNow.Date.AddDays(60), LeadTimeDays = 10, TotalQuotedAmount = 150m * Qty, SupplierOnTimeDeliveryPct = 96m }; _db.SupplierQuotes.Add(q); await _db.SaveChangesAsync(ct); }
        else { q.Status = SupplierQuoteStatus.Received; q.ValidUntilDate = DateTime.UtcNow.Date.AddDays(60); await _db.SaveChangesAsync(ct); }
        var qline = await _db.SupplierQuoteLines.FirstOrDefaultAsync(x => x.SupplierQuoteId == q.Id && x.SupplierRFQLineId == rl.Id, ct);
        if (qline == null) { _db.SupplierQuoteLines.Add(new SupplierQuoteLine { SupplierQuoteId = q.Id, SupplierRFQLineId = rl.Id, QuotedQuantity = Qty, QuotedUnitPrice = 150m, LineTotal = 150m * Qty, LeadTimeDays = 10 }); }
        else { qline.QuotedUnitPrice = 150m; }
        await _db.SaveChangesAsync(ct);

        // Parent production order + the demand line.
        var parent = await _db.ProductionOrders.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.OrderNumber == ParentNo, ct);
        if (parent == null) { parent = new ProductionOrder { CompanyId = companyId, OrderNumber = ParentNo, LocationId = locationId, Title = "Make-or-buy fulfillment parent order", Status = ProductionOrderStatus.Released, QuantityOrdered = Qty }; _db.ProductionOrders.Add(parent); await _db.SaveChangesAsync(ct); }
        var demand = await _db.ProductionSupplyDemands.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.DemandNumber == DemandNo, ct);
        if (demand == null)
        {
            demand = new ProductionSupplyDemand
            {
                CompanyId = companyId, SiteId = locationId, DemandNumber = DemandNo, ProductionOrderId = parent.Id,
                ItemId = itemId, RequiredQuantity = Qty, RemainingQuantity = Qty,
                RequiredDate = DateTime.UtcNow.Date.AddDays(21), NeedByDate = DateTime.UtcNow.Date.AddDays(21),
                SupplyPolicy = SupplyPolicy.ManualBuyerDecision, BuyerActionState = BuyerActionState.Open,
            };
            _db.ProductionSupplyDemands.Add(demand);
        }
        else { demand.ItemId = itemId; demand.SupplyPolicy = SupplyPolicy.ManualBuyerDecision; demand.BuyerActionState = BuyerActionState.Open; demand.BuyerActionStateUpdatedUtc = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);

        Set(true, $"Worked example ready: demand {DemandNo} (#{demand.Id}) for item #{itemId} (MakeOrBuy, $195/u make vs $150/u quote), " +
                  $"parent order {ParentNo}. Now Fulfill demand.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostFulfillAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        if (DemandId is null) { Set(false, "Run Set up worked example first."); return Page(); }
        var r = await _fulfill.FulfillDemandAsync(DemandId.Value, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        Result = r.Value;
        Set(true, $"Fulfilled demand #{Result!.DemandId} → {Result.Outcome}. {Result.Action}");
        return Page();
    }

    public async Task<IActionResult> OnPostToggleSourceControlledAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        if (DemandId is null || DemandItemId is null) { Set(false, "Run Set up worked example first."); return Page(); }
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == DemandItemId, ct);
        item!.IsSourceControlled = !item.IsSourceControlled;
        if (item.IsSourceControlled && string.IsNullOrWhiteSpace(item.SourceControlReason)) item.SourceControlReason = "AS9100 source-controlled drawing";
        await _db.SaveChangesAsync(ct);
        var r = await _fulfill.FulfillDemandAsync(DemandId.Value, ct);
        await LoadStatsAsync(ct);
        if (r.IsFailure) { Set(false, r.Error); return Page(); }
        Result = r.Value;
        Set(true, $"Item source-controlled = {item.IsSourceControlled}. Fulfilled → {Result!.Outcome}. {Result.Action}");
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }
}
