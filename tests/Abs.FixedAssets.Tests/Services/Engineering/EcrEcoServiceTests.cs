// Sprint 14.3 PR-1 (2026-05-27) — EcrEcoServiceTests.
//
// Realistic aerospace + manufacturing fixtures per HARD LOCK no-fake-data:
//   ECR-2026-00001 — Boeing-mandated dimensional tightening on Trent bracket
//                    Rev A (CustomerRequest, Expedited, Form+Fit affected,
//                    customer notice required).
//   ECR-2026-00002 — Internal DesignDefect on bracket M8 fastener (Routine,
//                    Function affected only, no customer notice).
//   ECR-2026-00003 — Supplier issue on AMS6520 heat-lot from secondary
//                    supplier (SupplierIssue, Emergency, Safety affected).
//
// Coverage (16 tests):
//   1. CreateEcr happy path with F/F/F flags.
//   2. CreateEcr duplicate EcrNumber same tenant throws.
//   3. CreateEcr cross-tenant LinkedItem refused.
//   4. SubmitEcr flips Draft → Submitted.
//   5. SubmitEcr on Submitted throws.
//   6. ApproveEcrAndCreateEco creates ECO atomically + inherits F/F/F as RequiresFAI.
//   7. ApproveEcrAndCreateEco inherits Urgency + AffectsCustomers as RequiresCustomerNotice.
//   8. RejectEcr stamps RejectionReason.
//   9. AddEcoLineItem auto-advances Sequence.
//  10. AddEcoLineItem cross-tenant AffectedItem refused.
//  11. AddEcoApprovalStage idempotent on (Eco, StageOrder).
//  12. AddEcoApprovalStage first stage flips ECO Draft → InApproval.
//  13. ApproveEcoStage out-of-order throws.
//  14. ApproveEcoStage all stages green flips ECO to Approved.
//  15. ReleaseEco with NewDocumentVersionId triggers atomic DocumentVersion supersede.
//  16. ImplementEco → CloseEco lifecycle.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Engineering;

