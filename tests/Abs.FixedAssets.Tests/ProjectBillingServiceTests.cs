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

// Theme B9 Wave 5 PR-14 — ProjectBillingService: schedule/invoice/recognition,
// the milestone-achieved + acceptance invoicing gates, and the bill totals.
public sealed class ProjectBillingServiceTests
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
            .UseInMemoryDatabase($"projbill-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectBillingService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectBillingService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1, decimal? contract = 185_000m)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-B-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Billing test", Status = CustomerProjectStatus.Active, ContractValue = contract,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> SeedMilestoneAsync(AppDbContext db, int pid, string code, ProjectMilestoneStatus status)
    {
        var m = new ProjectMilestone { CustomerProjectId = pid, Code = code, Name = code, Status = status };
        db.ProjectMilestones.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    // -- milestone gate ----------------------------------------------------

    [Fact]
    public async Task Cannot_invoice_milestone_line_until_milestone_achieved()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var msId = await SeedMilestoneAsync(db, pid, "MS-SHIP", ProjectMilestoneStatus.Open);
        var svc = NewSvc(db);
        var bid = (await svc.CreateBillingScheduleAsync(new CreateBillingScheduleRequest(
            pid, "BILL-1", "Final", 100_000m, ProjectBillingType.Milestone, ProjectMilestoneId: msId))).Value;

        var blocked = await svc.RecordInvoiceAsync(new RecordInvoiceRequest(bid, "INV-1", 100_000m, new DateTime(2026, 6, 26)));
        Assert.True(blocked.IsFailure);
        Assert.Contains("milestone has not been achieved", blocked.Error, StringComparison.OrdinalIgnoreCase);

        // Achieve the milestone → invoice now allowed.
        var ms = await db.ProjectMilestones.FindAsync(msId);
        ms!.Status = ProjectMilestoneStatus.Achieved;
        await db.SaveChangesAsync();

        var ok = await svc.RecordInvoiceAsync(new RecordInvoiceRequest(bid, "INV-1", 100_000m, new DateTime(2026, 6, 26)));
        Assert.True(ok.IsSuccess);
    }

    // -- acceptance gate ---------------------------------------------------

    [Fact]
    public async Task Cannot_final_bill_until_acceptance_confirmed()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var msId = await SeedMilestoneAsync(db, pid, "MS-SHIP", ProjectMilestoneStatus.Achieved);
        var svc = NewSvc(db);
        var bid = (await svc.CreateBillingScheduleAsync(new CreateBillingScheduleRequest(
            pid, "BILL-FINAL", "Final", 145_000m, ProjectBillingType.Milestone,
            ProjectMilestoneId: msId, RequiresAcceptance: true))).Value;

        var blocked = await svc.RecordInvoiceAsync(new RecordInvoiceRequest(bid, "INV-F", 145_000m, new DateTime(2026, 6, 26)));
        Assert.True(blocked.IsFailure);
        Assert.Contains("requires customer acceptance", blocked.Error, StringComparison.OrdinalIgnoreCase);

        var conf = await svc.ConfirmAcceptanceAsync(bid, "pm");
        Assert.True(conf.IsSuccess);

        var ok = await svc.RecordInvoiceAsync(new RecordInvoiceRequest(bid, "INV-F", 145_000m, new DateTime(2026, 6, 26)));
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public async Task Confirm_acceptance_is_set_once()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var bid = (await svc.CreateBillingScheduleAsync(new CreateBillingScheduleRequest(
            pid, "BILL-1", "Final", 100_000m, ProjectBillingType.Fixed, RequiresAcceptance: true))).Value;

        Assert.True((await svc.ConfirmAcceptanceAsync(bid, "pm")).IsSuccess);
        var second = await svc.ConfirmAcceptanceAsync(bid, "pm");
        Assert.True(second.IsFailure);
        Assert.Contains("already confirmed", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- non-milestone line invoices freely --------------------------------

    [Fact]
    public async Task Fixed_line_without_acceptance_invoices_freely()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var bid = (await svc.CreateBillingScheduleAsync(new CreateBillingScheduleRequest(
            pid, "BILL-DEP", "Deposit", 20_000m, ProjectBillingType.Fixed))).Value;

        var ok = await svc.RecordInvoiceAsync(new RecordInvoiceRequest(bid, "INV-DEP", 20_000m, new DateTime(2026, 5, 20)));
        Assert.True(ok.IsSuccess);
        var sched = await db.ProjectBillingSchedules.FindAsync(bid);
        Assert.Equal(ProjectBillingStatus.Invoiced, sched!.Status);
    }

    // -- totals + readiness ------------------------------------------------

    [Fact]
    public async Task Billing_view_rolls_up_totals_and_readiness()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, contract: 185_000m);
        var msAchieved = await SeedMilestoneAsync(db, pid, "MS-DESIGN", ProjectMilestoneStatus.Achieved);
        var svc = NewSvc(db);
        var b1 = (await svc.CreateBillingScheduleAsync(new CreateBillingScheduleRequest(
            pid, "BILL-1", "Progress", 40_000m, ProjectBillingType.Milestone, ProjectMilestoneId: msAchieved))).Value;
        await svc.CreateBillingScheduleAsync(new CreateBillingScheduleRequest(
            pid, "BILL-2", "Final", 145_000m, ProjectBillingType.Fixed, RequiresAcceptance: true));
        await svc.RecordInvoiceAsync(new RecordInvoiceRequest(b1, "INV-1", 40_000m, new DateTime(2026, 5, 28)));
        await svc.RecognizeRevenueAsync(new RecognizeRevenueRequest(pid, 40_000m, new DateTime(2026, 6, 1)));

        var v = (await svc.GetBillingAsync(pid)).Value!;
        Assert.Equal(185_000m, v.ScheduledTotal);
        Assert.Equal(40_000m, v.InvoicedTotal);
        Assert.Equal(40_000m, v.RecognizedTotal);
        Assert.Equal(145_000m, v.RemainingToBill);
        Assert.Equal(21.62m, v.PercentBilledOfContract);
        // The final line requires acceptance (not confirmed) ⇒ not ready.
        var fin = v.Schedule.Single(s => s.Code == "BILL-2");
        Assert.False(fin.ReadyToInvoice);
    }

    // -- tenant scope ------------------------------------------------------

    [Fact]
    public async Task CreateSchedule_rejects_project_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);
        var svc = NewSvc(db, 1);

        var res = await svc.CreateBillingScheduleAsync(new CreateBillingScheduleRequest(pid, "BILL-1", "X", 1_000m));

        Assert.True(res.IsFailure);
        Assert.Contains("tenant scope", res.Error, StringComparison.OrdinalIgnoreCase);
    }
}
