// Theme B9 Wave 2 PR-5 (2026-05-30) — Quote-to-cash spine: the estimate layer.
//
// The INTERNAL cost model behind a quote. Where ProjectQuoteRevision is the
// customer-facing price, ProjectEstimate is the estimator's build-up of what it
// will actually cost us. On quote submission the estimate is frozen into an
// immutable ProjectEstimateSnapshot (spec §"Estimate snapshot — internal cost
// model frozen at quote submission") that the revision's SourceEstimateSnapshotId
// points at — so a later edit to the working estimate can never rewrite the
// numbers we committed to in a submitted quote.
//
//   ProjectEstimate         — a working cost model for a project (mutable in Draft).
//   ProjectEstimateLine      — a cost-element line (Material / Labor / Subcontract /
//                              Overhead / …), typed with the SAME B7 CostElementType
//                              enum the item-standard cost split uses (estimate-as-
//                              standard tie-in, spec cascade PR-5).
//   ProjectEstimateSnapshot  — the immutable frozen copy + a JSON freeze of the lines.
//
// Conventions match PR-4 (CustomerProject/ProjectQuote): tenant trio on the top-level
// entities, lines scoped through their parent estimate (no CompanyId), xmin
// concurrency, enum DB defaults matched, additive new tables.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Masters; // CostElementType (B7 estimate-as-standard tie-in)

namespace Abs.FixedAssets.Models.Projects
{
    // ================================================================
    // ProjectEstimate — a working internal cost model.
    // ================================================================
    [Table("ProjectEstimates")]
    public class ProjectEstimate
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

        // Optional quote this estimate supports. SET NULL.
        public int? ProjectQuoteId { get; set; }
        public ProjectQuote? Quote { get; set; }

        // Per-company human-readable number. UNIQUE (CompanyId, EstimateNumber).
        [Required, StringLength(64)]
        public string EstimateNumber { get; set; } = string.Empty;

        [StringLength(200)] public string? Title { get; set; }
        [StringLength(2000)] public string? Description { get; set; }

        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public ProjectEstimateStatus Status { get; set; } = ProjectEstimateStatus.Draft;

        // Target margin the estimator is shooting for (informational).
        [Column(TypeName = "decimal(9,4)")] public decimal? TargetMarginPct { get; set; }

        // Risk contingency applied on top of the rolled-up cost (percent).
        [Column(TypeName = "decimal(9,4)")] public decimal? ContingencyPct { get; set; }

        [StringLength(100)] public string? EstimatorName { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }

        public ICollection<ProjectEstimateLine>? Lines { get; set; }
    }

    // ================================================================
    // ProjectEstimateLine — a cost-element line. Scoped through the estimate.
    // ================================================================
    [Table("ProjectEstimateLines")]
    public class ProjectEstimateLine
    {
        public int Id { get; set; }

        public int ProjectEstimateId { get; set; }
        public ProjectEstimate? Estimate { get; set; }

        // 1-based order. UNIQUE (ProjectEstimateId, LineNo).
        public int LineNo { get; set; }

        // The B7 cost-component-split category (estimate-as-standard tie-in).
        public CostElementType CostElementType { get; set; } = CostElementType.Material;

        [StringLength(500)] public string? Description { get; set; }

        // Optional related catalog item. SET NULL.
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; } = 0m;
        [StringLength(16)] public string? Uom { get; set; }

        [Column(TypeName = "decimal(18,4)")] public decimal? UnitCost { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal? ExtendedCost { get; set; }

        // Labor-flavored lines: hours × rate. ExtendedCost still holds the dollar value.
        [Column(TypeName = "decimal(18,4)")] public decimal? Hours { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal? Rate { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ================================================================
    // ProjectEstimateSnapshot — the IMMUTABLE frozen cost model at quote submission.
    // ProjectQuoteRevision.SourceEstimateSnapshotId FKs to this (SET NULL).
    // ================================================================
    [Table("ProjectEstimateSnapshots")]
    public class ProjectEstimateSnapshot
    {
        public int Id { get; set; }

        // Tenant trio.
        public int? TenantId { get; set; }
        [Required] public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteIdSnapshot { get; set; }

        // Parent project. CASCADE (the snapshot is deleted with the project).
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // The source working estimate this was captured from. SET NULL — the snapshot
        // is immutable and outlives later edits/deletion of the working estimate.
        public int? ProjectEstimateId { get; set; }
        public ProjectEstimate? Estimate { get; set; }

        // The quote revision this snapshot was captured for (soft int — the revision
        // already points back via SourceEstimateSnapshotId; no FK to avoid coupling).
        public int? ProjectQuoteRevisionId { get; set; }

        [StringLength(8)] public string? Currency { get; set; }

        // ── Frozen roll-up by cost bucket (immutable) ──
        [Column(TypeName = "decimal(18,4)")] public decimal MaterialCost { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal LaborCost { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal SubcontractCost { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal OverheadCost { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal OtherCost { get; set; }
        // Direct roll-up before contingency.
        [Column(TypeName = "decimal(18,4)")] public decimal DirectTotalCost { get; set; }
        [Column(TypeName = "decimal(9,4)")] public decimal? ContingencyPct { get; set; }
        // Direct + contingency — the committed cost.
        [Column(TypeName = "decimal(18,4)")] public decimal TotalCost { get; set; }

        // The quoted price this cost was measured against (the revision TotalPrice at
        // capture) + the resulting estimated margin %, both frozen.
        [Column(TypeName = "decimal(18,4)")] public decimal? QuotedPrice { get; set; }
        [Column(TypeName = "decimal(9,4)")] public decimal? EstimatedMarginPct { get; set; }
        [Column(TypeName = "decimal(9,4)")] public decimal? TargetMarginPct { get; set; }

        public int LineCount { get; set; }

        // Full line detail frozen as JSON. READ-BACK-ONLY ⇒ text, not jsonb (hard-lock).
        public string? FrozenLinesJson { get; set; }

        // Immutable — captured once, never modified (no ModifiedAt).
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CapturedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ----------------------------------------------------------------
    // Enums
    // ----------------------------------------------------------------

    public enum ProjectEstimateStatus
    {
        Draft = 0,        // editable working estimate
        Snapshotted = 1,  // a frozen snapshot has been captured; estimate is locked
        Superseded = 2,   // replaced by a newer estimate
        Archived = 3,
    }
}
