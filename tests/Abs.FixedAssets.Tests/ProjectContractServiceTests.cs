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

// Theme B9 Wave 2 PR-6 (CLOSES Wave 2) — ProjectContractService: award gate,
// winning-revision→baseline, and the contract-review launch gate.
public sealed class ProjectContractServiceTests
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
            .UseInMemoryDatabase($"projcontract-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectContractService NewContractSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectContractService>.Instance);

    private static ProjectQuoteService NewQuoteSvc(AppDbContext db) =>
        new(db, new StubTenantContext(), NullLogger<ProjectQuoteService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1,
        CustomerProjectStatus status = CustomerProjectStatus.Quote)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = "PRJ-C-1", Name = "Contract test", Status = status,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // Creates a quote with a submitted Rev A worth `price`. ApprovalStatus defaults to
    // NotRequired (i.e. counts as "approved" for award).
    private static async Task<int> SeedSubmittedRevisionAsync(AppDbContext db, ProjectQuoteService q, int pid, decimal price)
    {
        var quote = await q.CreateQuoteAsync(new CreateQuoteRequest(pid, $"Q-{Guid.NewGuid():N}".Substring(0, 12)));
        var revId = await db.ProjectQuoteRevisions.Where(r => r.ProjectQuoteId == quote.Value!.QuoteId).Select(r => r.Id).FirstAsync();
        await q.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "P1", Quantity: 1m, UnitPrice: price));
        await q.SubmitRevisionAsync(revId);
        return revId;
    }

    [Fact]
    public async Task CreateContract_starts_draft_review_notstarted()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewContractSvc(db);
        var r = await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-1001", "Master Agreement"));
        Assert.True(r.IsSuccess);
        var c = await db.ProjectContracts.SingleAsync(x => x.Id == r.Value);
        Assert.Equal(ProjectContractStatus.Draft, c.Status);
        Assert.Equal(ProjectContractReviewStatus.NotStarted, c.ReviewStatus);
        Assert.True(c.ReviewRequired);
    }

    [Fact]
    public async Task Award_winning_revision_sets_baseline_on_project_and_marks_quote_won()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var q = NewQuoteSvc(db);
        var svc = NewContractSvc(db);
        var revId = await SeedSubmittedRevisionAsync(db, q, pid, 250_000m);
        var cid = (await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-AWARD"))).Value;

        var a = await svc.AwardQuoteRevisionAsync(new AwardQuoteRevisionRequest(cid, revId));
        Assert.True(a.IsSuccess);
        Assert.Equal(250_000m, a.Value!.BaselineContractValue);

        var rev = await db.ProjectQuoteRevisions.Include(r => r.Quote).SingleAsync(r => r.Id == revId);
        Assert.Equal(ProjectQuoteRevisionStatus.Awarded, rev.VersionStatus);
        Assert.True(rev.ConvertedToBaseline);
        Assert.Equal(ProjectQuoteStatus.Won, rev.Quote!.Status);
        Assert.Equal(rev.Id, rev.Quote.AwardedRevisionId);

        var project = await db.CustomerProjects.SingleAsync(p => p.Id == pid);
        Assert.Equal(250_000m, project.ContractValue);   // winning revision → baseline

        var contract = await db.ProjectContracts.SingleAsync(c => c.Id == cid);
        Assert.Equal(ProjectContractStatus.Awarded, contract.Status);
    }

    [Fact]
    public async Task Award_unsubmitted_draft_revision_is_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var q = NewQuoteSvc(db);
        var svc = NewContractSvc(db);
        var quote = await q.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-DRAFT"));
        var draftRevId = await db.ProjectQuoteRevisions.Where(r => r.ProjectQuoteId == quote.Value!.QuoteId).Select(r => r.Id).FirstAsync();
        var cid = (await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-D"))).Value;

        var a = await svc.AwardQuoteRevisionAsync(new AwardQuoteRevisionRequest(cid, draftRevId));
        Assert.True(a.IsFailure);
    }

    [Fact]
    public async Task Award_unapproved_revision_requires_override()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var q = NewQuoteSvc(db);
        var svc = NewContractSvc(db);
        var revId = await SeedSubmittedRevisionAsync(db, q, pid, 100_000m);
        // Force the revision into a Pending-approval state.
        var rev = await db.ProjectQuoteRevisions.SingleAsync(r => r.Id == revId);
        rev.ApprovalStatus = ProjectQuoteApprovalStatus.Pending;
        await db.SaveChangesAsync();
        var cid = (await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-OVR"))).Value;

        // §20: cannot award an unapproved quote without an authorized override.
        var blocked = await svc.AwardQuoteRevisionAsync(new AwardQuoteRevisionRequest(cid, revId));
        Assert.True(blocked.IsFailure);
        Assert.Contains("override", blocked.Error, StringComparison.OrdinalIgnoreCase);

        // With the override it goes through.
        var ok = await svc.AwardQuoteRevisionAsync(new AwardQuoteRevisionRequest(cid, revId, AuthorizedOverride: true));
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public async Task Launch_is_blocked_until_review_complete_then_launches_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db); // project starts in Quote
        var svc = NewContractSvc(db);
        var cid = (await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-LAUNCH"))).Value;

        // §20 contract-review gate: cannot launch until the required review is complete.
        var blocked = await svc.LaunchProjectAsync(cid);
        Assert.True(blocked.IsFailure);
        Assert.Contains("review", blocked.Error, StringComparison.OrdinalIgnoreCase);

        await svc.CompleteReviewAsync(cid, "J. Counsel");
        var launched = await svc.LaunchProjectAsync(cid);
        Assert.True(launched.IsSuccess);
        Assert.Equal(ProjectContractStatus.Active, launched.Value!.Status);

        var project = await db.CustomerProjects.SingleAsync(p => p.Id == pid);
        Assert.Equal(CustomerProjectStatus.Active, project.Status);  // Quote → Active
    }

    [Fact]
    public async Task Launch_without_review_required_succeeds_immediately()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewContractSvc(db);
        var cid = (await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-NOREV", ReviewRequired: false))).Value;

        var launched = await svc.LaunchProjectAsync(cid);
        Assert.True(launched.IsSuccess);
    }

    [Fact]
    public async Task RecordCustomerPo_creates_po_linked_to_contract()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewContractSvc(db);
        var cid = (await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-PO"))).Value;

        var po = await svc.RecordCustomerPoAsync(new RecordCustomerPoRequest(pid, "WEIR-PO-2026-0421",
            ProjectContractId: cid, PoValue: 250_000m));
        Assert.True(po.IsSuccess);
        var row = await db.ProjectCustomerPOs.SingleAsync(x => x.Id == po.Value);
        Assert.Equal(cid, row.ProjectContractId);
        Assert.Equal(250_000m, row.PoValue);
    }

    [Fact]
    public async Task CreateContract_for_project_outside_tenant_scope_fails()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);
        var svc = NewContractSvc(db, 1);
        var r = await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-X"));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task CreateContract_duplicate_number_per_company_is_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewContractSvc(db);
        await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-DUP"));
        var dup = await svc.CreateContractAsync(new CreateContractRequest(pid, "CT-DUP"));
        Assert.True(dup.IsFailure);
    }
}
