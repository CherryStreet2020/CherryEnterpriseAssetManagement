// Theme B9 Wave 2 PR-4 (2026-05-30) — ProjectQuoteService impl.
// Tenant-scoped writes/reads over the quote spine. New RFQ/Quote inherit their
// CompanyId from the (tenant-verified) parent project. Revisions/lines are scoped
// through their parent quote. The locked-snapshot rule lives in AddLineAsync +
// SubmitRevisionAsync.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectQuoteService : IProjectQuoteService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectQuoteService> _log;

    public ProjectQuoteService(AppDbContext db, ITenantContext tenant, ILogger<ProjectQuoteService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    // Resolve a tenant-visible project and hand back its CompanyId for child writes.
    private async Task<(bool ok, int companyId, string? err)> ResolveProjectCompanyAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, 0, "CustomerProjectId must be > 0.");
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (row == null) return (false, 0, $"Customer project {projectId} not found in your tenant scope.");
        if (row.CompanyId is null) return (false, 0, $"Customer project {projectId} has no company assigned.");
        return (true, row.CompanyId.Value, null);
    }

    public async Task<Result<int>> CreateRfqAsync(CreateRfqRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.RfqNumber))
            return Result.Failure<int>("An RFQ number is required.");

        var (ok, companyId, err) = await ResolveProjectCompanyAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var rfqNo = req.RfqNumber.Trim();
        bool dup = await _db.ProjectRfqs.AnyAsync(r => r.CompanyId == companyId && r.RfqNumber == rfqNo, ct);
        if (dup) return Result.Failure<int>($"RFQ number '{rfqNo}' already exists for this company.");

        var rfq = new ProjectRfq
        {
            CompanyId = companyId,
            CustomerProjectId = req.CustomerProjectId,
            RfqNumber = rfqNo,
            CustomerRfqReference = req.CustomerRfqReference,
            Description = req.Description,
            ReceivedDate = req.ReceivedDate,
            DueDate = req.DueDate,
            Status = ProjectRfqStatus.Open,
            OwnerName = req.OwnerName,
            EstimatorName = req.EstimatorName,
            SalespersonName = req.SalespersonName,
        };
        _db.ProjectRfqs.Add(rfq);
        await _db.SaveChangesAsync(ct);
        return Result.Success(rfq.Id);
    }

    public async Task<Result<ProjectQuoteSummary>> CreateQuoteAsync(CreateQuoteRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.QuoteNumber))
            return Result.Failure<ProjectQuoteSummary>("A quote number is required.");

        var (ok, companyId, err) = await ResolveProjectCompanyAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectQuoteSummary>(err!);

        var quoteNo = req.QuoteNumber.Trim();
        bool dup = await _db.ProjectQuotes.AnyAsync(q => q.CompanyId == companyId && q.QuoteNumber == quoteNo, ct);
        if (dup) return Result.Failure<ProjectQuoteSummary>($"Quote number '{quoteNo}' already exists for this company.");

        // Validate the optional RFQ belongs to the same project/company.
        if (req.ProjectRfqId is { } rfqId)
        {
            bool rfqOk = await _db.ProjectRfqs.AnyAsync(
                r => r.Id == rfqId && r.CompanyId == companyId && r.CustomerProjectId == req.CustomerProjectId, ct);
            if (!rfqOk) return Result.Failure<ProjectQuoteSummary>($"RFQ {rfqId} is not on this project.");
        }

        var quote = new ProjectQuote
        {
            CompanyId = companyId,
            CustomerProjectId = req.CustomerProjectId,
            ProjectRfqId = req.ProjectRfqId,
            QuoteNumber = quoteNo,
            QuoteType = req.QuoteType,
            Scenario = req.Scenario,
            Description = req.Description,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency!.Trim(),
            OwnerName = req.OwnerName,
            EstimatorName = req.EstimatorName,
            SalespersonName = req.SalespersonName,
            Status = ProjectQuoteStatus.Draft,
        };
        // Seed the first revision "Rev A".
        quote.Revisions = new List<ProjectQuoteRevision>
        {
            new() { RevisionNumber = 1, RevisionLabel = NumberToLabel(1), VersionStatus = ProjectQuoteRevisionStatus.Draft },
        };
        _db.ProjectQuotes.Add(quote);
        await _db.SaveChangesAsync(ct);

        return Result.Success(new ProjectQuoteSummary(
            quote.Id, quote.QuoteNumber, quote.QuoteType, quote.Status, quote.Scenario, quote.Currency,
            RevisionCount: 1, null, null, null, null));
    }

    public async Task<Result<int>> AddRevisionAsync(int quoteId, CancellationToken ct = default)
    {
        var q = await _db.ProjectQuotes
            .Where(x => x.Id == quoteId && _tenant.VisibleCompanyIds.Contains(x.CompanyId))
            .Select(x => new { x.Id })
            .FirstOrDefaultAsync(ct);
        if (q == null) return Result.Failure<int>($"Quote {quoteId} not found in your tenant scope.");

        int maxNum = await _db.ProjectQuoteRevisions
            .Where(r => r.ProjectQuoteId == quoteId)
            .Select(r => (int?)r.RevisionNumber)
            .MaxAsync(ct) ?? 0;
        int next = maxNum + 1;

        var rev = new ProjectQuoteRevision
        {
            ProjectQuoteId = quoteId,
            RevisionNumber = next,
            RevisionLabel = NumberToLabel(next),
            VersionStatus = ProjectQuoteRevisionStatus.Draft,
        };
        _db.ProjectQuoteRevisions.Add(rev);
        await _db.SaveChangesAsync(ct);
        return Result.Success(rev.Id);
    }

    public async Task<Result<int>> AddLineAsync(AddQuoteLineRequest req, CancellationToken ct = default)
    {
        if (req is null || req.RevisionId <= 0) return Result.Failure<int>("A revision id is required.");

        // Load the revision + its quote for the tenant check + lock state.
        var rev = await _db.ProjectQuoteRevisions
            .Where(r => r.Id == req.RevisionId)
            .Select(r => new { r.Id, r.IsSnapshotLocked, r.VersionStatus, CompanyId = r.Quote!.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (rev == null || !_tenant.VisibleCompanyIds.Contains(rev.CompanyId))
            return Result.Failure<int>($"Quote revision {req.RevisionId} not found in your tenant scope.");

        if (rev.IsSnapshotLocked || rev.VersionStatus != ProjectQuoteRevisionStatus.Draft)
            return Result.Failure<int>(
                "This revision is submitted and its snapshot is locked — add a new revision instead of editing it.");

        // Tenant-scope the optional item FK (Codex P1): Items are company-scoped, so a
        // caller must not attach another tenant's item to a quote line. Shared/global
        // catalog items (CompanyId == null) are allowed.
        if (req.ItemId is { } itemId)
        {
            bool itemOk = await _db.Items.AnyAsync(
                i => i.Id == itemId
                    && (i.CompanyId == null || _tenant.VisibleCompanyIds.Contains(i.CompanyId.Value)), ct);
            if (!itemOk) return Result.Failure<int>($"Item {itemId} is not in your tenant scope.");
        }

        int lineNo = req.LineNo ?? ((await _db.ProjectQuoteLines
            .Where(l => l.ProjectQuoteRevisionId == req.RevisionId)
            .Select(l => (int?)l.LineNo).MaxAsync(ct) ?? 0) + 1);

        decimal? extended = (req.UnitPrice.HasValue) ? req.Quantity * req.UnitPrice.Value : (decimal?)null;

        var line = new ProjectQuoteLine
        {
            ProjectQuoteRevisionId = req.RevisionId,
            LineNo = lineNo,
            ItemId = req.ItemId,
            PartNumber = req.PartNumber,
            Description = req.Description,
            Quantity = req.Quantity,
            Uom = req.Uom,
            UnitPrice = req.UnitPrice,
            ExtendedPrice = extended,
            UnitCost = req.UnitCost,
            LeadTimeDays = req.LeadTimeDays,
            Notes = req.Notes,
        };
        _db.ProjectQuoteLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return Result.Success(line.Id);
    }

    public async Task<Result<ProjectQuoteRevisionSummary>> SubmitRevisionAsync(int revisionId, CancellationToken ct = default)
    {
        var rev = await _db.ProjectQuoteRevisions
            .Include(r => r.Quote)
            .FirstOrDefaultAsync(r => r.Id == revisionId, ct);
        if (rev == null || rev.Quote == null || !_tenant.VisibleCompanyIds.Contains(rev.Quote.CompanyId))
            return Result.Failure<ProjectQuoteRevisionSummary>($"Quote revision {revisionId} not found in your tenant scope.");

        if (rev.IsSnapshotLocked || rev.VersionStatus != ProjectQuoteRevisionStatus.Draft)
            return Result.Failure<ProjectQuoteRevisionSummary>(
                "Only a Draft revision can be submitted; this one is already locked.");

        var lines = await _db.ProjectQuoteLines
            .Where(l => l.ProjectQuoteRevisionId == revisionId)
            .Select(l => new { l.ExtendedPrice, l.Quantity, l.UnitPrice })
            .ToListAsync(ct);
        if (lines.Count == 0)
            return Result.Failure<ProjectQuoteRevisionSummary>("Add at least one line before submitting the revision.");

        decimal total = lines.Sum(l => l.ExtendedPrice ?? (l.UnitPrice.HasValue ? l.Quantity * l.UnitPrice.Value : 0m));

        var now = DateTime.UtcNow;
        rev.VersionStatus = ProjectQuoteRevisionStatus.Submitted;
        rev.IsSnapshotLocked = true;
        rev.SnapshotLockedAt = now;
        rev.SubmittedDate ??= now;
        rev.TotalPrice = total;
        if (rev.ValidityDays is { } vd && rev.ExpirationDate is null)
            rev.ExpirationDate = now.AddDays(vd);
        rev.ModifiedAt = now;

        // Supersede any other already-submitted revision on the same quote.
        var priorSubmitted = await _db.ProjectQuoteRevisions
            .Where(r => r.ProjectQuoteId == rev.ProjectQuoteId
                && r.Id != revisionId
                && r.VersionStatus == ProjectQuoteRevisionStatus.Submitted)
            .ToListAsync(ct);
        foreach (var p in priorSubmitted)
        {
            p.VersionStatus = ProjectQuoteRevisionStatus.Superseded;
            p.ModifiedAt = now;
        }

        // The quote goes Active once it has a live submitted revision.
        if (rev.Quote.Status == ProjectQuoteStatus.Draft)
            rev.Quote.Status = ProjectQuoteStatus.Active;

        await _db.SaveChangesAsync(ct);

        return Result.Success(new ProjectQuoteRevisionSummary(
            rev.Id, rev.ProjectQuoteId, rev.RevisionLabel, rev.RevisionNumber,
            rev.VersionStatus, rev.IsSnapshotLocked, rev.TotalPrice, lines.Count, rev.SubmittedDate));
    }

    public async Task<Result<IReadOnlyList<ProjectQuoteSummary>>> GetQuotesForProjectAsync(int customerProjectId, CancellationToken ct = default)
    {
        var (ok, _, err) = await ResolveProjectCompanyAsync(customerProjectId, ct);
        if (!ok) return Result.Failure<IReadOnlyList<ProjectQuoteSummary>>(err!);

        var quotes = await _db.ProjectQuotes
            .Where(q => q.CustomerProjectId == customerProjectId
                && _tenant.VisibleCompanyIds.Contains(q.CompanyId))
            .Select(q => new
            {
                q.Id, q.QuoteNumber, q.QuoteType, q.Status, q.Scenario, q.Currency,
                Revisions = q.Revisions!.Select(r => new
                {
                    r.RevisionNumber, r.RevisionLabel, r.VersionStatus, r.TotalPrice, r.SubmittedDate,
                }).ToList(),
            })
            .OrderBy(q => q.Id)
            .ToListAsync(ct);

        var list = quotes.Select(q =>
        {
            var submitted = q.Revisions
                .Where(r => r.VersionStatus == ProjectQuoteRevisionStatus.Submitted
                    || r.VersionStatus == ProjectQuoteRevisionStatus.Awarded)
                .OrderByDescending(r => r.RevisionNumber)
                .FirstOrDefault();
            return new ProjectQuoteSummary(
                q.Id, q.QuoteNumber, q.QuoteType, q.Status, q.Scenario, q.Currency,
                q.Revisions.Count,
                submitted?.RevisionNumber, submitted?.RevisionLabel,
                submitted?.TotalPrice, submitted?.SubmittedDate);
        }).ToList();

        return Result.Success<IReadOnlyList<ProjectQuoteSummary>>(list);
    }

    // 1→A, 2→B, … 26→Z, 27→AA (spreadsheet-style customer-visible labels).
    private static string NumberToLabel(int n)
    {
        if (n <= 0) return "A";
        var sb = new System.Text.StringBuilder();
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + n % 26));
            n /= 26;
        }
        return sb.ToString();
    }
}
