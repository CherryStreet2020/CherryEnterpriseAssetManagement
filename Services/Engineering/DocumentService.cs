// Sprint 14.2 PR-1 (2026-05-26 evening) — DocumentService impl.
//
// DMS substrate. All writes are atomic per the PR-FS-6 single-SaveChangesAsync
// lesson. xmin concurrency via MapXminRowVersion at the AppDbContext level
// (HARD LOCK from PR #365 — no IsRowVersion()/bytea).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering;

public sealed class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(AppDbContext db, ILogger<DocumentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<Document>> CreateAsync(
        int companyId,
        string documentNumber,
        string title,
        DocumentType documentType,
        bool isControlled,
        string? description,
        string? ownerName,
        string createdBy,
        CancellationToken ct)
    {
        if (companyId <= 0) return Result.Failure<Document>("CompanyId must be > 0.");
        if (string.IsNullOrWhiteSpace(documentNumber)) return Result.Failure<Document>("DocumentNumber is required.");
        if (string.IsNullOrWhiteSpace(title)) return Result.Failure<Document>("Title is required.");
        if (string.IsNullOrWhiteSpace(createdBy)) return Result.Failure<Document>("CreatedBy is required.");

        // Codex P1 fix (PR #366): normalize DocumentNumber BEFORE the uniqueness
        // lookup AND before assigning to the entity. AppDbContext uppercases
        // string fields on SaveChanges (the global normalizer), so a raw
        // "dwg-foo" lookup misses the existing "DWG-FOO" row and only fails
        // at the DB unique constraint as DbUpdateException — surfacing an
        // exception instead of a controlled Result.Failure.
        var normalizedDocNumber = documentNumber.Trim().ToUpperInvariant();

        // Service-side uniqueness check (DB partial UNIQUE complements).
        var existing = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.CompanyId == companyId && d.DocumentNumber == normalizedDocNumber, ct);
        if (existing != null)
        {
            return Result.Failure<Document>(
                $"Document {normalizedDocNumber} already exists for CompanyId {companyId} (Id={existing.Id}). " +
                "Pick a different DocumentNumber.");
        }

        var now = DateTime.UtcNow;
        var doc = new Document
        {
            CompanyId = companyId,
            DocumentNumber = normalizedDocNumber,
            Title = title,
            Description = description,
            DocumentType = documentType,
            Status = DocumentStatus.Draft,
            IsControlled = isControlled,
            OwnerName = ownerName,
            CreatedAt = now,
            CreatedBy = createdBy,
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentService.CreateAsync: created Document {DocId} '{DocNumber}' Type={Type} Controlled={Controlled} by {By}.",
            doc.Id, doc.DocumentNumber, doc.DocumentType, doc.IsControlled, createdBy);

        return Result.Success(doc);
    }

    public async Task<Result<DocumentVersion>> AddVersionAsync(
        int documentId,
        string revisionCode,
        string fileName,
        string? contentType,
        long fileSizeBytes,
        int? pageCount,
        string? contentHash,
        string? contentLocationUri,
        string? sourceEcoNumber,
        string? notes,
        string createdBy,
        CancellationToken ct)
    {
        if (documentId <= 0) return Result.Failure<DocumentVersion>("DocumentId must be > 0.");
        if (string.IsNullOrWhiteSpace(revisionCode)) return Result.Failure<DocumentVersion>("RevisionCode is required.");
        if (string.IsNullOrWhiteSpace(fileName)) return Result.Failure<DocumentVersion>("FileName is required.");
        if (fileSizeBytes < 0) return Result.Failure<DocumentVersion>("FileSizeBytes must be >= 0.");
        if (string.IsNullOrWhiteSpace(createdBy)) return Result.Failure<DocumentVersion>("CreatedBy is required.");

        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc == null) return Result.Failure<DocumentVersion>($"Document {documentId} not found.");

        // Codex P1 fix (PR #366): normalize RevisionCode BEFORE the
        // idempotency check AND before assigning to the entity. Same
        // reason as CreateAsync — AppDbContext uppercases on SaveChanges,
        // so a raw "a" misses the existing "A" row and hits the
        // UX_DocVersions_Doc_RevisionCode constraint as DbUpdateException
        // instead of surfacing a controlled Result.Failure.
        var normalizedRev = revisionCode.Trim().ToUpperInvariant();

        // Idempotency: same RevisionCode + same ContentHash on this Document → return existing.
        var existing = await _db.DocumentVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.DocumentId == documentId && v.RevisionCode == normalizedRev, ct);
        if (existing != null)
        {
            if (existing.ContentHash == contentHash)
            {
                _logger.LogInformation(
                    "DocumentService.AddVersionAsync: Document {DocId} RevisionCode '{Rev}' with identical ContentHash exists (VersionId={VId}); returning existing.",
                    documentId, normalizedRev, existing.Id);
                return Result.Success(existing);
            }

            return Result.Failure<DocumentVersion>(
                $"DocumentVersion for Document {documentId} RevisionCode '{normalizedRev}' already exists with a DIFFERENT ContentHash. " +
                "Content has changed — pick a new RevisionCode (bump the revision) instead of re-using.");
        }

        var nextVersionNumber = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        nextVersionNumber += 1;

        var now = DateTime.UtcNow;
        var version = new DocumentVersion
        {
            DocumentId = documentId,
            CompanyId = doc.CompanyId,
            LocationId = doc.LocationId,
            VersionNumber = nextVersionNumber,
            RevisionCode = normalizedRev,
            Status = DocumentStatus.Draft,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            PageCount = pageCount,
            ContentHash = contentHash,
            ContentLocationUri = contentLocationUri,
            SourceEcoNumber = sourceEcoNumber,
            Notes = notes,
            CreatedAt = now,
            CreatedBy = createdBy,
        };
        _db.DocumentVersions.Add(version);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentService.AddVersionAsync: Document {DocId} new VersionNumber={Ver} RevisionCode='{Rev}' ContentHash='{Hash}' ECO='{Eco}' by {By}.",
            documentId, nextVersionNumber, revisionCode, contentHash ?? "<null>", sourceEcoNumber ?? "<null>", createdBy);

        return Result.Success(version);
    }

    public async Task<Result<DocumentVersion>> ApproveVersionAsync(
        int versionId,
        string approvedBy,
        CancellationToken ct)
    {
        if (versionId <= 0) return Result.Failure<DocumentVersion>("VersionId must be > 0.");
        if (string.IsNullOrWhiteSpace(approvedBy)) return Result.Failure<DocumentVersion>("ApprovedBy is required.");

        var version = await _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version == null) return Result.Failure<DocumentVersion>($"DocumentVersion {versionId} not found.");
        if (version.Status != DocumentStatus.Draft && version.Status != DocumentStatus.InReview)
        {
            return Result.Failure<DocumentVersion>(
                $"DocumentVersion {versionId} is in status {version.Status}; only Draft or InReview can be approved.");
        }

        version.Status = DocumentStatus.Approved;
        version.ApprovedAtUtc = DateTime.UtcNow;
        version.ApprovedBy = approvedBy;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentService.ApproveVersionAsync: DocVersion {VId} approved by {By}.",
            versionId, approvedBy);

        return Result.Success(version);
    }

    public async Task<Result<DocumentVersion>> ReleaseVersionAsync(
        int versionId,
        string releasedBy,
        DateTime? effectiveFromUtc,
        CancellationToken ct)
    {
        if (versionId <= 0) return Result.Failure<DocumentVersion>("VersionId must be > 0.");
        if (string.IsNullOrWhiteSpace(releasedBy)) return Result.Failure<DocumentVersion>("ReleasedBy is required.");

        var version = await _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version == null) return Result.Failure<DocumentVersion>($"DocumentVersion {versionId} not found.");
        if (version.Status != DocumentStatus.Approved)
        {
            return Result.Failure<DocumentVersion>(
                $"DocumentVersion {versionId} is in status {version.Status}; only Approved versions can be released.");
        }

        var now = DateTime.UtcNow;
        var effective = effectiveFromUtc ?? now;

        // Atomic supersede: find any prior Released version on the same
        // Document and flip it to Superseded, plus stamp the new version's
        // SupersedesVersionId. Single SaveChangesAsync per PR-FS-6 lesson.
        var priorReleased = await _db.DocumentVersions
            .Where(v => v.DocumentId == version.DocumentId
                     && v.Id != version.Id
                     && v.Status == DocumentStatus.Released)
            .ToListAsync(ct);

        foreach (var prior in priorReleased)
        {
            prior.Status = DocumentStatus.Superseded;
            prior.EffectiveToUtc = effective;
        }

        version.Status = DocumentStatus.Released;
        version.ReleasedAtUtc = now;
        version.ReleasedBy = releasedBy;
        version.EffectiveFromUtc = effective;
        if (priorReleased.Count > 0)
        {
            // Point new version at the most-recent prior Released as
            // the chain head. (Multiple prior Releaseds is anomalous but
            // safe — they all get Superseded; the chain link goes to the
            // newest of them by VersionNumber.)
            version.SupersedesVersionId = priorReleased.OrderByDescending(p => p.VersionNumber).First().Id;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentService.ReleaseVersionAsync: DocVersion {VId} (Doc {DocId}) released by {By}. {SupersededCount} prior Released version(s) flipped to Superseded.",
            versionId, version.DocumentId, releasedBy, priorReleased.Count);

        return Result.Success(version);
    }

    public Task<DocumentVersion?> GetCurrentReleasedVersionAsync(int documentId, CancellationToken ct)
    {
        return _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == documentId && v.Status == DocumentStatus.Released)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result<ItemDocumentLink>> LinkToItemAsync(
        int itemId,
        int documentId,
        ItemDocumentLinkPurpose linkPurpose,
        bool isPrimary,
        string? notes,
        string linkedBy,
        CancellationToken ct)
    {
        if (itemId <= 0) return Result.Failure<ItemDocumentLink>("ItemId must be > 0.");
        if (documentId <= 0) return Result.Failure<ItemDocumentLink>("DocumentId must be > 0.");
        if (string.IsNullOrWhiteSpace(linkedBy)) return Result.Failure<ItemDocumentLink>("LinkedBy is required.");

        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc == null) return Result.Failure<ItemDocumentLink>($"Document {documentId} not found.");

        // Codex P1 fix (PR #366): verify Item.CompanyId matches Document.CompanyId
        // before linking. Without this, an Item from Tenant A can be linked to
        // a Document from Tenant B, and GetDocumentsForItemAsync would surface
        // cross-tenant data on the Item card — a tenant-leak P0 in disguise.
        // ItemDocumentLink.CompanyId is denormalized from the Document, so a
        // mismatched link silently corrupts the tenant-scope index.
        var item = await _db.Items.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.Id, i.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (item == null) return Result.Failure<ItemDocumentLink>($"Item {itemId} not found.");
        // Item.CompanyId is nullable in the legacy model — when set, it MUST
        // match the document's CompanyId. When null (legacy unscoped items),
        // we allow the link and stamp from the document (which IS scoped).
        if (item.CompanyId.HasValue && item.CompanyId.Value != doc.CompanyId)
        {
            return Result.Failure<ItemDocumentLink>(
                $"Cross-tenant link refused: Item {itemId} belongs to CompanyId {item.CompanyId.Value} " +
                $"but Document {documentId} belongs to CompanyId {doc.CompanyId}. " +
                "Both must belong to the same tenant.");
        }

        // Idempotency: existing link with the same (Item, Doc, Purpose) → return it.
        var existing = await _db.ItemDocumentLinks
            .FirstOrDefaultAsync(l => l.ItemId == itemId && l.DocumentId == documentId && l.LinkPurpose == linkPurpose, ct);
        if (existing != null)
        {
            _logger.LogInformation(
                "DocumentService.LinkToItemAsync: existing link Item={ItemId} Doc={DocId} Purpose={Purpose} (LinkId={LinkId}); returning existing.",
                itemId, documentId, linkPurpose, existing.Id);
            return Result.Success(existing);
        }

        var link = new ItemDocumentLink
        {
            ItemId = itemId,
            DocumentId = documentId,
            CompanyId = doc.CompanyId,
            LocationId = doc.LocationId,
            LinkPurpose = linkPurpose,
            IsPrimary = isPrimary,
            LinkedAt = DateTime.UtcNow,
            LinkedBy = linkedBy,
            Notes = notes,
        };
        _db.ItemDocumentLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentService.LinkToItemAsync: linked Item={ItemId} ↔ Doc={DocId} Purpose={Purpose} Primary={Primary} by {By}.",
            itemId, documentId, linkPurpose, isPrimary, linkedBy);

        return Result.Success(link);
    }

    public async Task<Result<int>> UnlinkAsync(int linkId, string unlinkBy, CancellationToken ct)
    {
        if (linkId <= 0) return Result.Failure<int>("LinkId must be > 0.");
        if (string.IsNullOrWhiteSpace(unlinkBy)) return Result.Failure<int>("UnlinkBy is required.");

        var link = await _db.ItemDocumentLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);
        if (link == null) return Result.Failure<int>($"ItemDocumentLink {linkId} not found.");

        _db.ItemDocumentLinks.Remove(link);
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning(
            "DocumentService.UnlinkAsync: HARD DELETED ItemDocumentLink {LinkId} (Item={ItemId} Doc={DocId} Purpose={Purpose}) by {By}.",
            linkId, link.ItemId, link.DocumentId, link.LinkPurpose, unlinkBy);

        return Result.Success(linkId);
    }

    public async Task<IReadOnlyList<ItemDocumentSummary>> GetDocumentsForItemAsync(int itemId, CancellationToken ct)
    {
        // Pull links + docs + each doc's current Released version in one
        // round-trip-light shape. EF can't translate the FirstOrDefault
        // within a Select for complex types cleanly across all providers,
        // so do it in two passes: first the links + doc, then the
        // current Released version per docId.

        var rows = await (
            from link in _db.ItemDocumentLinks.AsNoTracking()
            join doc in _db.Documents.AsNoTracking() on link.DocumentId equals doc.Id
            where link.ItemId == itemId
            select new
            {
                link.Id,
                link.DocumentId,
                doc.DocumentNumber,
                doc.Title,
                doc.DocumentType,
                DocStatus = doc.Status,
                doc.IsControlled,
                link.LinkPurpose,
                link.IsPrimary,
            }
        ).ToListAsync(ct);

        var docIds = rows.Select(r => r.DocumentId).Distinct().ToList();
        var releasedByDoc = await _db.DocumentVersions.AsNoTracking()
            .Where(v => docIds.Contains(v.DocumentId) && v.Status == DocumentStatus.Released)
            .GroupBy(v => v.DocumentId)
            .Select(g => g.OrderByDescending(v => v.VersionNumber).First())
            .ToDictionaryAsync(v => v.DocumentId, v => v, ct);

        return rows.Select(r =>
        {
            releasedByDoc.TryGetValue(r.DocumentId, out var rel);
            return new ItemDocumentSummary(
                r.Id,
                r.DocumentId,
                r.DocumentNumber,
                r.Title,
                r.DocumentType,
                r.DocStatus,
                r.IsControlled,
                r.LinkPurpose,
                r.IsPrimary,
                rel?.Id,
                rel?.RevisionCode,
                rel?.ReleasedAtUtc);
        }).ToList();
    }
}
