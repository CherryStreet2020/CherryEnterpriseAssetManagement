// Sprint 15.4 PR-20 — admin probe for IRfqQuoteService (CLOSES the cascade).
//   Writes (6): Create RFQ, Issue, Record Quote, Rank, Award, Convert-to-PO
//   Reads  (3): Load RFQ, Load Ranked (side-by-side comparison), Load RFQ list
// All AppDbContext reads tenant-scoped (PR-18 Codex lesson).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe. AppDbContext used for tenant-scoped read-only count " +
    "+ option queries. All writes flow through IRfqQuoteService.")]
public sealed class RfqQuoteProbeModel : PageModel
{
    private readonly IRfqQuoteService _service;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RfqQuoteProbeModel> _logger;

    public RfqQuoteProbeModel(
        IRfqQuoteService service, AppDbContext db,
        ITenantContext tenantContext, ILogger<RfqQuoteProbeModel> logger)
    {
        _service = service;
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ── Create ──
    [BindProperty] public string RfqTitle { get; set; } = "Probe RFQ — bar stock";
    [BindProperty] public string Line1Desc { get; set; } = "1018 CR steel bar, 1in";
    [BindProperty] public decimal Line1Qty { get; set; } = 100m;
    [BindProperty] public string? Line2Desc { get; set; } = "4140 alloy bar, 2in";
    [BindProperty] public decimal Line2Qty { get; set; } = 50m;

    // ── Issue ──
    [BindProperty] public int IssueRfqId { get; set; } = 1;
    [BindProperty] public string VendorIdsCsv { get; set; } = "1,2,3";

    // ── Record quote ──
    [BindProperty] public int QuoteRfqId { get; set; } = 1;
    [BindProperty] public int QuoteVendorId { get; set; } = 1;
    [BindProperty] public decimal QuoteUnitPrice { get; set; } = 12.50m;
    [BindProperty] public int QuoteLeadTimeDays { get; set; } = 14;

    // ── Rank ──
    [BindProperty] public int RankRfqId { get; set; } = 1;
    [BindProperty] public decimal PriceWeight { get; set; } = 0.5m;
    [BindProperty] public decimal LeadTimeWeight { get; set; } = 0.3m;
    [BindProperty] public decimal OtdWeight { get; set; } = 0.2m;

    // ── Award / Convert ──
    [BindProperty] public int AwardQuoteId { get; set; } = 1;
    [BindProperty] public int ConvertQuoteId { get; set; } = 1;

    // ── Reads ──
    [BindProperty] public int LoadRfqId { get; set; } = 1;
    [BindProperty] public int RankedRfqId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalRfqs { get; private set; }
    public int IssuedCount { get; private set; }
    public int EvaluatedCount { get; private set; }
    public int AwardedCount { get; private set; }
    public int TotalQuotes { get; private set; }

    public SupplierRFQ? LoadedRfq { get; private set; }
    public IReadOnlyList<RankedQuoteRow>? RankedRows { get; private set; }
    public IReadOnlyList<RfqListRow>? RfqList { get; private set; }
    public IReadOnlyList<VendorOption> Vendors { get; private set; } = Array.Empty<VendorOption>();

    public sealed record VendorOption(int Id, string Display);

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        var rfqs = _db.Set<SupplierRFQ>().Where(r => r.CompanyId != null && visible.Contains(r.CompanyId.Value));
        TotalRfqs = await rfqs.CountAsync(ct);
        IssuedCount = await rfqs.CountAsync(r => r.Status == RfqStatus.Issued || r.Status == RfqStatus.QuotesReceived, ct);
        EvaluatedCount = await rfqs.CountAsync(r => r.Status == RfqStatus.Evaluated, ct);
        AwardedCount = await rfqs.CountAsync(r => r.Status == RfqStatus.Awarded || r.Status == RfqStatus.Closed, ct);
        TotalQuotes = await _db.Set<SupplierQuote>()
            .CountAsync(q => q.CompanyId != null && visible.Contains(q.CompanyId.Value), ct);

        Vendors = await _db.Set<Vendor>()
            .Where(v => v.CompanyId != null && visible.Contains(v.CompanyId.Value))
            .OrderBy(v => v.Name).Take(25)
            .Select(v => new VendorOption(v.Id, $"#{v.Id} — {v.Name}"))
            .ToListAsync(ct);
    }

    // W1) CREATE RFQ
    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var lines = new List<RfqLineInput>
        {
            new(Line1Desc, Line1Qty, "EA", null, null, DateTime.UtcNow.Date.AddDays(30), null, null, null, null),
        };
        if (!string.IsNullOrWhiteSpace(Line2Desc) && Line2Qty > 0)
            lines.Add(new(Line2Desc!, Line2Qty, "EA", null, null, DateTime.UtcNow.Date.AddDays(30), null, null, null, null));

