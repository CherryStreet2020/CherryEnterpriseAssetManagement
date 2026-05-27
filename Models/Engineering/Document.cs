// Sprint 14.2 PR-1 (2026-05-26 evening) — Document Management System (DMS).
//
// The controlled-engineering-document substrate. Sits alongside the existing
// Models/Attachment.cs (which is a flat file-blob store attached to business
// entities). A Document is a *logical artifact* with lifecycle + revision
// control; an Attachment is a *file*. Future PRs will wire DocumentVersion
// to actual blob storage via Attachment.
//
// WHAT THIS PR-1 SHIPS:
//   - Document (header) — engineering artifact with type + status + ownership
//   - DocumentVersion (per-revision satellite) — monotonic VersionNumber +
//     RevisionCode + ContentHash + ContentLocationUri + lifecycle stamps +
//     supersession chain
//   - ItemDocumentLink (M:N) — link Documents to Items by purpose
//     (BillOfDrawing / Specification / InspectionPlan / etc)
//   - 3 new enums (DocumentType / DocumentStatus / ItemDocumentLinkPurpose)
//   - IDocumentService with 8 ops (write-path-heavy — Create / AddVersion /
//     Approve / Release / Link / Unlink / GetCurrentReleased /
//     GetForItem)
//   - /Admin/DocumentProbe with 5 WRITE BUTTONS per the new xmin lock
//     corollary (Lock 16 corollary from PR #365: every probe must exercise
//     INSERT path before merge so latent IsRowVersion()-vs-bytea bugs
//     surface in dev, not prod)
//
// WHAT IT EXPLICITLY DOES NOT SHIP:
//   - Blob storage integration (Sprint 14.2 PR-2). ContentLocationUri is a
//     string placeholder until Attachment-coupling lands.
//   - Drawing-pinning into ProductionMaterialStructure (Sprint 14.3 ECR/ECO
//     scope — when a PRO is snapshotted, the current Released DocumentVersion
//     for each linked Drawing freezes into the snapshot).
//   - File upload UI (later UI sprint).
//   - DocumentVersion-to-Attachment 1:1 FK (deferred until Attachment.Source
//     enum gets a Document value).
//
// LOCKS APPLIED PROPHYLACTICALLY:
//   - HARD LOCK xmin pattern (encoded in PR #365): every concurrency-token
//     property is `byte[]? RowVersion` mapped via MapXminRowVersion. NEVER
//     IsRowVersion()+bytea (which threw 23502 on PR #364 INSERTs).
//   - HARD LOCK enum DB defaults (encoded in PR #363): every enum column
//     gets HasDefaultValue matching the model default BEFORE migration is
//     generated.
//   - HARD LOCK no-fake-data: tests use Rolls-Royce Trent bracket drawing,
//     Boeing BAMS-3320 spec, AS9102 procedure, Cert of Conformance with
//     real revision codes.
//   - BIC entity checklist: tenant trio (CompanyId NOT NULL, LocationId
//     nullable, TenantId via convention), partial UNIQUE indexes,
//     ITenantContext-compatible, chain-of-custody preserved.
//
// REFERENCES:
//   - memory: feedback_xmin_pattern_for_concurrency_lock.md
//   - memory: feedback_b6_enum_defaults_must_match_model.md
//   - memory: feedback_b6_go_big_2026_05_26.md
//   - memory: project_pr364_pr365_shipped.md (the predecessor + the lesson)
//   - memory: reference_master_plan_audit_2026_05_24.md (Wave 14.2)
//   - Models/Attachment.cs (the flat file store this sits alongside)
//   - Models/Production/ProductionMaterialStructure.cs (future consumer
//     via Sprint 14.3)

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// What kind of engineering artifact this Document is. Drives display
    /// + routing + retention rules. SAP DMS document type / Oracle Agile
    /// item type / Arena PLM category.
    /// </summary>
    public enum DocumentType
    {
        /// <summary>Engineering drawing (CAD output, ballooned PDF, technical illustration).</summary>
        Drawing = 0,

        /// <summary>Material/process specification (customer-supplied like BAMS, or internal).</summary>
        Specification = 1,

        /// <summary>Work procedure / SOP / setup sheet.</summary>
        Procedure = 2,

        /// <summary>Reference manual / handbook.</summary>
        Manual = 3,

        /// <summary>Certificate of Conformance / Compliance.</summary>
        CertOfConformance = 4,

        /// <summary>Material certification (mill cert, heat-lot cert).</summary>
        MaterialCert = 5,

        /// <summary>Inspection plan (AS9102 FAI plan, CMM program ref, gauge plan).</summary>
        InspectionPlan = 6,

        /// <summary>Test report / measurement record.</summary>
        TestReport = 7,

        /// <summary>Other — free-form catch-all (route via Title + Notes).</summary>
        Other = 99,
    }

    /// <summary>
    /// Lifecycle status of a Document or DocumentVersion. AS9100 §7.5.3
    /// controlled-document discipline. Default Draft = new artifacts always
    /// start unreleased.
    /// </summary>
    public enum DocumentStatus
    {
        /// <summary>Draft — author working copy.</summary>
        Draft = 0,

        /// <summary>In review — submitted for approval.</summary>
        InReview = 1,

        /// <summary>Approved — signed off but not yet released to manufacturing.</summary>
        Approved = 2,

        /// <summary>Released — effective + in-use. Only one Released version per Document at a time.</summary>
        Released = 3,

        /// <summary>Superseded — replaced by a newer Released version.</summary>
        Superseded = 4,

        /// <summary>Obsolete — permanently retired; no new use.</summary>
        Obsolete = 5,
    }

    /// <summary>
    /// Why a Document is linked to an Item. Drives where the link appears
    /// in the Item record + which workflows pick it up.
    /// </summary>
    public enum ItemDocumentLinkPurpose
    {
        /// <summary>Engineering drawing — the bill-of-drawing for this Item.</summary>
        BillOfDrawing = 0,

        /// <summary>Specification (BAMS-3320, customer spec, internal).</summary>
        Specification = 1,

        /// <summary>Inspection plan (AS9102 / CMM / gauge plan).</summary>
        InspectionPlan = 2,

        /// <summary>Work procedure / SOP.</summary>
        Procedure = 3,

        /// <summary>Certificate of Conformance (for shipped items).</summary>
        CertOfConformance = 4,

        /// <summary>Other — annotated via Notes.</summary>
        Other = 99,
    }

    /// <summary>
    /// Document — controlled engineering artifact with lifecycle and revisions.
    /// </summary>
    [Table("Documents")]
    public class Document
    {
        public int Id { get; set; }

        // ===== Tenant trio ================================================

        [Required]
        public int CompanyId { get; set; }

        public int? LocationId { get; set; }

        // ===== Identity ===================================================

        /// <summary>
        /// Human-facing identifier ("DWG-TRENT-BRACKET-A", "SPEC-BAMS-3320",
        /// "PROC-FAI-AS9102"). UNIQUE per (CompanyId, DocumentNumber) via
        /// partial UNIQUE index in the migration. Convention but not
        /// enforced: prefix-type-name-rev style.
        /// </summary>
        [Required, StringLength(100)]
        [Display(Name = "Document Number")]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Document Type")]
        public DocumentType DocumentType { get; set; } = DocumentType.Drawing;

        /// <summary>
        /// Header-level lifecycle status. Default Draft. Often mirrors the
        /// status of the latest version but can diverge (e.g., Document
        /// Obsoleted even while old Released versions exist).
        /// </summary>
        [Required]
        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

        /// <summary>
        /// AS9100 §7.5.3 / NADCAP controlled-document flag. When TRUE,
        /// the document requires the full Draft → InReview → Approved →
        /// Released approval chain on every revision; when FALSE, can
        /// short-circuit (uncontrolled reference material).
        /// </summary>
        [Display(Name = "Is Controlled (AS9100 §7.5.3)")]
        public bool IsControlled { get; set; } = false;

        // ===== Ownership ==================================================

        [StringLength(100)]
        [Display(Name = "Owner Name")]
        public string? OwnerName { get; set; }

        [StringLength(50)]
        [Display(Name = "Owner User Id")]
        public string? OwnerUserId { get; set; }

        // ===== Audit + concurrency =========================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        // Concurrency via Postgres xmin (project convention; see
        // Data/XminRowVersionExtensions.cs). NEVER IsRowVersion() per the
        // HARD LOCK encoded after PR #365's hotfix.
        public byte[]? RowVersion { get; set; }

        // ===== Navs ========================================================

        public ICollection<DocumentVersion>? Versions { get; set; }
        public ICollection<ItemDocumentLink>? ItemLinks { get; set; }
    }

    /// <summary>
    /// DocumentVersion — one immutable revision of a Document. Lifecycle:
    /// Draft → InReview → Approved → Released → Superseded → Obsolete.
    /// Only one Released version per Document at a time (enforced by the
    /// service-layer release flow, which also flips the prior Released
    /// version to Superseded atomically).
    /// </summary>
    [Table("DocumentVersions")]
    public class DocumentVersion
    {
        public int Id { get; set; }

        // ===== Parent + tenant denorm =====================================

        [Required]
        public int DocumentId { get; set; }
        public Document? Document { get; set; }

        [Required]
        public int CompanyId { get; set; }

        public int? LocationId { get; set; }

        // ===== Version identity ===========================================

        /// <summary>
        /// Monotonic integer per Document — 1, 2, 3... Service-layer
        /// AddVersion auto-increments via MAX(VersionNumber) + 1 within a
        /// transaction. UNIQUE per (DocumentId, VersionNumber).
        /// </summary>
        [Required]
        [Display(Name = "Version Number")]
        public int VersionNumber { get; set; }

        /// <summary>
        /// Free-form revision code ("A", "B", "Rev-01", "R3"). UNIQUE per
        /// (DocumentId, RevisionCode) via partial UNIQUE — two revisions
        /// can share a number across different documents but not within
        /// the same document.
        /// </summary>
        [Required, StringLength(16)]
        [Display(Name = "Revision Code")]
        public string RevisionCode { get; set; } = "A";

        [Required]
        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

        // ===== Content =====================================================

        /// <summary>
        /// User-facing filename (e.g. "Trent-Bracket-Rev-A.pdf").
        /// </summary>
        [Required, StringLength(255)]
        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// MIME type ("application/pdf", "image/png", "model/iges").
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Content Type")]
        public string? ContentType { get; set; }

        [Display(Name = "File Size (bytes)")]
        public long FileSizeBytes { get; set; }

        [Display(Name = "Page Count")]
        public int? PageCount { get; set; }

        /// <summary>
        /// SHA-256 (lower-case hex, 64 chars) of the binary content. Used
        /// for change detection (a re-upload with the same hash is a no-op)
        /// and tamper evidence (the snapshot in ProductionMaterialStructure
        /// can later verify the linked drawing hasn't been swapped).
        /// </summary>
        [StringLength(64)]
        [Display(Name = "Content Hash (SHA-256)")]
        public string? ContentHash { get; set; }

        /// <summary>
        /// Where the binary lives. PR-1: free-form URI (S3 URL, file://
        /// path, sharepoint:// ref, etc.). Future PR will normalize to an
        /// Attachment FK once Attachment.Source has a Document value.
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Content Location URI")]
        public string? ContentLocationUri { get; set; }

        // ===== Engineering-change linkage =================================

        /// <summary>
        /// ECN/ECO number that triggered this version (Theme B6 Arena PLM
        /// substrate). Free-form string until Sprint 14.3 introduces a
        /// formal ECR/ECO entity.
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Source ECO/ECN Number")]
        public string? SourceEcoNumber { get; set; }

        /// <summary>
        /// FK to the version this one supersedes (when set). Null on the
        /// first version. SET NULL on supersedee delete so the chain
        /// survives administrative cleanup of obsolete versions.
        /// </summary>
        [Display(Name = "Supersedes Version")]
        public int? SupersedesVersionId { get; set; }
        public DocumentVersion? SupersedesVersion { get; set; }

        // ===== Lifecycle stamps ===========================================

        [Display(Name = "Approved At")]
        public DateTime? ApprovedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Released At")]
        public DateTime? ReleasedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Released By")]
        public string? ReleasedBy { get; set; }

        [Display(Name = "Effective From")]
        public DateTime? EffectiveFromUtc { get; set; }

        [Display(Name = "Effective To")]
        public DateTime? EffectiveToUtc { get; set; }

        // ===== Audit + concurrency ========================================

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// ItemDocumentLink — many-to-many between Items and Documents with
    /// link purpose. The "bill of drawings" for an Item is the set of
    /// links with LinkPurpose=BillOfDrawing.
    /// </summary>
    [Table("ItemDocumentLinks")]
    public class ItemDocumentLink
    {
        public int Id { get; set; }

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Required]
        public int DocumentId { get; set; }
        public Document? Document { get; set; }

        // ===== Tenant trio (denorm) =======================================

        [Required]
        public int CompanyId { get; set; }

        public int? LocationId { get; set; }

        // ===== Link semantics =============================================

        [Required]
        [Display(Name = "Link Purpose")]
        public ItemDocumentLinkPurpose LinkPurpose { get; set; } = ItemDocumentLinkPurpose.BillOfDrawing;

        /// <summary>
        /// Flag the primary drawing/spec/etc for this Item-Purpose pair.
        /// E.g., an Item with multiple BillOfDrawing links can flag one
        /// as primary for default rendering on the Item card.
        /// </summary>
        [Display(Name = "Is Primary")]
        public bool IsPrimary { get; set; } = false;

        // ===== Audit ======================================================

        public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        [Display(Name = "Linked By")]
        public string? LinkedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