public class EcrEcoServiceTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.CostLayer>().Ignore(c => c.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.ItemSourcingRule>().Ignore(r => r.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.CustomerItemXref>().Ignore(x => x.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Production.ProductionMaterialStructure>().Ignore(p => p.RowVersion);
            mb.Entity<Document>().Ignore(d => d.RowVersion);
            mb.Entity<DocumentVersion>().Ignore(d => d.RowVersion);
            mb.Entity<ItemDocumentLink>().Ignore(l => l.RowVersion);
            mb.Entity<EngineeringChangeRequest>().Ignore(e => e.RowVersion);
            mb.Entity<EngineeringChangeOrder>().Ignore(e => e.RowVersion);
            mb.Entity<EcoLineItem>().Ignore(l => l.RowVersion);
            mb.Entity<EcoApproval>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecreco-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static (EcrEcoService svc, DocumentService docSvc) NewServices(AppDbContext db)
    {
        var docSvc = new DocumentService(db, NullLogger<DocumentService>.Instance);
        var svc = new EcrEcoService(db, docSvc, NullLogger<EcrEcoService>.Instance);
        return (svc, docSvc);
    }

    private static Item TrentBracketItem(int companyId = 1) => new()
    {
        Id = 9501,
        PartNumber = "ASM-TRENT-BRACKET-A",
        Description = "Rolls-Royce Trent XWB Engine Bracket Assembly",
        Revision = "A",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.Internal,
        IsActive = true,
        IsSellable = true,
        AS9100Critical = true,
        CompanyId = companyId,
    };

    [Fact]
    public async Task CreateEcr_HappyPath_WithFffFlags()
    {
        await using var db = NewDb();
        db.Items.Add(TrentBracketItem());
        await db.SaveChangesAsync();

        var (svc, _) = NewServices(db);
        var r = await svc.CreateEcrAsync(
            companyId: 1,
            ecrNumber: "ECR-2026-00001",
            title: "Boeing-mandated dimensional tightening on Trent bracket Rev A",
            description: "Boeing CCN reduces tolerance on M8 hole pattern from ±0.05 to ±0.02. F/F change requires AS9102 FAI re-baseline.",
            changeReason: ChangeReason.CustomerRequest,
            urgency: ChangeUrgency.Expedited,
            affectsForm: true,
            affectsFit: true,
            affectsFunction: false,
            affectsSafety: false,
            affectsCustomers: true,
            affectsRegulatory: false,
            linkedItemId: 9501,
            linkedDocumentId: null,
            linkedProductionOrderId: null,
            linkedCustomerId: null,
            requestedBy: "engineer-1",
            ct: CancellationToken.None);

        Assert.True(r.IsSuccess, r.Error);
        Assert.Equal("ECR-2026-00001", r.Value!.EcrNumber);
        Assert.Equal(EcrStatus.Draft, r.Value.Status);
        Assert.Equal(ChangeReason.CustomerRequest, r.Value.ChangeReason);
        Assert.True(r.Value.AffectsForm);
        Assert.True(r.Value.AffectsFit);
        Assert.False(r.Value.AffectsFunction);
        Assert.True(r.Value.AffectsCustomers);
    }

    [Fact]
    public async Task CreateEcr_DuplicateEcrNumber_SameTenant_Throws()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        await svc.CreateEcrAsync(1, "ECR-2026-00001", "First", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None);

        var dup = await svc.CreateEcrAsync(1, "ECR-2026-00001", "Duplicate attempt", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e2", CancellationToken.None);

        Assert.False(dup.IsSuccess);
        Assert.Contains("already exists", dup.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateEcr_CrossTenant_LinkedItem_Refused()
    {
        await using var db = NewDb();
        db.Items.Add(TrentBracketItem(companyId: 2)); // Item belongs to tenant 2
        await db.SaveChangesAsync();

        var (svc, _) = NewServices(db);
        var r = await svc.CreateEcrAsync(
            companyId: 1, // ECR for tenant 1
            ecrNumber: "ECR-2026-00001",
            title: "Cross-tenant attempt",
            description: null,
            changeReason: ChangeReason.Other,
            urgency: ChangeUrgency.Routine,
            affectsForm: false, affectsFit: false, affectsFunction: false,
            affectsSafety: false, affectsCustomers: false, affectsRegulatory: false,
            linkedItemId: 9501,
            linkedDocumentId: null, linkedProductionOrderId: null, linkedCustomerId: null,
            requestedBy: "e1", ct: CancellationToken.None);

        Assert.False(r.IsSuccess);
        Assert.Contains("Cross-tenant", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitEcr_Flips_DraftToSubmitted()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;

        var sub = await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);

        Assert.True(sub.IsSuccess);
        Assert.Equal(EcrStatus.Submitted, sub.Value!.Status);
        Assert.NotNull(sub.Value.SubmittedAtUtc);
    }

    [Fact]
    public async Task SubmitEcr_OnSubmitted_Throws()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);

        var attempt = await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);

        Assert.False(attempt.IsSuccess);
        Assert.Contains("only Draft", attempt.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveEcr_CreatesEco_InheritsFffAsRequiresFai()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "Boeing tightening", null,
            ChangeReason.CustomerRequest, ChangeUrgency.Expedited,
            affectsForm: true, affectsFit: true, affectsFunction: false,
            affectsSafety: false, affectsCustomers: false, affectsRegulatory: false,
            null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);

        var ap = await svc.ApproveEcrAndCreateEcoAsync(
            ecr.Id, "ECO-2026-00001", "Trent bracket Rev B release",
            EcoEffectivityType.Immediate, null, null, null, null, null, null,
            "qa-lead", CancellationToken.None);

        Assert.True(ap.IsSuccess, ap.Error);
        Assert.Equal(EcrStatus.Approved, ap.Value!.Ecr.Status);
        Assert.Equal(ecr.Id, ap.Value.Eco.SourceEcrId);
        Assert.Equal(EcoStatus.Draft, ap.Value.Eco.Status);
        Assert.True(ap.Value.Eco.RequiresFaiRetrigger); // F+F → true
        Assert.False(ap.Value.Eco.RequiresCustomerNotice); // affectsCustomers was false
    }

    [Fact]
    public async Task ApproveEcr_InheritsUrgency_AndAffectsCustomersAsRequiresCustomerNotice()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "Customer-notice case", null,
            ChangeReason.CustomerRequest, ChangeUrgency.Emergency,
            false, false, false, false,
            affectsCustomers: true,
            affectsRegulatory: true,
            null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);

        var ap = await svc.ApproveEcrAndCreateEcoAsync(
            ecr.Id, "ECO-2026-00001", "Emergency change",
            EcoEffectivityType.Immediate, null, null, null, null, null, null,
            "qa-lead", CancellationToken.None);

        Assert.True(ap.IsSuccess);
        Assert.Equal(ChangeUrgency.Emergency, ap.Value!.Eco.Urgency);
        Assert.True(ap.Value.Eco.RequiresCustomerNotice);
        Assert.True(ap.Value.Eco.RequiresRegulatoryNotice);
        Assert.False(ap.Value.Eco.RequiresFaiRetrigger); // no F/F/F flags
    }

    [Fact]
    public async Task RejectEcr_Stamps_RejectionReason()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "Reject test", null,
            ChangeReason.CostReduction, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);

        var rej = await svc.RejectEcrAsync(ecr.Id, "Cost > expected benefit; revisit Q3", "qa-lead", CancellationToken.None);

        Assert.True(rej.IsSuccess);
        Assert.Equal(EcrStatus.Rejected, rej.Value!.Status);
        Assert.Equal("Cost > expected benefit; revisit Q3", rej.Value.RejectionReason);
        Assert.NotNull(rej.Value.DecidedAtUtc);
    }

    [Fact]
    public async Task AddEcoLineItem_AutoAdvancesSequence()
    {
        await using var db = NewDb();
        db.Items.Add(TrentBracketItem());
        await db.SaveChangesAsync();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "t",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;

        var l1 = await svc.AddEcoLineItemAsync(ap.Eco.Id, 9501, null, null, null, "First line", "old", "new", EcoLineItemDisposition.UseAsIs, null, "e1", CancellationToken.None);
        var l2 = await svc.AddEcoLineItemAsync(ap.Eco.Id, 9501, null, null, null, "Second line", "old2", "new2", EcoLineItemDisposition.Rework, null, "e1", CancellationToken.None);

        Assert.Equal(10, l1.Value!.Sequence);
        Assert.Equal(20, l2.Value!.Sequence);
    }

    [Fact]
    public async Task AddEcoLineItem_CrossTenant_AffectedItem_Refused()
    {
        await using var db = NewDb();
        db.Items.Add(TrentBracketItem(companyId: 2));
        await db.SaveChangesAsync();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "t",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;

        var attempt = await svc.AddEcoLineItemAsync(ap.Eco.Id, 9501, null, null, null, "cross-tenant", "a", "b", EcoLineItemDisposition.NotApplicable, null, "e1", CancellationToken.None);

        Assert.False(attempt.IsSuccess);
        Assert.Contains("Cross-tenant", attempt.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddEcoApprovalStage_Idempotent()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "t",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;

        var s1 = await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 1, "Engineering Lead", null, "e1", CancellationToken.None);
        var dup = await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 1, "Different Role Name", null, "e2", CancellationToken.None);

        Assert.True(s1.IsSuccess);
        Assert.True(dup.IsSuccess);
        Assert.Equal(s1.Value!.Id, dup.Value!.Id);
        Assert.Equal("Engineering Lead", dup.Value.ApprovalRole); // existing role preserved, not overwritten
    }

    [Fact]
    public async Task AddFirstStage_Flips_EcoDraftToInApproval()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "t",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;
        Assert.Equal(EcoStatus.Draft, ap.Eco.Status);

        await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 1, "Engineering Lead", null, "e1", CancellationToken.None);

        var reloadedEco = await db.EngineeringChangeOrders.FirstAsync(e => e.Id == ap.Eco.Id);
        Assert.Equal(EcoStatus.InApproval, reloadedEco.Status);
    }

    [Fact]
    public async Task ApproveEcoStage_OutOfOrder_Throws()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "t",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;
        var s1 = (await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 1, "Engineering Lead", null, "e1", CancellationToken.None)).Value!;
        var s2 = (await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 2, "Quality Manager", null, "e1", CancellationToken.None)).Value!;

        // Try to approve stage 2 before stage 1.
        var attempt = await svc.ApproveEcoStageAsync(s2.Id, "qa-mgr", null, CancellationToken.None);

        Assert.False(attempt.IsSuccess);
        Assert.Contains("Earlier approval stage", attempt.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveEcoStage_AllStagesGreen_FlipsEcoToApproved()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "t",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;
        var s1 = (await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 1, "Engineering Lead", null, "e1", CancellationToken.None)).Value!;
        var s2 = (await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 2, "Quality Manager", null, "e1", CancellationToken.None)).Value!;

        // Approve stage 1 — ECO should still be InApproval.
        await svc.ApproveEcoStageAsync(s1.Id, "eng-lead", null, CancellationToken.None);
        var ecoAfter1 = await db.EngineeringChangeOrders.AsNoTracking().FirstAsync(e => e.Id == ap.Eco.Id);
        Assert.Equal(EcoStatus.InApproval, ecoAfter1.Status);

        // Approve stage 2 — should flip ECO to Approved.
        await svc.ApproveEcoStageAsync(s2.Id, "qa-mgr", null, CancellationToken.None);
        var ecoAfter2 = await db.EngineeringChangeOrders.AsNoTracking().FirstAsync(e => e.Id == ap.Eco.Id);
        Assert.Equal(EcoStatus.Approved, ecoAfter2.Status);
        Assert.NotNull(ecoAfter2.ApprovedAtUtc);
        Assert.Equal("qa-mgr", ecoAfter2.ApprovedBy);
    }

    [Fact]
    public async Task ReleaseEco_WithNewDocumentVersionId_TriggersDocumentSupersede()
    {
        await using var db = NewDb();
        var (svc, docSvc) = NewServices(db);

        // Create a Document with Rev A → Released, then a Rev B → Approved (ready to release via ECO).
        var doc = (await docSvc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        var vA = (await docSvc.AddVersionAsync(doc.Id, "A", "trent-a.pdf", "application/pdf", 100, null, "hA", null, null, null, "e1", CancellationToken.None)).Value!;
        await docSvc.ApproveVersionAsync(vA.Id, "qa", CancellationToken.None);
        await docSvc.ReleaseVersionAsync(vA.Id, "mgr", null, CancellationToken.None);

        var vB = (await docSvc.AddVersionAsync(doc.Id, "B", "trent-b.pdf", "application/pdf", 110, null, "hB", null, "ECR-2026-00001", null, "e1", CancellationToken.None)).Value!;
        await docSvc.ApproveVersionAsync(vB.Id, "qa", CancellationToken.None);
        // vB is Approved but NOT yet Released — ECO release will fire that.

        // Create + approve an ECR + ECO, add line item with NewDocumentVersionId=vB.Id.
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "Trent bracket Rev B", null,
            ChangeReason.CustomerRequest, ChangeUrgency.Expedited,
            true, true, false, false, true, false,
            null, doc.Id, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "Trent Rev B release",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;
        await svc.AddEcoLineItemAsync(ap.Eco.Id, null, doc.Id, vA.Id, vB.Id, "Rev A → Rev B", "Rev A", "Rev B", EcoLineItemDisposition.UseAsIs, null, "e1", CancellationToken.None);
        var s1 = (await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 1, "Engineering Lead", null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveEcoStageAsync(s1.Id, "eng-lead", null, CancellationToken.None);

        // Release the ECO — should atomically release vB AND supersede vA.
        var rel = await svc.ReleaseEcoAsync(ap.Eco.Id, "release-mgr", null, CancellationToken.None);

        Assert.True(rel.IsSuccess, rel.Error);
        Assert.Equal(EcoStatus.Released, rel.Value!.Status);
        var reloadedA = await db.DocumentVersions.AsNoTracking().FirstAsync(v => v.Id == vA.Id);
        var reloadedB = await db.DocumentVersions.AsNoTracking().FirstAsync(v => v.Id == vB.Id);
        Assert.Equal(DocumentStatus.Superseded, reloadedA.Status);
        Assert.Equal(DocumentStatus.Released, reloadedB.Status);
    }

    [Fact]
    public async Task ImplementEco_ThenCloseEco_LifecycleCompletes()
    {
        await using var db = NewDb();
        var (svc, _) = NewServices(db);
        var ecr = (await svc.CreateEcrAsync(1, "ECR-2026-00001", "t", null, ChangeReason.Other, ChangeUrgency.Routine,
            false, false, false, false, false, false, null, null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.SubmitEcrAsync(ecr.Id, "e1", CancellationToken.None);
        var ap = (await svc.ApproveEcrAndCreateEcoAsync(ecr.Id, "ECO-2026-00001", "t",
            EcoEffectivityType.Immediate, null, null, null, null, null, null, "qa", CancellationToken.None)).Value!;
        var s1 = (await svc.AddEcoApprovalStageAsync(ap.Eco.Id, 1, "Engineering Lead", null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveEcoStageAsync(s1.Id, "eng-lead", null, CancellationToken.None);
        await svc.ReleaseEcoAsync(ap.Eco.Id, "release-mgr", null, CancellationToken.None);

        var imp = await svc.ImplementEcoAsync(ap.Eco.Id, "ops-mgr", CancellationToken.None);
        Assert.True(imp.IsSuccess);
        // Re-read via AsNoTracking so the assertion sees a frozen snapshot,
        // not the same tracked entity that the subsequent CloseEco mutation
        // will overwrite (EF tracker shares the instance).
        var afterImplement = await db.EngineeringChangeOrders.AsNoTracking().FirstAsync(e => e.Id == ap.Eco.Id);
        Assert.Equal(EcoStatus.Implemented, afterImplement.Status);
        Assert.NotNull(afterImplement.ImplementedAtUtc);

        var cls = await svc.CloseEcoAsync(ap.Eco.Id, "qa-mgr", CancellationToken.None);
        Assert.True(cls.IsSuccess);
        var afterClose = await db.EngineeringChangeOrders.AsNoTracking().FirstAsync(e => e.Id == ap.Eco.Id);
        Assert.Equal(EcoStatus.Closed, afterClose.Status);
        Assert.NotNull(afterClose.ClosedAtUtc);
    }
}