        var r = await _service.CreateRfqAsync(
            new CreateRfqRequest(RfqTitle, DateTime.UtcNow.Date.AddDays(30), null, "Admin probe RFQ.", lines),
            DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created {r.Value!.RfqNumber} (id {r.Value.SupplierRFQId}) with {r.Value.LinesCreated} line(s)."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // W2) ISSUE
    public async Task<IActionResult> OnPostIssueAsync(CancellationToken ct)
    {
        var vendorIds = ParseIds(VendorIdsCsv);
        var r = await _service.IssueRfqAsync(IssueRfqId, vendorIds, DateTime.UtcNow.AddDays(7), DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Issued RFQ #{IssueRfqId} — invited {r.Value} new supplier(s)."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // W3) RECORD QUOTE — applies the unit price to every RFQ line at its qty.
    public async Task<IActionResult> OnPostRecordQuoteAsync(CancellationToken ct)
    {
        var rfq = await _service.GetRfqAsync(QuoteRfqId, ct);
        if (rfq == null)
        {
            Set(false, $"RFQ {QuoteRfqId} not found.");
            await LoadStatsAsync(ct);
            return Page();
        }
        var lines = rfq.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new QuoteLineInput(l.Id, l.Quantity, QuoteUnitPrice, QuoteLeadTimeDays))
            .ToList();

        var r = await _service.RecordQuoteAsync(
            new RecordQuoteRequest(QuoteRfqId, QuoteVendorId, $"VQ-{QuoteVendorId}", DateTime.UtcNow.AddDays(30),
                "USD", QuoteLeadTimeDays, lines),
            DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Quote recorded for vendor {QuoteVendorId}: total ${r.Value!.TotalQuotedAmount:N2}, {r.Value.LeadTimeDays}d lead, status {r.Value.Status}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // W4) RANK ⭐
    public async Task<IActionResult> OnPostRankAsync(CancellationToken ct)
    {
        var r = await _service.RankQuotesAsync(
            RankRfqId, new RankWeights(PriceWeight, LeadTimeWeight, OtdWeight), DateTime.UtcNow, ct);
        if (r.IsSuccess)
        {
            RankedRows = r.Value!.Ranked;
            var w = r.Value.Ranked.FirstOrDefault(x => x.IsWinner);
            Set(true, $"Ranked {r.Value.QuotesRanked} quote(s). Winner: "
                + (w != null ? $"{w.VendorName} (composite {w.CompositeScore:N1})." : "none."));
        }
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // W5) AWARD
    public async Task<IActionResult> OnPostAwardAsync(CancellationToken ct)
    {
        var r = await _service.AwardQuoteAsync(AwardQuoteId, DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Quote #{AwardQuoteId} awarded (status {r.Value!.Status})."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // W6) CONVERT TO PO
    public async Task<IActionResult> OnPostConvertAsync(CancellationToken ct)
    {
        var r = await _service.ConvertQuoteToPoLineAsync(ConvertQuoteId, DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Converted quote #{ConvertQuoteId} → {r.Value!.PoNumber} (PO #{r.Value.PurchaseOrderId}), {r.Value.LinesCreated} line(s), {r.Value.DemandLinksCreated} demand link(s)."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // R1) LOAD RFQ
    public async Task<IActionResult> OnPostLoadRfqAsync(CancellationToken ct)
    {
        LoadedRfq = await _service.GetRfqAsync(LoadRfqId, ct);
        Set(true, LoadedRfq == null
            ? $"RFQ {LoadRfqId} not found."
            : $"{LoadedRfq.RfqNumber}: {LoadedRfq.Status}, {LoadedRfq.Lines.Count} line(s), {LoadedRfq.Quotes.Count} quote(s).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R2) LOAD RANKED (side-by-side comparison)
    public async Task<IActionResult> OnPostLoadRankedAsync(CancellationToken ct)
    {
        RankedRows = await _service.GetRankedQuotesAsync(RankedRfqId, ct);
        Set(true, $"{RankedRows.Count} ranked quote(s) for RFQ #{RankedRfqId}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R3) LOAD RFQ LIST
    public async Task<IActionResult> OnPostLoadListAsync(CancellationToken ct)
    {
        RfqList = await _service.GetRfqListAsync(50, null, ct);
        Set(true, $"{RfqList.Count} RFQ(s) in scope.");
        await LoadStatsAsync(ct);
        return Page();
    }

    private static List<int> ParseIds(string csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0).Distinct().ToList();
}
