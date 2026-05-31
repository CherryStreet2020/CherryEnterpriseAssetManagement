using System;
using System.Collections.Generic;
using System.Linq;
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

// Theme B9 Wave 6 PR-17 — ProjectQualityService: inspections/NCR/MRB/punch +
// the §22.4 acceptance gate (blocked by open blocking NCR / pending MRB /
// blocking-acceptance punch) and the wiring that flips the PR-14 billing
// AcceptanceConfirmed on a RevenueTrigger acceptance.
public sealed class ProjectQualityServiceTests
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
            .UseInMemoryDatabase($"projqa-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectQualityService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectQualityService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-Q-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Quality test", Status = CustomerProjectStatus.Active, ContractValue = 185_000m,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> SeedAcceptanceGatedBillingAsync(AppDbContext db, int pid)
    {
        var s = new ProjectBillingSchedule
        {
            CustomerProjectId = pid, Code = "BILL-FINAL", Name = "Final billing",
            BillingType = ProjectBillingType.Milestone, ScheduledAmount = 145_000m, Currency = "USD",
            RequiresAcceptance = true, AcceptanceConfirmed = false, Status = ProjectBillingStatus.Planned,
        };
        db.ProjectBillingSchedules.Add(s);
        await db.SaveChangesAsync();
        return s.Id;
    }

    // -- numbering + scoping ----------------------------------------------

    [Fact]
    public async Task CreateNcr_numbers_per_project_and_scopes_phase()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var otherPid = await SeedProjectAsync(db);
        var phase = new ProjectPhase { CustomerProjectId = otherPid, Code = "PH-X", Name = "Other" };
        db.ProjectPhases.Add(phase); await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var a = await svc.CreateNcrAsync(new CreateNcrRequest(pid, "N1"));
        var b = await svc.CreateNcrAsync(new CreateNcrRequest(pid, "N2"));
        Assert.Equal(1, (await db.ProjectNCRs.FindAsync(a.Value))!.NcrNumber);
        Assert.Equal(2, (await db.ProjectNCRs.FindAsync(b.Value))!.NcrNumber);
        // phase from another project rejected
        Assert.False((await svc.CreateNcrAsync(new CreateNcrRequest(pid, "bad", AffectedPhaseId: phase.Id))).IsSuccess);
    }

    [Fact]
    public async Task CreateNcr_rejects_negative_quantity()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        Assert.False((await svc.CreateNcrAsync(new CreateNcrRequest(pid, "bad", QuantityAffected: -5))).IsSuccess);
    }

    // -- NCR disposition + close gate -------------------------------------

    [Fact]
    public async Task Ncr_cannot_close_before_dispositioned()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateNcrAsync(new CreateNcrRequest(pid, "N"))).Value;
        // not dispositioned → close blocked
        Assert.False((await svc.TransitionNcrAsync(id, ProjectNcrStatus.Closed, "Dean")).IsSuccess);
        Assert.True((await svc.DispositionNcrAsync(new DispositionNcrRequest(id, ProjectQualityDisposition.Rework, "bad tool", "replace insert"))).IsSuccess);
        var t = await svc.TransitionNcrAsync(id, ProjectNcrStatus.Closed, "Dean");
        Assert.True(t.IsSuccess);
        Assert.NotNull(t.Value.ClosedAt);
        Assert.Equal("Dean", t.Value.ClosedBy);
    }

    // -- MRB customer-approval gate ---------------------------------------

    [Fact]
    public async Task Mrb_requiring_customer_approval_cannot_disposition_until_approved()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateMrbAsync(new CreateMrbRequest(pid, "M", CustomerApprovalRequired: true))).Value;
        // no customer approval → blocked
        Assert.False((await svc.DispositionMrbAsync(new DispositionMrbRequest(id, ProjectQualityDisposition.UseAsIs, "fit for use"))).IsSuccess);
        // with approval → ok
        var d = await svc.DispositionMrbAsync(new DispositionMrbRequest(id, ProjectQualityDisposition.UseAsIs, "fit for use", CustomerApproved: true));
        Assert.True(d.IsSuccess);
        Assert.Equal(ProjectMrbStatus.Dispositioned, d.Value.Status);
    }

    // -- THE §22.4 acceptance gate ----------------------------------------

    [Fact]
    public async Task Acceptance_blocked_by_open_blocking_ncr_then_succeeds_and_flips_billing()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var billingId = await SeedAcceptanceGatedBillingAsync(db, pid);
        var svc = NewSvc(db);

        var ncrId = (await svc.CreateNcrAsync(new CreateNcrRequest(pid, "blocker", BlocksShipment: true))).Value;
        var accId = (await svc.CreateAcceptanceAsync(new CreateAcceptanceRequest(pid, RevenueTrigger: true))).Value;

        // blocked while the NCR is open
        var blocked = await svc.ConfirmAcceptanceAsync(new ConfirmAcceptanceRequest(accId, "Weir QA"));
        Assert.False(blocked.IsSuccess);

        // clear the NCR (disposition + close)
        await svc.DispositionNcrAsync(new DispositionNcrRequest(ncrId, ProjectQualityDisposition.UseAsIs));
        await svc.TransitionNcrAsync(ncrId, ProjectNcrStatus.Closed, "Dean");

        var okres = await svc.ConfirmAcceptanceAsync(new ConfirmAcceptanceRequest(accId, "Weir QA", AcceptedQuantity: 1));
        Assert.True(okres.IsSuccess);
        Assert.Equal(ProjectAcceptanceStatus.Accepted, okres.Value.Status);
        Assert.True(okres.Value.RevenueTriggered);
        Assert.Equal(1, okres.Value.BillingLinesConfirmed);

        // the PR-14 billing acceptance gate is now confirmed
        var billing = await db.ProjectBillingSchedules.FindAsync(billingId);
        Assert.True(billing!.AcceptanceConfirmed);
        Assert.NotNull(billing.AcceptanceConfirmedAt);
    }

    [Fact]
    public async Task Acceptance_blocked_by_pending_mrb_and_blocking_punch()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreateMrbAsync(new CreateMrbRequest(pid, "pending"));            // Pending by default
        await svc.CreatePunchItemAsync(new CreatePunchItemRequest(pid, "scratch", BlockingAcceptance: true));
        var accId = (await svc.CreateAcceptanceAsync(new CreateAcceptanceRequest(pid))).Value;
        var res = await svc.ConfirmAcceptanceAsync(new ConfirmAcceptanceRequest(accId, "Weir"));
        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task Acceptance_without_revenue_trigger_does_not_flip_billing()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var billingId = await SeedAcceptanceGatedBillingAsync(db, pid);
        var svc = NewSvc(db);
        var accId = (await svc.CreateAcceptanceAsync(new CreateAcceptanceRequest(pid, RevenueTrigger: false))).Value;
        var res = await svc.ConfirmAcceptanceAsync(new ConfirmAcceptanceRequest(accId, "Weir"));
        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value.BillingLinesConfirmed);
        Assert.False((await db.ProjectBillingSchedules.FindAsync(billingId))!.AcceptanceConfirmed);
    }

    [Fact]
    public async Task ConfirmAcceptance_is_terminal()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var accId = (await svc.CreateAcceptanceAsync(new CreateAcceptanceRequest(pid))).Value;
        Assert.True((await svc.ConfirmAcceptanceAsync(new ConfirmAcceptanceRequest(accId, "Weir"))).IsSuccess);
        Assert.False((await svc.ConfirmAcceptanceAsync(new ConfirmAcceptanceRequest(accId, "Weir"))).IsSuccess);
    }

    // -- punch + read rollup ----------------------------------------------

    [Fact]
    public async Task GetQuality_rolls_up_ship_and_acceptance_readiness()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreateNcrAsync(new CreateNcrRequest(pid, "open blocker", BlocksShipment: true));
        await svc.CreatePunchItemAsync(new CreatePunchItemRequest(pid, "ship blocker", BlockingShipment: true));

        var v = (await svc.GetQualityAsync(pid)).Value!;
        Assert.Equal(1, v.OpenNcrCount);
        Assert.Equal(1, v.BlockingNcrCount);
        Assert.False(v.ShipReady);
        Assert.False(v.AcceptanceReady);
        Assert.NotEmpty(v.AcceptanceBlockers);
    }

    [Fact]
    public async Task GetQuality_blocked_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 2);
        var svc = NewSvc(db, 1);
        Assert.False((await svc.GetQualityAsync(pid)).IsSuccess);
    }
}
