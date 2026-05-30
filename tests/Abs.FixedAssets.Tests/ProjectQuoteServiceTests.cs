using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

// Theme B9 Wave 2 PR-4 — ProjectQuoteService: quote spine + locked-submitted-snapshot rule.
public sealed class ProjectQuoteServiceTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"projquote-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public int? TenantId { get; init; } = 1;
        public int? CompanyId { get; init; }
        public int? SiteId { get; init; }
        public int? AssignedCompanyId { get; init; }
        public int? AssignedSiteId { get; init; }
        public List<int> VisibleCompanyIds { get; init; } = new() { 1 };
        public List<int> VisibleSiteIds { get; init; } = new();
        public bool IsResolved => true;
        public string? ResolutionError => null;
        public void SetContext(int? tenantId, int? companyId, int? siteId) { }
        public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
        public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
        public void SetError(string error) { }
    }

    private static ProjectQuoteService NewService(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectQuoteService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1, string code = "PRJ-Q-1")
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = code, Name = "Quote test", Status = CustomerProjectStatus.Active,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<(int quoteId, int revId)> SeedQuoteWithRevAsync(AppDbContext db, ProjectQuoteService svc, int pid)
    {
        var q = await svc.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-1001", ProjectQuoteType.Firm, "Baseline", Currency: "USD"));
        Assert.True(q.IsSuccess);
        var revId = await db.ProjectQuoteRevisions.Where(r => r.ProjectQuoteId == q.Value!.QuoteId)
            .Select(r => r.Id).FirstAsync();
        return (q.Value!.QuoteId, revId);
    }

    [Fact]
    public async Task CreateQuote_creates_quote_with_draft_rev_A()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);

        var r = await svc.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-1001", ProjectQuoteType.Firm, "Baseline"));
        Assert.True(r.IsSuccess);
        Assert.Equal(1, r.Value!.RevisionCount);
        var rev = await db.ProjectQuoteRevisions.SingleAsync(x => x.ProjectQuoteId == r.Value.QuoteId);
        Assert.Equal("A", rev.RevisionLabel);
        Assert.Equal(ProjectQuoteRevisionStatus.Draft, rev.VersionStatus);
        Assert.False(rev.IsSnapshotLocked);
    }

    [Fact]
    public async Task AddLine_to_draft_computes_lineno_and_extended_price()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);
        var (_, revId) = await SeedQuoteWithRevAsync(db, svc, pid);

        var l = await svc.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "TI-BRKT-44120",
            Description: "Ti-6Al-4V bracket", Quantity: 10m, UnitPrice: 392.80m, Uom: "EA"));
        Assert.True(l.IsSuccess);
        var line = await db.ProjectQuoteLines.SingleAsync(x => x.Id == l.Value);
        Assert.Equal(1, line.LineNo);
        Assert.Equal(3928.00m, line.ExtendedPrice);
    }

    [Fact]
    public async Task Submit_freezes_total_locks_snapshot_and_activates_quote()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);
        var (quoteId, revId) = await SeedQuoteWithRevAsync(db, svc, pid);
        await svc.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "P1", Quantity: 10m, UnitPrice: 392.80m));
        await svc.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "P2", Quantity: 2m, UnitPrice: 100m));

        var s = await svc.SubmitRevisionAsync(revId);
        Assert.True(s.IsSuccess);
        Assert.Equal(ProjectQuoteRevisionStatus.Submitted, s.Value!.VersionStatus);
        Assert.True(s.Value.IsSnapshotLocked);
        Assert.Equal(4128.00m, s.Value.TotalPrice); // 3928 + 200

        var quote = await db.ProjectQuotes.SingleAsync(x => x.Id == quoteId);
        Assert.Equal(ProjectQuoteStatus.Active, quote.Status);
    }

    [Fact]
    public async Task AddLine_to_submitted_revision_is_rejected_snapshot_locked()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);
        var (_, revId) = await SeedQuoteWithRevAsync(db, svc, pid);
        await svc.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "P1", Quantity: 1m, UnitPrice: 100m));
        await svc.SubmitRevisionAsync(revId);

        // The core rule: cannot overwrite a submitted quote snapshot.
        var l = await svc.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "P2", Quantity: 1m, UnitPrice: 50m));
        Assert.True(l.IsFailure);
        Assert.Contains("locked", l.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_with_no_lines_is_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);
        var (_, revId) = await SeedQuoteWithRevAsync(db, svc, pid);

        var s = await svc.SubmitRevisionAsync(revId);
        Assert.True(s.IsFailure);
    }

    [Fact]
    public async Task New_revision_B_supersedes_prior_submitted_A_on_submit()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);
        var (quoteId, revAId) = await SeedQuoteWithRevAsync(db, svc, pid);
        await svc.AddLineAsync(new AddQuoteLineRequest(revAId, PartNumber: "P1", Quantity: 1m, UnitPrice: 100m));
        await svc.SubmitRevisionAsync(revAId);

        var addB = await svc.AddRevisionAsync(quoteId);
        Assert.True(addB.IsSuccess);
        var revB = await db.ProjectQuoteRevisions.SingleAsync(x => x.Id == addB.Value);
        Assert.Equal("B", revB.RevisionLabel);
        Assert.Equal(2, revB.RevisionNumber);

        await svc.AddLineAsync(new AddQuoteLineRequest(revB.Id, PartNumber: "P1", Quantity: 1m, UnitPrice: 150m));
        await svc.SubmitRevisionAsync(revB.Id);

        var revA = await db.ProjectQuoteRevisions.SingleAsync(x => x.Id == revAId);
        Assert.Equal(ProjectQuoteRevisionStatus.Superseded, revA.VersionStatus);
    }

    [Fact]
    public async Task GetQuotesForProject_rolls_up_latest_submitted()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);
        var (_, revId) = await SeedQuoteWithRevAsync(db, svc, pid);
        await svc.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "P1", Quantity: 10m, UnitPrice: 392.80m));
        await svc.SubmitRevisionAsync(revId);

        var r = await svc.GetQuotesForProjectAsync(pid);
        Assert.True(r.IsSuccess);
        var q = Assert.Single(r.Value!);
        Assert.Equal("Q-1001", q.QuoteNumber);
        Assert.Equal("A", q.LatestSubmittedRevisionLabel);
        Assert.Equal(3928.00m, q.LatestSubmittedTotalPrice);
    }

    [Fact]
    public async Task CreateQuote_duplicate_number_per_company_is_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);
        await svc.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-DUP"));
        var dup = await svc.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-DUP"));
        Assert.True(dup.IsFailure);
    }

    [Fact]
    public async Task CreateQuote_for_project_outside_tenant_scope_fails()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);
        var svc = NewService(db, 1); // tenant sees only company 1

        var r = await svc.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-X"));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task AddLine_rejects_item_from_another_tenant()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db); // company 1
        var foreignItem = new Item { PartNumber = "OTHER-CO-ITM", Description = "Foreign item", CompanyId = 99 };
        db.Items.Add(foreignItem);
        await db.SaveChangesAsync();

        var svc = NewService(db, 1); // tenant sees only company 1
        var (_, revId) = await SeedQuoteWithRevAsync(db, svc, pid);

        var l = await svc.AddLineAsync(new AddQuoteLineRequest(revId, ItemId: foreignItem.Id,
            Quantity: 1m, UnitPrice: 100m));
        Assert.True(l.IsFailure);
        Assert.Contains("tenant scope", l.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRfq_then_quote_links_rfq()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewService(db);

        var rfq = await svc.CreateRfqAsync(new CreateRfqRequest(pid, "RFQ-2026-0142", CustomerRfqReference: "WEIR-RFQ-9"));
        Assert.True(rfq.IsSuccess);
        var q = await svc.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-1001", ProjectRfqId: rfq.Value));
        Assert.True(q.IsSuccess);
        var quote = await db.ProjectQuotes.SingleAsync(x => x.Id == q.Value!.QuoteId);
        Assert.Equal(rfq.Value, quote.ProjectRfqId);
    }
}
