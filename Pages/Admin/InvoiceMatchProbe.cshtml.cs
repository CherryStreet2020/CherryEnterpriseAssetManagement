// Sprint 15.4 PR-19 — admin probe for IInvoiceMatchService.
//   Writes (2): Run Match, Run & Approve If Clean (atomic match + AP posting)
//   Reads  (2): Load Current Match (with lines), Load Exceptions
// All AppDbContext reads tenant-scoped from the start (PR-18 Codex lesson).

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
    "Admin diagnostic probe. AppDbContext used for tenant-scoped read-only " +
    "count + option queries. All writes flow through IInvoiceMatchService.")]
public sealed class InvoiceMatchProbeModel : PageModel
{
    private readonly IInvoiceMatchService _service;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<InvoiceMatchProbeModel> _logger;

    public InvoiceMatchProbeModel(
        IInvoiceMatchService service,
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<InvoiceMatchProbeModel> logger)
    {
        _service = service;
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ── Run match ──
    [BindProperty] public int RunInvoiceId { get; set; } = 1;
    [BindProperty] public decimal PriceAbs { get; set; } = 0.01m;
    [BindProperty] public decimal PricePct { get; set; } = 2.0m;
    [BindProperty] public decimal QtyAbs { get; set; } = 0m;
    [BindProperty] public decimal QtyPct { get; set; } = 0m;
    [BindProperty] public int DateDays { get; set; } = 7;

    // ── Run & approve ──
    [BindProperty] public int ApproveInvoiceId { get; set; } = 1;
    [BindProperty] public string ApproverUsername { get; set; } = "admin-probe";

    // ── Reads ──
    [BindProperty] public int CurrentInvoiceId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalResults { get; private set; }
    public int CurrentResults { get; private set; }
    public int MatchedCount { get; private set; }
    public int WithinToleranceCount { get; private set; }
    public int ExceptionCount { get; private set; }
    public int PostedCount { get; private set; }

    public InvoiceMatchResult? LastCurrent { get; private set; }
    public IReadOnlyList<InvoiceMatchExceptionRow>? LastExceptions { get; private set; }
    public IReadOnlyList<InvoiceOption> CandidateInvoices { get; private set; }
        = Array.Empty<InvoiceOption>();

    public sealed record InvoiceOption(int Id, string Display);

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    private InvoiceMatchTolerances BuildTolerances() =>
        new(PriceAbs, PricePct, QtyAbs, QtyPct, DateDays);

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        var scoped = _db.Set<InvoiceMatchResult>()
            .Where(r => r.CompanyId != null && visible.Contains(r.CompanyId.Value));

        TotalResults = await scoped.CountAsync(ct);
        CurrentResults = await scoped.CountAsync(r => r.IsCurrent, ct);
        MatchedCount = await scoped.CountAsync(r => r.IsCurrent && r.Outcome == InvoiceMatchOutcome.Matched, ct);
        WithinToleranceCount = await scoped.CountAsync(r => r.IsCurrent && r.Outcome == InvoiceMatchOutcome.MatchedWithinTolerance, ct);
        ExceptionCount = await scoped.CountAsync(r => r.IsCurrent && r.Outcome == InvoiceMatchOutcome.Exception, ct);
        PostedCount = await scoped.CountAsync(r => r.IsCurrent && r.PostedOnMatch, ct);

        // Invoices with at least one PO-linked line — the 3-way-match candidates.
        CandidateInvoices = await _db.Set<VendorInvoice>()
            .Where(i => i.CompanyId != null && visible.Contains(i.CompanyId.Value)
                && i.Lines.Any(l => l.PurchaseOrderLineId != null))
            .OrderByDescending(i => i.InvoiceDate)
            .Take(25)
            .Select(i => new InvoiceOption(i.Id,
                $"#{i.Id} — {i.InvoiceNumber} ({i.MatchStatus}, ${i.Total:N2})"))
            .ToListAsync(ct);
    }

    // W1) RUN MATCH
    public async Task<IActionResult> OnPostRunMatchAsync(CancellationToken ct)
    {
        var r = await _service.RunMatchAsync(RunInvoiceId, BuildTolerances(), DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"{r.Value!.MatchRunNumber}: {r.Value.Outcome} — {r.Value.LinesMatched} matched, "
              + $"{r.Value.LinesWithinTolerance} within tol, {r.Value.LinesException} exception(s), "
              + $"price var ${r.Value.TotalPriceVariance:N2}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // W2) RUN & APPROVE IF CLEAN
    public async Task<IActionResult> OnPostRunApproveAsync(CancellationToken ct)
    {
        var r = await _service.RunAndApproveIfCleanAsync(
            ApproveInvoiceId, BuildTolerances(), ApproverUsername, DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"{r.Value!.MatchRunNumber}: {r.Value.Outcome}. "
              + (r.Value.PostedOnMatch
                    ? $"Approved + posted JE {r.Value.PostedJournalEntryId}."
                    : "Not posted (exceptions present).")
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // R1) LOAD CURRENT MATCH
    public async Task<IActionResult> OnPostLoadCurrentAsync(CancellationToken ct)
    {
        LastCurrent = await _service.GetCurrentMatchAsync(CurrentInvoiceId, ct);
        Set(true, LastCurrent == null
            ? $"Invoice {CurrentInvoiceId} has no match run yet."
            : $"{LastCurrent.MatchRunNumber}: {LastCurrent.Outcome}, {LastCurrent.Lines.Count} line(s).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R2) LOAD EXCEPTIONS
    public async Task<IActionResult> OnPostLoadExceptionsAsync(CancellationToken ct)
    {
        LastExceptions = await _service.GetExceptionRowsAsync(50, ct);
        Set(true, $"{LastExceptions.Count} invoice(s) with open match exceptions.");
        await LoadStatsAsync(ct);
        return Page();
    }
}
