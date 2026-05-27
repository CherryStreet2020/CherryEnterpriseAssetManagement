// Sprint 14.2 PR-1 (2026-05-26 evening) — DocumentServiceTests.
//
// All fixtures use REALISTIC aerospace + mfg scenarios per HARD LOCK
// feedback_no_fake_data.md:
//
//   DWG-TRENT-BRACKET-A     — Rolls-Royce Trent XWB engine bracket assembly drawing
//                             (Drawing, Controlled, Rev A → Rev B chain).
//   SPEC-BAMS-3320          — Boeing material spec (Specification, Controlled).
//   PROC-FAI-AS9102         — AS9102 First Article Inspection procedure (Procedure).
//   CERT-COC-TRENT-001      — Certificate of Conformance for Trent bracket shipset
//                             (CertOfConformance, Controlled).
//   MAT-CERT-AMS6520-HEAT   — Heat-lot material cert for AMS 6520 stainless
//                             (MaterialCert, Controlled).
//
// Coverage (15 tests):
//   1. Create happy path.
//   2. Create duplicate DocumentNumber on same tenant throws.
//   3. Create allows same DocumentNumber across different tenants.
//   4. AddVersion auto-increments VersionNumber.
//   5. AddVersion idempotent on same RevisionCode + same ContentHash.
//   6. AddVersion same RevisionCode different ContentHash throws.
//   7. AddVersion stamps source ECO number.
//   8. ApproveVersion flips Draft → Approved.
//   9. ApproveVersion fails on Released version.
//  10. ReleaseVersion flips Approved → Released and stamps timestamps.
//  11. ReleaseVersion atomically supersedes prior Released version on same Document.
//  12. LinkToItem happy path.
//  13. LinkToItem idempotent on (Item, Doc, Purpose).
//  14. GetCurrentReleasedVersion returns latest Released.
//  15. GetDocumentsForItem returns linked docs + their current Released versions.

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

