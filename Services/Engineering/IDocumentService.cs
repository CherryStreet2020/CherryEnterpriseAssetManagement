// Sprint 14.2 PR-1 (2026-05-26 evening) — IDocumentService.
//
// Per-Lock 15 — service surface only, no direct DbContext from PageModels.
// Per the new HARD LOCK from PR #365 — the admin probe MUST exercise these
// write paths (Create / AddVersion / Approve / Release / Link) before merge
// so any latent xmin/bytea config issues surface in dev, not prod.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering;

/// <summary>
/// CRUD + lifecycle for the DMS substrate (Document + DocumentVersion +
/// ItemDocumentLink). All write operations return <see cref="Result{T}"/>
/// for structured success/failure handling.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Create a new Document with status Draft. Validates uniqueness of
    /// (CompanyId, DocumentNumber). Stamps CreatedAt + CreatedBy.
    /// </summary>
    Task<Result<Document>> CreateAsync(
        int companyId,
        string documentNumber,
        string title,
        DocumentType documentType,
        bool isControlled,
        string? description,
        string? ownerName,
        string createdBy,
        CancellationToken ct);

    /// <summary>
    /// Add a new DocumentVersion to an existing Document. Auto-increments
    /// VersionNumber as MAX(existing) + 1 within the same Document.
    ///
    /// Idempotency: if a version with the same (DocumentId, RevisionCode)
    /// already exists AND the ContentHash matches, returns the existing
    /// row without writing. If RevisionCode matches but ContentHash
    /// differs, throws — the caller must pick a different revision code
    /// (content has changed, so it's a new revision).
    /// </summary>
    Task<Result<DocumentVersion>> AddVersionAsync(
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
        CancellationToken ct);

    /// <summary>
    /// Flip a Draft or InReview DocumentVersion to Approved. Stamps
    /// ApprovedAtUtc + ApprovedBy. Returns Failure if version not in
    /// Draft/InReview or already past Approved.
    /// </summary>
    Task<Result<DocumentVersion>> ApproveVersionAsync(
        int versionId,
        string approvedBy,
        CancellationToken ct);

    /// <summary>
    /// Release an Approved version. Stamps ReleasedAtUtc + ReleasedBy +
    /// EffectiveFromUtc. Atomically supersedes any prior Released
    /// version on the same Document (flips to Superseded + stamps
    /// EffectiveToUtc + SupersedesVersionId on the new version).
    /// Single SaveChangesAsync per the PR-FS-6 atomic-supersede lesson.
    /// </summary>
    Task<Result<DocumentVersion>> ReleaseVersionAsync(
        int versionId,
        string releasedBy,
        DateTime? effectiveFromUtc,
        CancellationToken ct);

    /// <summary>
    /// Get the current Released version of a Document. Returns null if no
    /// version has been Released yet.
    /// </summary>
    Task<DocumentVersion?> GetCurrentReleasedVersionAsync(
        int documentId,
        CancellationToken ct);

    /// <summary>
    /// Link an Item to a Document with a specific purpose (BillOfDrawing
    /// by default). Idempotent: if a link already exists for the same
    /// (ItemId, DocumentId, LinkPurpose), returns the existing row.
    /// </summary>
    Task<Result<ItemDocumentLink>> LinkToItemAsync(
        int itemId,
        int documentId,
        ItemDocumentLinkPurpose linkPurpose,
        bool isPrimary,
        string? notes,
        string linkedBy,
        CancellationToken ct);

    /// <summary>
    /// Hard-delete an ItemDocumentLink. Admin recovery only — links are
    /// otherwise immutable once created (a doc can be Superseded, but
    /// the link to the Item persists).
    /// </summary>
    Task<Result<int>> UnlinkAsync(
        int linkId,
        string unlinkBy,
        CancellationToken ct);

    /// <summary>
    /// All Documents linked to a given Item, with their current Released
    /// version (if any). Used by the Item card to render the bill of
    /// drawings + specs + procedures.
    /// </summary>
    Task<IReadOnlyList<ItemDocumentSummary>> GetDocumentsForItemAsync(
        int itemId,
        CancellationToken ct);
}

/// <summary>
/// Read-only projection of an Item's Document links + current Released
/// version. Returned by GetDocumentsForItemAsync.
/// </summary>
public sealed record ItemDocumentSummary(
    int LinkId,
    int DocumentId,
    string DocumentNumber,
    string Title,
    DocumentType DocumentType,
    DocumentStatus DocumentStatus,
    bool IsControlled,
    ItemDocumentLinkPurpose LinkPurpose,
    bool IsPrimary,
    int? CurrentReleasedVersionId,
    string? CurrentReleasedRevisionCode,
    DateTime? CurrentReleasedAtUtc);
