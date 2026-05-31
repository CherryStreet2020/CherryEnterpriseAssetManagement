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

// Theme B9 Wave 6 PR-16 — ProjectGovernanceService: RAID + meetings — numbering,
// peg scoping, risk exposure + terminal guards, set-once close/complete stamps,
// overdue-action rollup, and the governance read rollup.
public sealed class ProjectGovernanceServiceTests
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
            .UseInMemoryDatabase($"projgov-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectGovernanceService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectGovernanceService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1, string currency = "USD")
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-G-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Gov test", Status = CustomerProjectStatus.Active, ContractValue = 185_000m,
            Mode = CustomerProjectMode.Standard, Currency = currency,
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // -- risk numbering + exposure -----------------------------------------

    [Fact]
    public async Task CreateRisk_numbers_and_computes_exposure_and_defaults_currency()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, currency: "EUR");
        var svc = NewSvc(db);

        var r1 = await svc.CreateRiskAsync(new CreateRiskRequest(pid, "R1", Probability: ProjectRiskRating.High, Impact: ProjectRiskRating.Medium));
        var r2 = await svc.CreateRiskAsync(new CreateRiskRequest(pid, "R2"));
        Assert.True(r1.IsSuccess);
        var risk1 = await db.ProjectRisks.FindAsync(r1.Value);
        var risk2 = await db.ProjectRisks.FindAsync(r2.Value);
        Assert.Equal(1, risk1!.RiskNumber);
        Assert.Equal(2, risk2!.RiskNumber);
        Assert.Equal(12, risk1.Exposure);   // High(4) × Medium(3)
        Assert.Equal(0, risk2.Exposure);     // NotSet → 0
        Assert.Equal("EUR", risk1.Currency);
    }

    [Fact]
    public async Task UpdateRiskAssessment_recomputes_exposure_then_terminal_blocks()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateRiskAsync(new CreateRiskRequest(pid, "R"))).Value;

        var u = await svc.UpdateRiskAssessmentAsync(new UpdateRiskAssessmentRequest(id, ProjectRiskRating.VeryHigh, ProjectRiskRating.High));
        Assert.True(u.IsSuccess);
        Assert.Equal(20, u.Value.Exposure);  // 5 × 4

        Assert.True((await svc.TransitionRiskAsync(id, ProjectRiskStatus.Accepted, "Dean")).IsSuccess);
        // Accepted is terminal → re-assess blocked, and re-transition blocked.
        Assert.False((await svc.UpdateRiskAssessmentAsync(new UpdateRiskAssessmentRequest(id, ProjectRiskRating.Low, ProjectRiskRating.Low))).IsSuccess);
        Assert.False((await svc.TransitionRiskAsync(id, ProjectRiskStatus.Open)).IsSuccess);
    }

    [Fact]
    public async Task TransitionRisk_to_closed_stamps_set_once()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateRiskAsync(new CreateRiskRequest(pid, "R"))).Value;
        var t = await svc.TransitionRiskAsync(id, ProjectRiskStatus.Closed, "Dean");
        Assert.True(t.IsSuccess);
        Assert.NotNull(t.Value.ClosedAt);
        Assert.Equal("Dean", t.Value.ClosedBy);  // case-preserved ("closedby")
    }

    [Fact]
    public async Task CreateRisk_rejects_phase_from_another_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var otherPid = await SeedProjectAsync(db);
        var phase = new ProjectPhase { CustomerProjectId = otherPid, Code = "PH-X", Name = "Other" };
        db.ProjectPhases.Add(phase); await db.SaveChangesAsync();
        var svc = NewSvc(db);
        Assert.False((await svc.CreateRiskAsync(new CreateRiskRequest(pid, "R", AffectedPhaseId: phase.Id))).IsSuccess);
    }

    // -- issues ------------------------------------------------------------

    [Fact]
    public async Task TransitionIssue_close_stamps_closed_date_and_is_terminal()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateIssueAsync(new CreateIssueRequest(pid, "I", Severity: ProjectIssueSeverity.Critical))).Value;
        var t = await svc.TransitionIssueAsync(id, ProjectIssueStatus.Closed, "Dean");
        Assert.True(t.IsSuccess);
        Assert.NotNull(t.Value.ClosedDate);
        Assert.NotNull(t.Value.ClosedAt);
        Assert.False((await svc.TransitionIssueAsync(id, ProjectIssueStatus.Open)).IsSuccess);
    }

    // -- meetings + action items -------------------------------------------

    [Fact]
    public async Task ActionItem_requires_meeting_in_same_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var otherPid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var otherMeeting = (await svc.CreateMeetingAsync(new CreateMeetingRequest(otherPid, "M"))).Value;
        // Meeting belongs to another project → rejected.
        Assert.False((await svc.CreateActionItemAsync(new CreateActionItemRequest(pid, "do", ProjectMeetingId: otherMeeting))).IsSuccess);
    }

    [Fact]
    public async Task ActionItem_rejects_source_id_from_another_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var otherPid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        // A risk in ANOTHER project — must not be accepted as this action's source.
        var foreignRisk = (await svc.CreateRiskAsync(new CreateRiskRequest(otherPid, "foreign"))).Value;
        var res = await svc.CreateActionItemAsync(new CreateActionItemRequest(
            pid, "do", Source: ProjectActionSource.Risk, SourceId: foreignRisk));
        Assert.False(res.IsSuccess);
        // A risk in THIS project is accepted.
        var ownRisk = (await svc.CreateRiskAsync(new CreateRiskRequest(pid, "own"))).Value;
        Assert.True((await svc.CreateActionItemAsync(new CreateActionItemRequest(
            pid, "do", Source: ProjectActionSource.Risk, SourceId: ownRisk))).IsSuccess);
    }

    [Fact]
    public async Task CompleteActionItem_stamps_completion_and_is_terminal()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var mid = (await svc.CreateMeetingAsync(new CreateMeetingRequest(pid, "M"))).Value;
        var aid = (await svc.CreateActionItemAsync(new CreateActionItemRequest(pid, "expedite PO", Owner: "Dean", ProjectMeetingId: mid))).Value;
        var t = await svc.TransitionActionItemAsync(aid, ProjectActionStatus.Done, "Dean");
        Assert.True(t.IsSuccess);
        Assert.NotNull(t.Value.CompletionDate);
        Assert.Equal("Dean", t.Value.CompletedBy);
        Assert.False((await svc.TransitionActionItemAsync(aid, ProjectActionStatus.Open)).IsSuccess);
    }

    // -- decisions ---------------------------------------------------------

    [Fact]
    public async Task Decision_rejected_is_terminal()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.RecordDecisionAsync(new RecordDecisionRequest(pid, "D"))).Value;
        Assert.True((await svc.TransitionDecisionAsync(id, ProjectDecisionStatus.Rejected)).IsSuccess);
        Assert.False((await svc.TransitionDecisionAsync(id, ProjectDecisionStatus.Approved)).IsSuccess);
    }

    // -- read rollup -------------------------------------------------------

    [Fact]
    public async Task GetGovernance_rolls_up_open_counts_top_exposure_and_overdue()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);

        await svc.CreateRiskAsync(new CreateRiskRequest(pid, "open-risk", Probability: ProjectRiskRating.High, Impact: ProjectRiskRating.High, CostExposure: 22_000m));
        var closedRisk = (await svc.CreateRiskAsync(new CreateRiskRequest(pid, "closed-risk", Probability: ProjectRiskRating.Low, Impact: ProjectRiskRating.Low))).Value;
        await svc.TransitionRiskAsync(closedRisk, ProjectRiskStatus.Closed, "Dean");

        await svc.CreateIssueAsync(new CreateIssueRequest(pid, "crit", Severity: ProjectIssueSeverity.Critical));

        // An overdue open action (due yesterday).
        await svc.CreateActionItemAsync(new CreateActionItemRequest(pid, "late", DueDate: DateTime.UtcNow.Date.AddDays(-1)));
        // A not-overdue open action (due next week).
        await svc.CreateActionItemAsync(new CreateActionItemRequest(pid, "soon", DueDate: DateTime.UtcNow.Date.AddDays(7)));

        await svc.RecordDecisionAsync(new RecordDecisionRequest(pid, "decided"));
        await svc.CreateMeetingAsync(new CreateMeetingRequest(pid, "kickoff"));

        var v = (await svc.GetGovernanceAsync(pid)).Value!;
        Assert.Equal(1, v.OpenRiskCount);            // closed one excluded
        Assert.Equal(16, v.TopRiskExposure);         // High×High
        Assert.Equal(22_000m, v.OpenRiskCostExposure);
        Assert.Equal(1, v.OpenIssueCount);
        Assert.Equal(1, v.CriticalIssueCount);
        Assert.Equal(2, v.OpenActionCount);
        Assert.Equal(1, v.OverdueActionCount);
        Assert.Equal(1, v.DecisionCount);
        Assert.Equal(1, v.MeetingCount);
    }

    [Fact]
    public async Task GetGovernance_blocked_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 2);
        var svc = NewSvc(db, 1);
        Assert.False((await svc.GetGovernanceAsync(pid)).IsSuccess);
    }
}