public class DocumentServiceTests
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
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dms-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static DocumentService NewService(AppDbContext db) =>
        new(db, NullLogger<DocumentService>.Instance);

    // ---------- realistic-mfg fixtures ----------

    private static Item TrentBracketItem() => new()
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
    };

    // ---------- tests ----------

    [Fact]
    public async Task Create_HappyPath()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var result = await svc.CreateAsync(
            companyId: 1,
            documentNumber: "DWG-TRENT-BRACKET-A",
            title: "Rolls-Royce Trent XWB Engine Bracket Assy Drawing",
            documentType: DocumentType.Drawing,
            isControlled: true,
            description: "AS9100 controlled engineering drawing, ballooned.",
            ownerName: "Engineering Lead",
            createdBy: "engineer-1",
            ct: CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.Id > 0);
        Assert.Equal("DWG-TRENT-BRACKET-A", result.Value.DocumentNumber);
        Assert.Equal(DocumentType.Drawing, result.Value.DocumentType);
        Assert.Equal(DocumentStatus.Draft, result.Value.Status);
        Assert.True(result.Value.IsControlled);
    }

    [Fact]
    public async Task Create_DuplicateDocumentNumber_OnSameTenant_Throws()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var first = await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket assy", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None);
        Assert.True(first.IsSuccess);

        var dup = await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Duplicate attempt", DocumentType.Drawing, true, null, null, "e2", CancellationToken.None);
        Assert.False(dup.IsSuccess);
        Assert.Contains("already exists", dup.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AllowsSameDocumentNumber_AcrossDifferentTenants()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var tenant1 = await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Tenant 1 doc", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None);
        var tenant2 = await svc.CreateAsync(2, "DWG-TRENT-BRACKET-A", "Tenant 2 doc", DocumentType.Drawing, true, null, null, "e2", CancellationToken.None);

        Assert.True(tenant1.IsSuccess);
        Assert.True(tenant2.IsSuccess);
        Assert.NotEqual(tenant1.Value!.Id, tenant2.Value!.Id);
    }

    [Fact]
    public async Task AddVersion_AutoIncrementsVersionNumber()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;

        var v1 = await svc.AddVersionAsync(doc.Id, "A", "trent-bracket-rev-a.pdf", "application/pdf", 102_400, null, "hash1", "s3://bucket/trent-a.pdf", "ECO-2026-100", null, "e1", CancellationToken.None);
        var v2 = await svc.AddVersionAsync(doc.Id, "B", "trent-bracket-rev-b.pdf", "application/pdf", 102_500, null, "hash2", "s3://bucket/trent-b.pdf", "ECO-2026-118", null, "e1", CancellationToken.None);
        var v3 = await svc.AddVersionAsync(doc.Id, "C", "trent-bracket-rev-c.pdf", "application/pdf", 102_600, null, "hash3", "s3://bucket/trent-c.pdf", "ECO-2026-125", null, "e1", CancellationToken.None);

        Assert.Equal(1, v1.Value!.VersionNumber);
        Assert.Equal(2, v2.Value!.VersionNumber);
        Assert.Equal(3, v3.Value!.VersionNumber);
    }

    [Fact]
    public async Task AddVersion_Idempotent_OnSameRevisionAndHash()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;

        var first = await svc.AddVersionAsync(doc.Id, "A", "trent-bracket-rev-a.pdf", "application/pdf", 102_400, null, "samehash", "s3://bucket/trent-a.pdf", null, null, "e1", CancellationToken.None);
        var dup   = await svc.AddVersionAsync(doc.Id, "A", "trent-bracket-rev-a-rerun.pdf", "application/pdf", 102_400, null, "samehash", "s3://bucket/trent-a-rerun.pdf", null, null, "e1", CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(dup.IsSuccess);
        Assert.Equal(first.Value!.Id, dup.Value!.Id);  // same row, no duplicate written
        var count = await db.DocumentVersions.CountAsync(v => v.DocumentId == doc.Id && v.RevisionCode == "A");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddVersion_SameRevision_DifferentHash_Throws()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        await svc.AddVersionAsync(doc.Id, "A", "rev-a-original.pdf", "application/pdf", 100, null, "hashA", "s3://bucket/a.pdf", null, null, "e1", CancellationToken.None);

        var conflict = await svc.AddVersionAsync(doc.Id, "A", "rev-a-CHANGED.pdf", "application/pdf", 200, null, "hashB-different", "s3://bucket/a-changed.pdf", null, null, "e1", CancellationToken.None);

        Assert.False(conflict.IsSuccess);
        Assert.Contains("DIFFERENT ContentHash", conflict.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddVersion_StampsSourceEcoNumber()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        var v = await svc.AddVersionAsync(doc.Id, "B", "trent-rev-b.pdf", "application/pdf", 102_500, null, "hashB", "s3://b.pdf", "ECO-2026-118", "Boeing-mandated dim change", "e1", CancellationToken.None);

        Assert.True(v.IsSuccess);
        Assert.Equal("ECO-2026-118", v.Value!.SourceEcoNumber);
        Assert.Equal("Boeing-mandated dim change", v.Value.Notes);
    }

    [Fact]
    public async Task ApproveVersion_Flips_DraftToApproved()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        var v = (await svc.AddVersionAsync(doc.Id, "A", "trent-a.pdf", "application/pdf", 100, null, "h", null, null, null, "e1", CancellationToken.None)).Value!;

        var approved = await svc.ApproveVersionAsync(v.Id, "qa-lead", CancellationToken.None);

        Assert.True(approved.IsSuccess);
        Assert.Equal(DocumentStatus.Approved, approved.Value!.Status);
        Assert.NotNull(approved.Value.ApprovedAtUtc);
        Assert.Equal("qa-lead", approved.Value.ApprovedBy);
    }

    [Fact]
    public async Task ApproveVersion_FailsOnReleasedVersion()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        var v = (await svc.AddVersionAsync(doc.Id, "A", "trent-a.pdf", "application/pdf", 100, null, "h", null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(v.Id, "qa-lead", CancellationToken.None);
        await svc.ReleaseVersionAsync(v.Id, "eng-mgr", null, CancellationToken.None);

        var attempt = await svc.ApproveVersionAsync(v.Id, "qa-lead", CancellationToken.None);

        Assert.False(attempt.IsSuccess);
        Assert.Contains("Released", attempt.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReleaseVersion_Flips_ApprovedToReleased_StampsTimestamps()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        var v = (await svc.AddVersionAsync(doc.Id, "A", "trent-a.pdf", "application/pdf", 100, null, "h", null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(v.Id, "qa-lead", CancellationToken.None);

        var released = await svc.ReleaseVersionAsync(v.Id, "eng-mgr", null, CancellationToken.None);

        Assert.True(released.IsSuccess);
        Assert.Equal(DocumentStatus.Released, released.Value!.Status);
        Assert.NotNull(released.Value.ReleasedAtUtc);
        Assert.NotNull(released.Value.EffectiveFromUtc);
        Assert.Equal("eng-mgr", released.Value.ReleasedBy);
    }

    [Fact]
    public async Task ReleaseVersion_AtomicallySupersedes_PriorReleased()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;

        // Release Rev A.
        var vA = (await svc.AddVersionAsync(doc.Id, "A", "trent-a.pdf", "application/pdf", 100, null, "hA", null, "ECO-2026-100", null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(vA.Id, "qa", CancellationToken.None);
        await svc.ReleaseVersionAsync(vA.Id, "mgr", null, CancellationToken.None);

        // Release Rev B — should supersede A.
        var vB = (await svc.AddVersionAsync(doc.Id, "B", "trent-b.pdf", "application/pdf", 110, null, "hB", null, "ECO-2026-118", null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(vB.Id, "qa", CancellationToken.None);
        var releasedB = await svc.ReleaseVersionAsync(vB.Id, "mgr", null, CancellationToken.None);

        Assert.True(releasedB.IsSuccess);
        Assert.Equal(DocumentStatus.Released, releasedB.Value!.Status);
        Assert.Equal(vA.Id, releasedB.Value.SupersedesVersionId);

        var reloadedA = await db.DocumentVersions.FirstAsync(v => v.Id == vA.Id);
        Assert.Equal(DocumentStatus.Superseded, reloadedA.Status);
        Assert.NotNull(reloadedA.EffectiveToUtc);
    }

    [Fact]
    public async Task LinkToItem_HappyPath()
    {
        await using var db = NewDb();
        db.Items.Add(TrentBracketItem());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;

        var link = await svc.LinkToItemAsync(9501, doc.Id, ItemDocumentLinkPurpose.BillOfDrawing, true, "Primary engineering drawing", "engineer-1", CancellationToken.None);

        Assert.True(link.IsSuccess, link.Error);
        Assert.Equal(9501, link.Value!.ItemId);
        Assert.Equal(doc.Id, link.Value.DocumentId);
        Assert.Equal(ItemDocumentLinkPurpose.BillOfDrawing, link.Value.LinkPurpose);
        Assert.True(link.Value.IsPrimary);
    }

    [Fact]
    public async Task LinkToItem_IsIdempotent()
    {
        await using var db = NewDb();
        db.Items.Add(TrentBracketItem());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;

        var first = await svc.LinkToItemAsync(9501, doc.Id, ItemDocumentLinkPurpose.BillOfDrawing, true, null, "e1", CancellationToken.None);
        var dup   = await svc.LinkToItemAsync(9501, doc.Id, ItemDocumentLinkPurpose.BillOfDrawing, true, null, "e2", CancellationToken.None);

        Assert.Equal(first.Value!.Id, dup.Value!.Id);
        var count = await db.ItemDocumentLinks.CountAsync(l => l.ItemId == 9501 && l.DocumentId == doc.Id && l.LinkPurpose == ItemDocumentLinkPurpose.BillOfDrawing);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetCurrentReleasedVersion_ReturnsLatestReleased()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var doc = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        var vA = (await svc.AddVersionAsync(doc.Id, "A", "a.pdf", "application/pdf", 100, null, "hA", null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(vA.Id, "qa", CancellationToken.None);
        await svc.ReleaseVersionAsync(vA.Id, "mgr", null, CancellationToken.None);
        var vB = (await svc.AddVersionAsync(doc.Id, "B", "b.pdf", "application/pdf", 110, null, "hB", null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(vB.Id, "qa", CancellationToken.None);
        await svc.ReleaseVersionAsync(vB.Id, "mgr", null, CancellationToken.None);

        var current = await svc.GetCurrentReleasedVersionAsync(doc.Id, CancellationToken.None);

        Assert.NotNull(current);
        Assert.Equal(vB.Id, current!.Id);
        Assert.Equal("B", current.RevisionCode);
    }

    [Fact]
    public async Task GetDocumentsForItem_ReturnsLinkedDocsWithCurrentReleased()
    {
        await using var db = NewDb();
        db.Items.Add(TrentBracketItem());
        await db.SaveChangesAsync();

        var svc = NewService(db);

        // Drawing — released at Rev A.
        var dwg = (await svc.CreateAsync(1, "DWG-TRENT-BRACKET-A", "Trent bracket drawing", DocumentType.Drawing, true, null, null, "e1", CancellationToken.None)).Value!;
        var dwgA = (await svc.AddVersionAsync(dwg.Id, "A", "trent-a.pdf", "application/pdf", 100, null, "hA", null, "ECO-2026-100", null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(dwgA.Id, "qa", CancellationToken.None);
        await svc.ReleaseVersionAsync(dwgA.Id, "mgr", null, CancellationToken.None);

        // Spec — Draft only (no current released).
        var spec = (await svc.CreateAsync(1, "SPEC-BAMS-3320", "Boeing material spec", DocumentType.Specification, true, "Aluminum sheet processing", "Boeing", "e1", CancellationToken.None)).Value!;

        // Procedure — released at A.
        var proc = (await svc.CreateAsync(1, "PROC-FAI-AS9102", "AS9102 FAI procedure", DocumentType.Procedure, true, null, "QA Lead", "e1", CancellationToken.None)).Value!;
        var procA = (await svc.AddVersionAsync(proc.Id, "A", "as9102-fai-proc.pdf", "application/pdf", 50_000, null, "hP", null, null, null, "e1", CancellationToken.None)).Value!;
        await svc.ApproveVersionAsync(procA.Id, "qa", CancellationToken.None);
        await svc.ReleaseVersionAsync(procA.Id, "mgr", null, CancellationToken.None);

        // Link all 3 to the Trent bracket Item.
        await svc.LinkToItemAsync(9501, dwg.Id, ItemDocumentLinkPurpose.BillOfDrawing, true, null, "e1", CancellationToken.None);
        await svc.LinkToItemAsync(9501, spec.Id, ItemDocumentLinkPurpose.Specification, false, null, "e1", CancellationToken.None);
        await svc.LinkToItemAsync(9501, proc.Id, ItemDocumentLinkPurpose.Procedure, false, null, "e1", CancellationToken.None);

        var summaries = await svc.GetDocumentsForItemAsync(9501, CancellationToken.None);

        Assert.Equal(3, summaries.Count);
        var dwgSummary = summaries.Single(s => s.DocumentNumber == "DWG-TRENT-BRACKET-A");
        Assert.Equal(ItemDocumentLinkPurpose.BillOfDrawing, dwgSummary.LinkPurpose);
        Assert.True(dwgSummary.IsPrimary);
        Assert.Equal("A", dwgSummary.CurrentReleasedRevisionCode);

        var specSummary = summaries.Single(s => s.DocumentNumber == "SPEC-BAMS-3320");
        Assert.Null(specSummary.CurrentReleasedVersionId);
        Assert.Null(specSummary.CurrentReleasedRevisionCode);

        var procSummary = summaries.Single(s => s.DocumentNumber == "PROC-FAI-AS9102");
        Assert.Equal("A", procSummary.CurrentReleasedRevisionCode);
    }
}
