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

// Theme B9 Wave 6 PR-15 — ProjectChangeService: change-request intake/numbering,
// impact estimation, the disposition legal-map + set-once stamps, and the
// convert-to-change-order §20 gate (no apply before approval; idempotent;
// cross-linked to ProjectAmendment).
public sealed class ProjectChangeServiceTests
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
            .UseInMemoryDatabase($"projchg-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectChangeService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectChangeService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1, decimal? contract = 185_000m, string currency = "USD")
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-C-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Change test", Status = CustomerProjectStatus.Active, ContractValue = contract,
            Mode = CustomerProjectMode.Standard, Currency = currency,
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // Walk a request all the way to CustomerApproved (both approvals required).
    private static async Task DriveToCustomerApprovedAsync(ProjectChangeService svc, int crId)
    {
        Assert.True((await svc.UpdateImpactAsync(new UpdateChangeImpactRequest(crId, 18_400m, 27_500m, 1.2m, 9))).IsSuccess);
        Assert.True((await svc.TransitionAsync(new TransitionChangeRequest(crId, ProjectChangeRequestStatus.InternalApproved, "Dean"))).IsSuccess);
        Assert.True((await svc.TransitionAsync(new TransitionChangeRequest(crId, ProjectChangeRequestStatus.SubmittedToCustomer))).IsSuccess);
        Assert.True((await svc.TransitionAsync(new TransitionChangeRequest(crId, ProjectChangeRequestStatus.CustomerApproved, "Weir"))).IsSuccess);
    }

    // -- create + numbering -------------------------------------------------

    [Fact]
    public async Task Create_assigns_monotonic_per_project_number_and_defaults_currency()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, currency: "EUR");
        var svc = NewSvc(db);

        var r1 = await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "First"));
        var r2 = await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Second"));
        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);

        var c1 = await db.ProjectChangeRequests.FindAsync(r1.Value);
        var c2 = await db.ProjectChangeRequests.FindAsync(r2.Value);
        Assert.Equal(1, c1!.ChangeRequestNumber);
        Assert.Equal(2, c2!.ChangeRequestNumber);
        Assert.Equal("EUR", c1.Currency); // defaulted to the project currency
    }

    [Fact]
    public async Task Create_rejects_phase_from_another_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var otherPid = await SeedProjectAsync(db);
        var phase = new ProjectPhase { CustomerProjectId = otherPid, Code = "PH-X", Name = "Other phase" };
        db.ProjectPhases.Add(phase);
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var res = await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Bad phase", AffectedPhaseId: phase.Id));
        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task Create_blocked_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 2);
        var svc = NewSvc(db, 1); // visible = company 1 only
        var res = await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "X"));
        Assert.False(res.IsSuccess);
    }

    // -- impact -------------------------------------------------------------

    [Fact]
    public async Task UpdateImpact_advances_draft_to_estimated()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Scope"))).Value;

        var res = await svc.UpdateImpactAsync(new UpdateChangeImpactRequest(id, 1_000m, 2_500m, 0.5m, 3, ProjectChangeRiskLevel.Low, "narrative"));
        Assert.True(res.IsSuccess);
        Assert.Equal(ProjectChangeRequestStatus.Estimated, res.Value.Status);
        Assert.Equal(2_500m, res.Value.RevenueImpact);
    }

    // -- legal map + set-once stamps ---------------------------------------

    [Fact]
    public async Task Transition_stamps_internal_then_customer_approval()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Scope"))).Value;
        await svc.UpdateImpactAsync(new UpdateChangeImpactRequest(id, 1m, 2m));

        var ia = await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.InternalApproved, "Dean"));
        Assert.True(ia.IsSuccess);
        Assert.NotNull(ia.Value.InternalApprovedAt);
        // Case-preserved by the normalizer "approvedby" allowlist token.
        Assert.Equal("Dean", ia.Value.InternalApprovedBy);

        await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.SubmittedToCustomer));
        var ca = await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.CustomerApproved, "Weir"));
        Assert.True(ca.IsSuccess);
        Assert.NotNull(ca.Value.CustomerApprovedAt);
    }

    [Fact]
    public async Task Transition_rejects_illegal_skip()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Scope"))).Value;
        // Draft → CustomerApproved is illegal (must walk the map).
        var res = await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.CustomerApproved));
        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task Transition_skips_internal_when_not_required()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(
            pid, "Internal-only", RequiresInternalApproval: false, RequiresCustomerApproval: false))).Value;
        await svc.UpdateImpactAsync(new UpdateChangeImpactRequest(id, 1m, 2m));
        // No approvals required → Estimated → CustomerApproved is legal.
        var res = await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.CustomerApproved));
        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Cancelled_is_terminal()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Scope"))).Value;
        Assert.True((await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.Cancelled))).IsSuccess);
        // No outgoing transition from Cancelled.
        Assert.False((await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.UnderReview))).IsSuccess);
    }

    // -- convert (§20 gate) -------------------------------------------------

    [Fact]
    public async Task Convert_blocked_before_customer_approval()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Scope"))).Value;
        await svc.UpdateImpactAsync(new UpdateChangeImpactRequest(id, 1m, 2m));
        await svc.TransitionAsync(new TransitionChangeRequest(id, ProjectChangeRequestStatus.InternalApproved, "Dean"));
        // Internal-approved but customer approval still required → blocked.
        var res = await svc.ConvertToChangeOrderAsync(new ConvertToChangeOrderRequest(id, DateTime.UtcNow));
        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task Convert_after_approval_creates_linked_amendment()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Add arm"))).Value;
        await DriveToCustomerApprovedAsync(svc, id);

        var res = await svc.ConvertToChangeOrderAsync(new ConvertToChangeOrderRequest(
            id, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), ApproveImmediately: true, ApprovedByName: "Dean"));
        Assert.True(res.IsSuccess);
        Assert.Equal(27_500m, res.Value.ValueDelta);          // = RevenueImpact
        Assert.Equal(ProjectAmendmentStatus.Approved, res.Value.AmendmentStatus);

        var cr = await db.ProjectChangeRequests.FindAsync(id);
        Assert.Equal(ProjectChangeRequestStatus.Converted, cr!.Status);
        Assert.Equal(res.Value.ProjectAmendmentId, cr.ResultingProjectAmendmentId);

        var am = await db.ProjectAmendments.FindAsync(res.Value.ProjectAmendmentId);
        Assert.Equal(id, am!.SourceChangeRequestId);
        Assert.Equal(9, am.TargetEndDateDelta);               // = ScheduleImpactDays
    }

    [Fact]
    public async Task Convert_is_idempotent_second_call_fails()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Add arm"))).Value;
        await DriveToCustomerApprovedAsync(svc, id);

        Assert.True((await svc.ConvertToChangeOrderAsync(new ConvertToChangeOrderRequest(id, DateTime.UtcNow))).IsSuccess);
        Assert.False((await svc.ConvertToChangeOrderAsync(new ConvertToChangeOrderRequest(id, DateTime.UtcNow))).IsSuccess);
    }

    // -- read rollup --------------------------------------------------------

    [Fact]
    public async Task GetChanges_rolls_up_effective_contract_and_exposure()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, contract: 185_000m);
        var svc = NewSvc(db);

        // One in-flight request (exposure, not yet approved).
        var pending = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Pending"))).Value;
        await svc.UpdateImpactAsync(new UpdateChangeImpactRequest(pending, 5_000m, 12_000m));

        // One approved + converted → contributes to effective contract value.
        var approved = (await svc.CreateChangeRequestAsync(new CreateChangeRequestRequest(pid, "Approved"))).Value;
        await DriveToCustomerApprovedAsync(svc, approved);
        await svc.ConvertToChangeOrderAsync(new ConvertToChangeOrderRequest(approved, DateTime.UtcNow, ApproveImmediately: true, ApprovedByName: "Dean"));

        var view = (await svc.GetChangesAsync(pid)).Value;
        Assert.Equal(185_000m, view.BaselineContractValue);
        Assert.Equal(27_500m, view.ApprovedChangeValue);          // converted+approved amendment delta
        Assert.Equal(212_500m, view.EffectiveContractValue);
        Assert.Equal(12_000m, view.PendingRevenueExposure);       // only the in-flight one
        Assert.Equal(1, view.OpenChangeRequestCount);             // converted is terminal
        Assert.Equal(1, view.ChangeOrderCount);
    }
}
