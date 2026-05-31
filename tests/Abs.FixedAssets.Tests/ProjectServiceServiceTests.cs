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

// Theme B9 Wave 6 PR-18 — ProjectServiceService: service handoff (sign-off gate),
// warranty (activate/claim), the closeout-readiness signal, and the data-driven
// AI project review that stamps the AI-summary fields. CLOSES B9.
public sealed class ProjectServiceServiceTests
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
            .UseInMemoryDatabase($"projsvc-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectServiceService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectServiceService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1, decimal? contract = 185_000m, decimal? etc = 120_000m)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-S-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Service test", Status = CustomerProjectStatus.Active, ContractValue = contract,
            EstimatedTotalCost = etc, PercentComplete = 90m, Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // -- handoff sign-off gate --------------------------------------------

    [Fact]
    public async Task SignOff_blocked_until_checklist_and_training_complete()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateServiceHandoffAsync(new CreateServiceHandoffRequest(pid, "handoff", SerialNumber: "SN-1"))).Value;

        // not complete → blocked
        Assert.False((await svc.SignOffHandoffAsync(new SignOffHandoffRequest(id, "Weir"))).IsSuccess);
        await svc.UpdateHandoffProgressAsync(new UpdateHandoffProgressRequest(id, StartupChecklistComplete: true, TrainingCompleted: true));
        var ok = await svc.SignOffHandoffAsync(new SignOffHandoffRequest(id, "Weir"));
        Assert.True(ok.IsSuccess);
        Assert.Equal(ProjectHandoffStatus.SignedOff, ok.Value.Status);
        Assert.True(ok.Value.CustomerSignoff);
        Assert.NotNull(ok.Value.CustomerSignoffAt);
        // already signed → cannot re-sign
        Assert.False((await svc.SignOffHandoffAsync(new SignOffHandoffRequest(id, "Weir"))).IsSuccess);
    }

    [Fact]
    public async Task SignedOff_handoff_cannot_reopen_only_close()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateServiceHandoffAsync(new CreateServiceHandoffRequest(pid, "h"))).Value;
        await svc.UpdateHandoffProgressAsync(new UpdateHandoffProgressRequest(id, StartupChecklistComplete: true, TrainingCompleted: true));
        await svc.SignOffHandoffAsync(new SignOffHandoffRequest(id, "Weir"));
        // backward transitions blocked
        Assert.False((await svc.TransitionHandoffAsync(id, ProjectHandoffStatus.Draft)).IsSuccess);
        Assert.False((await svc.TransitionHandoffAsync(id, ProjectHandoffStatus.Commissioned)).IsSuccess);
        // only Closed is allowed
        Assert.True((await svc.TransitionHandoffAsync(id, ProjectHandoffStatus.Closed)).IsSuccess);
    }

    [Fact]
    public async Task Handoff_numbers_per_project_and_scopes_phase()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var otherPid = await SeedProjectAsync(db);
        var phase = new ProjectPhase { CustomerProjectId = otherPid, Code = "PH-X", Name = "Other" };
        db.ProjectPhases.Add(phase); await db.SaveChangesAsync();
        var svc = NewSvc(db);
        var a = await svc.CreateServiceHandoffAsync(new CreateServiceHandoffRequest(pid, "H1"));
        var b = await svc.CreateServiceHandoffAsync(new CreateServiceHandoffRequest(pid, "H2"));
        Assert.Equal(1, (await db.ProjectServiceHandoffs.FindAsync(a.Value))!.HandoffNumber);
        Assert.Equal(2, (await db.ProjectServiceHandoffs.FindAsync(b.Value))!.HandoffNumber);
        Assert.False((await svc.CreateServiceHandoffAsync(new CreateServiceHandoffRequest(pid, "bad", AffectedPhaseId: phase.Id))).IsSuccess);
    }

    // -- warranty ----------------------------------------------------------

    [Fact]
    public async Task Warranty_activate_and_claim_increments_count()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateWarrantyAsync(new CreateWarrantyRequest(pid, "12mo", WarrantyType: ProjectWarrantyType.Full))).Value;
        var a = await svc.ActivateWarrantyAsync(new ActivateWarrantyRequest(id, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddYears(1)));
        Assert.True(a.IsSuccess);
        Assert.Equal(ProjectWarrantyStatus.Active, a.Value.Status);
        var c = await svc.TransitionWarrantyAsync(id, ProjectWarrantyStatus.Claimed);
        Assert.True(c.IsSuccess);
        Assert.Equal(1, c.Value.ClaimCount);
    }

    [Fact]
    public async Task Warranty_rejects_end_before_start()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var res = await svc.CreateWarrantyAsync(new CreateWarrantyRequest(pid, "bad",
            StartDate: DateTime.UtcNow.Date, EndDate: DateTime.UtcNow.Date.AddDays(-1)));
        Assert.False(res.IsSuccess);
    }

    // -- closeout readiness ------------------------------------------------

    [Fact]
    public async Task GetService_closeout_blocked_by_unsigned_handoff_then_ready()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var id = (await svc.CreateServiceHandoffAsync(new CreateServiceHandoffRequest(pid, "h"))).Value;

        var v1 = (await svc.GetServiceAsync(pid)).Value!;
        Assert.False(v1.CloseoutReady);
        Assert.NotEmpty(v1.CloseoutBlockers);

        await svc.UpdateHandoffProgressAsync(new UpdateHandoffProgressRequest(id, StartupChecklistComplete: true, TrainingCompleted: true));
        await svc.SignOffHandoffAsync(new SignOffHandoffRequest(id, "Weir"));

        var v2 = (await svc.GetServiceAsync(pid)).Value!;
        Assert.True(v2.CloseoutReady);
    }

    // -- AI project review -------------------------------------------------

    [Fact]
    public async Task GenerateReview_stamps_summary_and_computes_margin_and_checklist()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, contract: 185_000m, etc: 120_000m);
        var svc = NewSvc(db);

        var res = await svc.GenerateProjectReviewAsync(pid, "Dean");
        Assert.True(res.IsSuccess);
        Assert.Equal(65_000m, res.Value.ProjectedMargin);     // 185k − 120k
        Assert.Equal(35.1m, res.Value.ProjectedMarginPct);    // 65k / 185k
        Assert.NotEmpty(res.Value.CloseoutChecklist);
        Assert.Contains("Project review", res.Value.Narrative);

        // stamped onto the project AI-summary fields
        var p = await db.CustomerProjects.FindAsync(pid);
        Assert.Equal(ProjectServiceService.ReviewModel, p!.AiSummaryModel);
        Assert.NotNull(p.AiSummaryGeneratedAt);
        Assert.False(string.IsNullOrEmpty(p.AiSummaryText));
    }

    [Fact]
    public async Task GenerateReview_closeout_ready_when_everything_clear()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        // accepted acceptance + a fully-billed schedule + a signed handoff + warranty
        db.ProjectAcceptances.Add(new ProjectAcceptance { CustomerProjectId = pid, AcceptanceNumber = 1, Status = ProjectAcceptanceStatus.Accepted });
        db.ProjectBillingSchedules.Add(new ProjectBillingSchedule { CustomerProjectId = pid, Code = "B1", Name = "all", ScheduledAmount = 100_000m, Currency = "USD" });
        db.ProjectInvoiceLinks.Add(new ProjectInvoiceLink { CustomerProjectId = pid, InvoiceNumber = "INV-1", InvoicedAmount = 185_000m, InvoiceDate = DateTime.UtcNow, Currency = "USD", Status = ProjectInvoiceStatus.Issued });
        await db.SaveChangesAsync();
        var hid = (await svc.CreateServiceHandoffAsync(new CreateServiceHandoffRequest(pid, "h"))).Value;
        await svc.UpdateHandoffProgressAsync(new UpdateHandoffProgressRequest(hid, StartupChecklistComplete: true, TrainingCompleted: true));
        await svc.SignOffHandoffAsync(new SignOffHandoffRequest(hid, "Weir"));
        var wid = (await svc.CreateWarrantyAsync(new CreateWarrantyRequest(pid, "w", ProjectServiceHandoffId: hid))).Value;
        await svc.ActivateWarrantyAsync(new ActivateWarrantyRequest(wid));

        var res = await svc.GenerateProjectReviewAsync(pid);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.CloseoutReady);
        Assert.Contains("Closeout READY", res.Value.Narrative);
    }

    [Fact]
    public async Task GetService_blocked_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 2);
        var svc = NewSvc(db, 1);
        Assert.False((await svc.GetServiceAsync(pid)).IsSuccess);
    }
}
