// Theme B9 Wave 2 PR-6 (2026-05-30, CLOSES B9 Wave 2) — the contract/award layer.
//
// The capstone of the quote-to-cash spine: where a won quote becomes a binding
// contract and the project's cost/schedule baseline. Lands three entities + the
// two §20 gates the spine hinges on:
//
//   ProjectContract    — the binding agreement; carries the contract-review gate
//                        ("cannot launch project until required contract review is
//                        complete", spec §20) and the award/baseline link.
//   ProjectContractLine — contract deliverable lines (CLINs).
//   ProjectCustomerPO   — the customer purchase order(s) authorizing the work.
//
// Award validation ("cannot mark project awarded without an approved quote or
// authorized override", spec §20) + winning-revision→baseline live in
// IProjectContractService. Conventions match PR-4/PR-5 (tenant trio on top-level
// entities, lines scoped through the parent contract, xmin, enum defaults).

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // ================================================================
    // ProjectContract — the binding agreement + review gate + award link.
    // ================================================================
    [Table("ProjectContracts")]
    public class ProjectContract
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

        // Per-company human-readable number. UNIQUE (CompanyId, ContractNumber).
        [Required, StringLength(64)]
        public string ContractNumber { get; set; } = string.Empty;

        [StringLength(200)] public string? Title { get; set; }
        [StringLength(2000)] public string? Description { get; set; }

        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public ProjectContractStatus Status { get; set; } = ProjectContractStatus.Draft;

        // ── Contract-review gate (spec §20: cannot launch until review complete) ──
        public bool ReviewRequired { get; set; } = true;
        public ProjectContractReviewStatus ReviewStatus { get; set; } = ProjectContractReviewStatus.NotStarted;
        public DateTime? ReviewDueDate { get; set; }
        public DateTime? ReviewCompletedAt { get; set; }
        [StringLength(100)] public string? ReviewedByName { get; set; }

        // ── Award / baseline link (set by AwardQuoteRevisionAsync) ──
        // Soft refs into the quote spine (no DB FK — the awarded quote/revision live
        // in their own cascade aggregate; integrity enforced in the service).
        public int? AwardedProjectQuoteId { get; set; }
        public int? AwardedRevisionId { get; set; }
        public DateTime? AwardDate { get; set; }
        // The baseline contract value stamped from the winning revision's frozen total.
        [Column(TypeName = "decimal(18,4)")] public decimal? BaselineContractValue { get; set; }

        // When the project was launched off this contract (review-gated).
        public DateTime? LaunchedAt { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }

        public ICollection<ProjectContractLine>? Lines { get; set; }
    }

    // ================================================================
    // ProjectContractLine — a contract deliverable (CLIN). Scoped through the contract.
    // ================================================================
    [Table("ProjectContractLines")]
    public class ProjectContractLine
    {
        public int Id { get; set; }

        public int ProjectContractId { get; set; }
        public ProjectContract? Contract { get; set; }

        // 1-based order. UNIQUE (ProjectContractId, LineNo).
        public int LineNo { get; set; }

        // The contract-line / CLIN reference ("CLIN 0001").
        [StringLength(64)] public string? ContractLineReference { get; set; }

        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [StringLength(100)] public string? PartNumber { get; set; }
        [StringLength(500)] public string? Description { get; set; }

        [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; } = 0m;
        [StringLength(16)] public string? Uom { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal? UnitPrice { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal? ExtendedPrice { get; set; }

        public DateTime? BaselineStart { get; set; }
        public DateTime? BaselineFinish { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ================================================================
    // ProjectCustomerPO — the customer purchase order(s) authorizing the work.
    // ================================================================
    [Table("ProjectCustomerPOs")]
    public class ProjectCustomerPO
    {
        public int Id { get; set; }

        // Tenant trio.
        public int? TenantId { get; set; }
        [Required] public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteIdSnapshot { get; set; }

        // Parent project. CASCADE.
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Optional contract this PO is issued against. SET NULL.
        public int? ProjectContractId { get; set; }
        public ProjectContract? Contract { get; set; }

        // The customer's PO number ("WEIR-PO-2026-0421"). UNIQUE (CompanyId, CustomerPoNumber).
        [Required, StringLength(100)]
        public string CustomerPoNumber { get; set; } = string.Empty;

        public DateTime? PoDate { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal? PoValue { get; set; }
        [Required, StringLength(8)] public string Currency { get; set; } = "USD";

        [StringLength(500)] public string? Description { get; set; }
        public ProjectCustomerPoStatus Status { get; set; } = ProjectCustomerPoStatus.Open;

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

    public enum ProjectContractStatus
    {
        Draft = 0,         // being assembled
        UnderReview = 1,   // contract review in progress
        Awarded = 2,       // a quote revision was awarded → baseline set
        Active = 3,        // project launched off this contract
        Closed = 4,
        Cancelled = 5,
    }

    public enum ProjectContractReviewStatus
    {
        NotStarted = 0,
        InReview = 1,
        Complete = 2,   // gate satisfied — project may launch
        Waived = 3,     // review explicitly waived — gate satisfied
    }

    public enum ProjectCustomerPoStatus
    {
        Open = 0,
        Closed = 1,
        Cancelled = 2,
    }
}
