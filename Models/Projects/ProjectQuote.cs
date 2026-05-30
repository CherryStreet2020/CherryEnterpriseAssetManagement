// Theme B9 Wave 2 PR-4 (2026-05-30) — Quote-to-cash spine: the quote layer.
//
// The customer project lifecycle STARTS at the quote, not at job release (spec §2).
// This file lands the four entities that model "what did we quote, and in which
// version did we quote it":
//
//   ProjectRfq            — the formal request-for-quote received from the customer.
//   ProjectQuote          — a quote against the project/RFQ. MULTIPLE per project
//                           (spec §4: "multiple quotes — treat quotes as child records").
//   ProjectQuoteRevision  — a customer-visible revision (Rev A/B/C). The versioned
//                           record. On SUBMIT it freezes a LOCKED SNAPSHOT (price,
//                           margin, scope, lead time) that can never be overwritten —
//                           a new revision must be minted instead (spec §"Quote versions").
//   ProjectQuoteLine      — line items on a revision.
//
// Architecture (matches the shipped CustomerProject/ProjectAmendment conventions):
//   - Tenant trio on the TOP-LEVEL entities (ProjectRfq, ProjectQuote). Revisions and
//     lines carry NO CompanyId and are tenant-scoped THROUGH their parent ProjectQuote
//     (the RoutingOperation→Routing precedent), so child writes never need company
//     snapshot-stamping.
//   - New TABLES ⇒ default initializers are safe (no backfill). xmin concurrency via
//     MapXminRowVersion (NEVER [Timestamp]/IsRowVersion()+bytea — repo hard-lock).
//   - Enum DB defaults are wired in AppDbContext to match these model defaults.
//
// Winning-revision → contract baseline lands in PR-6 (ProjectContract). PR-5 adds the
// frozen ProjectEstimateSnapshot that SourceEstimateSnapshotId will FK to.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // ================================================================
    // ProjectRfq — the customer's formal request for quote.
    // ================================================================
    [Table("ProjectRfqs")]
    public class ProjectRfq
    {
        public int Id { get; set; }

        // Tenant trio.
        public int? TenantId { get; set; }
        [Required] public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteIdSnapshot { get; set; }

        // Parent project. CASCADE — an RFQ is intrinsic to its project (projects
        // are soft-deleted via Status, never hard-deleted in practice).
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Per-company human-readable RFQ number. UNIQUE (CompanyId, RfqNumber).
        [Required, StringLength(64)]
        public string RfqNumber { get; set; } = string.Empty;

        // The customer's own RFQ reference ("Weir RFQ-2026-0142").
        [StringLength(100)]
        public string? CustomerRfqReference { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        public DateTime? ReceivedDate { get; set; }
        public DateTime? DueDate { get; set; }

        public ProjectRfqStatus Status { get; set; } = ProjectRfqStatus.Open;

        // Role snapshots (names, not FKs) so the historical record survives
        // user-renames/deletes — same convention as ProjectAmendment.ApprovedByName.
        [StringLength(100)] public string? OwnerName { get; set; }
        [StringLength(100)] public string? EstimatorName { get; set; }
        [StringLength(100)] public string? SalespersonName { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }

        public ICollection<ProjectQuote>? Quotes { get; set; }
    }

    // ================================================================
    // ProjectQuote — a quote (parent of revisions). Multiple per project.
    // ================================================================
    [Table("ProjectQuotes")]
    public class ProjectQuote
    {
        public int Id { get; set; }

        // Tenant trio.
        public int? TenantId { get; set; }
        [Required] public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteIdSnapshot { get; set; }

        // Parent project. CASCADE (intrinsic to project).
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Optional originating RFQ. SET NULL — a quote outlives a deleted RFQ.
        public int? ProjectRfqId { get; set; }
        public ProjectRfq? Rfq { get; set; }

        // Per-company human-readable quote number. UNIQUE (CompanyId, QuoteNumber).
        [Required, StringLength(64)]
        public string QuoteNumber { get; set; } = string.Empty;

        public ProjectQuoteType QuoteType { get; set; } = ProjectQuoteType.Budgetary;

        // A named scenario ("Baseline", "Expedited", "Value-engineered") — lets a
        // project carry parallel competing quotes (spec §4 multiple-quotes design).
        [StringLength(200)] public string? Scenario { get; set; }

        [StringLength(2000)] public string? Description { get; set; }

        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        [StringLength(100)] public string? OwnerName { get; set; }
        [StringLength(100)] public string? EstimatorName { get; set; }
        [StringLength(100)] public string? SalespersonName { get; set; }

        public ProjectQuoteStatus Status { get; set; } = ProjectQuoteStatus.Draft;

        // The revision that became the contract baseline (PR-6 award). A SOFT
        // reference (no DB FK) — the awarded revision lives inside this quote's own
        // cascade aggregate, so a hard FK back to it would create a delete cycle.
        // PR-6 sets/reads it; integrity is enforced in the service layer.
        public int? AwardedRevisionId { get; set; }

        // Win probability percentage (0..100).
        [Column(TypeName = "decimal(5,2)")] public decimal? Probability { get; set; }

        [StringLength(500)] public string? LostReason { get; set; }
        [StringLength(500)] public string? NoBidReason { get; set; }
        [StringLength(200)] public string? Competitor { get; set; }
        public string? CustomerFeedback { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }

        public ICollection<ProjectQuoteRevision>? Revisions { get; set; }
    }

    // ================================================================
    // ProjectQuoteRevision — Rev A/B/C. The versioned, snapshot-locked record.
    // Scoped through ProjectQuote (no own CompanyId — RoutingOperation precedent).
    // ================================================================
    [Table("ProjectQuoteRevisions")]
    public class ProjectQuoteRevision
    {
        public int Id { get; set; }

        public int ProjectQuoteId { get; set; }
        public ProjectQuote? Quote { get; set; }

        // Customer-visible label ("A", "B", "C") + the monotonic 1-based number the
        // service computes (MAX+1 per quote). UNIQUE (ProjectQuoteId, RevisionNumber).
        [Required, StringLength(8)]
        public string RevisionLabel { get; set; } = "A";
        public int RevisionNumber { get; set; } = 1;

        public ProjectQuoteRevisionStatus VersionStatus { get; set; } = ProjectQuoteRevisionStatus.Draft;

        public DateTime? SubmittedDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public int? ValidityDays { get; set; }

        [Column(TypeName = "decimal(18,4)")] public decimal? TotalPrice { get; set; }
        [Column(TypeName = "decimal(9,4)")] public decimal? TargetMarginPct { get; set; }
        [Column(TypeName = "decimal(9,4)")] public decimal? EstimatedMarginPct { get; set; }
        public int? QuotedLeadTimeDays { get; set; }
        public DateTime? QuotedDeliveryDate { get; set; }

        public string? Assumptions { get; set; }
        public string? Inclusions { get; set; }
        public string? Exclusions { get; set; }
        public string? Exceptions { get; set; }
        public string? CommercialNotes { get; set; }
        public string? TechnicalNotes { get; set; }
        public string? CustomerFacingNotes { get; set; }
        public string? InternalNotes { get; set; }

        public ProjectQuoteApprovalStatus ApprovalStatus { get; set; } = ProjectQuoteApprovalStatus.NotRequired;
        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }
        [StringLength(100)] public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }

        // ── Frozen submission snapshot — THE locked record (spec §"Quote versions") ──
        // Once IsSnapshotLocked is true (set on Submit), the revision's commercial
        // fields and lines are immutable. The service refuses any overwrite and
        // directs the caller to mint a new revision instead.
        public bool IsSnapshotLocked { get; set; } = false;
        public DateTime? SnapshotLockedAt { get; set; }

        // Forward-ref to the frozen internal cost model (PR-5 ProjectEstimateSnapshot).
        // No DB FK yet — the table doesn't exist until PR-5.
        public int? SourceEstimateSnapshotId { get; set; }

        // What was included/excluded at the moment of submission (free-form scope freeze).
        public string? ScopeSnapshot { get; set; }

        // Set when this revision is awarded → contract baseline (PR-6).
        public bool ConvertedToBaseline { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }

        public ICollection<ProjectQuoteLine>? Lines { get; set; }
    }

    // ================================================================
    // ProjectQuoteLine — a priced line on a revision.
    // Scoped through ProjectQuoteRevision → ProjectQuote (no own CompanyId).
    // ================================================================
    [Table("ProjectQuoteLines")]
    public class ProjectQuoteLine
    {
        public int Id { get; set; }

        public int ProjectQuoteRevisionId { get; set; }
        public ProjectQuoteRevision? Revision { get; set; }

        // 1-based line order. UNIQUE (ProjectQuoteRevisionId, LineNo).
        public int LineNo { get; set; }

        // Optional catalog item. SET NULL — an item outlives the quote line, and
        // ETO lines may be ad-hoc (PartNumber/Description only, no Item row).
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(100)] public string? PartNumber { get; set; }
        [StringLength(500)] public string? Description { get; set; }

        [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; } = 0m;
        [StringLength(16)] public string? Uom { get; set; }

        [Column(TypeName = "decimal(18,4)")] public decimal? UnitPrice { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal? ExtendedPrice { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal? UnitCost { get; set; }

        public int? LeadTimeDays { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ----------------------------------------------------------------
    // Enums
    // ----------------------------------------------------------------

    public enum ProjectRfqStatus
    {
        Open = 0,        // received, not yet quoting
        Quoting = 1,     // a quote is being built
        Quoted = 2,      // at least one quote submitted to the customer
        Won = 3,         // a quote was awarded
        Lost = 4,        // customer awarded elsewhere
        NoBid = 5,       // we declined to quote
        Cancelled = 6,   // customer pulled the RFQ
    }

    public enum ProjectQuoteType
    {
        Budgetary = 0,             // rough/early-stage budgetary number
        Firm = 1,                  // firm, committed pricing
        RoughOrderOfMagnitude = 2, // ROM
        Revised = 3,               // a re-quote
    }

    public enum ProjectQuoteStatus
    {
        Draft = 0,       // being built
        Active = 1,      // live with the customer
        Won = 2,         // awarded (a revision became baseline)
        Lost = 3,        // not awarded
        NoBid = 4,       // we declined
        Expired = 5,     // validity lapsed
        Withdrawn = 6,   // we pulled it
    }

    public enum ProjectQuoteRevisionStatus
    {
        Draft = 0,       // editable
        Submitted = 1,   // sent to customer — snapshot LOCKED
        Superseded = 2,  // a later revision replaced it
        Awarded = 3,     // became the contract baseline
        Lost = 4,        // not selected
        Expired = 5,     // validity lapsed
    }

    public enum ProjectQuoteApprovalStatus
    {
        NotRequired = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 3,
    }
}
