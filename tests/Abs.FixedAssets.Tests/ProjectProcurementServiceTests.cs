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

// Theme B9 Wave 4 PR-10 — ProjectProcurementService: plan/commitment/receipt
// CRUD + tenant-scoping, received-balance math + status auto-advance, the
// project-close gate (blocked by open commitments unless waived), and PO pegging.
public sealed class ProjectProcurementServiceTests
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
            .UseInMemoryDatabase($"projproc-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectProcurementService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectProcurementService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1,
        CustomerProjectStatus status = CustomerProjectStatus.Active)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-P-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Procurement test", Status = status,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // -- create ------------------------------------------------------------

    [Fact]
    public async Task Create_plan_commitment_and_receipt_succeed()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);

        var plan = await svc.CreatePlanAsync(new CreateProcurementPlanRequest(pid, "P1", "Ti bar stock", PlannedAmount: 40_000m));
        var cmt = await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 38_500m, ProjectProcurementPlanId: plan.Value));
        var recv = await svc.RecordReceiptAsync(new RecordReceiptRequest(cmt.Value, 10_000m));

        Assert.True(plan.IsSuccess);
        Assert.True(cmt.IsSuccess);
        Assert.True(recv.IsSuccess);
        Assert.Equal(ProjectCommitmentStatus.PartiallyReceived, recv.Value!.Status);
    }

    [Fact]
    public async Task Create_commitment_rejects_plan_from_another_project()
    {
        using var db = NewDb();
        var p1 = await SeedProjectAsync(db);
        var p2 = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var planP2 = (await svc.CreatePlanAsync(new CreateProcurementPlanRequest(p2, "P", "Other"))).Value;

        var res = await svc.CreateCommitmentAsync(new CreateCommitmentRequest(p1, "C1", 100m, ProjectProcurementPlanId: planP2));

        Assert.True(res.IsFailure);
        Assert.Contains("not in this project", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_commitment_rejects_project_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);   // company 99
        var svc = NewSvc(db, 1);                                // tenant sees only company 1

        var res = await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 100m));

        Assert.True(res.IsFailure);
        Assert.Contains("tenant scope", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_commitment_code_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 100m));

        var dup = await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 200m));

        Assert.True(dup.IsFailure);
        Assert.Contains("already exists", dup.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- receipt math + status auto-advance --------------------------------

    [Fact]
    public async Task Full_receipt_advances_commitment_to_received()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var cid = (await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 5_000m))).Value;

        await svc.RecordReceiptAsync(new RecordReceiptRequest(cid, 2_000m));
        var second = await svc.RecordReceiptAsync(new RecordReceiptRequest(cid, 3_000m));

        Assert.True(second.IsSuccess);
        Assert.Equal(ProjectCommitmentStatus.Received, second.Value!.Status);

        var view = (await svc.GetProcurementAsync(pid)).Value!;
        var row = view.Commitments.Single();
        Assert.Equal(5_000m, row.ReceivedToDate);
        Assert.Equal(0m, row.OpenBalance);
        Assert.False(row.IsOpen);
    }

    [Fact]
    public async Task Receipt_against_closed_commitment_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var cid = (await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 5_000m))).Value;
        await svc.CloseCommitmentAsync(cid, "tester");

        var res = await svc.RecordReceiptAsync(new RecordReceiptRequest(cid, 100m));

        Assert.True(res.IsFailure);
        Assert.Contains("closed", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- the close gate ----------------------------------------------------

    [Fact]
    public async Task Close_project_blocked_by_open_commitment()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "CMT-OPEN", 10_000m));

        var res = await svc.CloseProjectAsync(new CloseProjectRequest(pid));

        Assert.True(res.IsFailure);
        Assert.Contains("open commitment", res.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CMT-OPEN", res.Error);
    }

    [Fact]
    public async Task Close_project_blocked_by_unsigned_service_handoff()
    {
        // B9 Wave 6 PR-18 — the equipment closeout gate is enforced on THIS path too.
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        db.Set<Abs.FixedAssets.Models.Projects.ProjectServiceHandoff>().Add(
            new Abs.FixedAssets.Models.Projects.ProjectServiceHandoff
            {
                CustomerProjectId = pid, HandoffNumber = 1, Title = "unsigned",
                Status = Abs.FixedAssets.Models.Projects.ProjectHandoffStatus.Draft,
            });
        await db.SaveChangesAsync();

        var res = await svc.CloseProjectAsync(new CloseProjectRequest(pid));
        Assert.True(res.IsFailure);
        Assert.Contains("signed off", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Close_project_succeeds_with_waiver()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "CMT-OPEN", 10_000m));

        var res = await svc.CloseProjectAsync(new CloseProjectRequest(pid, WaiveOpenCommitments: true, ClosedBy: "pm"));

        Assert.True(res.IsSuccess);
        Assert.Equal(CustomerProjectStatus.Closed, res.Value!.Status);
        Assert.NotNull(res.Value!.ClosedAt);
    }

    [Fact]
    public async Task Close_project_succeeds_when_commitments_fully_received()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var cid = (await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 4_000m))).Value;
        await svc.RecordReceiptAsync(new RecordReceiptRequest(cid, 4_000m)); // → Received (not open)

        var res = await svc.CloseProjectAsync(new CloseProjectRequest(pid));

        Assert.True(res.IsSuccess);
        Assert.Equal(CustomerProjectStatus.Closed, res.Value!.Status);
    }

    [Fact]
    public async Task Close_project_rejects_non_active_status()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, status: CustomerProjectStatus.Quote);
        var svc = NewSvc(db);

        var res = await svc.CloseProjectAsync(new CloseProjectRequest(pid));

        Assert.True(res.IsFailure);
        Assert.Contains("Active or OnHold", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetOpenCommitments_lists_only_open()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "OPEN-1", 1_000m));
        var received = (await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "DONE-1", 1_000m))).Value;
        await svc.RecordReceiptAsync(new RecordReceiptRequest(received, 1_000m)); // → Received

        var open = (await svc.GetOpenCommitmentsAsync(pid)).Value!;

        Assert.Single(open);
        Assert.Equal("OPEN-1", open[0]);
    }

    // -- PO pegging --------------------------------------------------------

    [Fact]
    public async Task Link_purchase_order_pegs_po_to_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var po = new PurchaseOrder { PONumber = "PO-1001", VendorId = 1, CompanyId = 1, Currency = "USD" };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var res = await svc.LinkPurchaseOrderToProjectAsync(new LinkPurchaseOrderRequest(po.Id, pid));

        Assert.True(res.IsSuccess);
        var reloaded = await db.PurchaseOrders.FindAsync(po.Id);
        Assert.Equal(pid, reloaded!.CustomerProjectId);
    }

    [Fact]
    public async Task Procurement_view_rolls_up_totals()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreatePlanAsync(new CreateProcurementPlanRequest(pid, "P1", "Ti", PlannedAmount: 40_000m));
        var cid = (await svc.CreateCommitmentAsync(new CreateCommitmentRequest(pid, "C1", 38_500m))).Value;
        await svc.RecordReceiptAsync(new RecordReceiptRequest(cid, 5_000m));

        var view = (await svc.GetProcurementAsync(pid)).Value!;

        Assert.Equal(40_000m, view.PlannedTotal);
        Assert.Equal(38_500m, view.CommittedTotal);
        Assert.Equal(5_000m, view.ReceivedTotal);
        Assert.Equal(1, view.OpenCommitmentCount);
        Assert.Equal(33_500m, view.OpenCommitmentTotal);
    }
}
