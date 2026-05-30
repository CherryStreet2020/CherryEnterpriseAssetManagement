// Theme B9 Wave 4 PR-10 (2026-05-30) — Project procurement spine. OPENS Wave 4.
//
// ProjectProcurementPlan / ProjectCommitment / ProjectReceipt — the buy-side
// execution layer that pegs purchasing to a CustomerProject. Sits alongside the
// new PurchaseOrder.CustomerProjectId FK (the shipped ProductionOrder precedent).
//
// The key business rule (spec §10 / §20): a project cannot be CLOSED while it
// still has OPEN commitments, unless an authorized user explicitly waives them.
// "Open" = a commitment in Open / PartiallyReceived state (i.e. value committed
// to a vendor that has not yet been fully received or formally closed).
//
// Conventions (per ProjectPhase / ProjectSchedule precedent): these are CHILDREN
// of a CustomerProject — they carry CustomerProjectId and are tenant-scoped
// THROUGH the parent project; NO CompanyId of their own. xmin concurrency token.
//   - ProjectProcurementPlan, ProjectCommitment: CASCADE from the project.
//   - ProjectReceipt: owned by its commitment (single CASCADE path
//     project→commitment→receipt). Its denormalized CustomerProjectId is a plain
//     indexed column with NO FK (adding a second CASCADE FK from the project would
//     create a multiple-cascade-path); GoodsReceiptId is a soft int reference.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectProcurementPlan — what the project INTENDS to buy. The planned
    // procurement line (material, subcontract, service…) on the WBS, against
    // which firm commitments are later raised.
    // =====================================================================
    public class ProjectProcurementPlan
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Optional WBS anchor + optional Item-master peg.
        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProjectProcurementCategory Category { get; set; } = ProjectProcurementCategory.Material;

        public decimal? PlannedQuantity { get; set; }
        [StringLength(16)]
        public string? UnitOfMeasure { get; set; }

        // Budgeted spend for this planned buy.
        public decimal? PlannedAmount { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime? NeedByDate { get; set; }

        // Long-lead items drive the schedule (the Ti / Inconel bar-stock buys).
        public bool IsLongLead { get; set; } = false;

        public ProjectProcurementPlanStatus Status { get; set; } = ProjectProcurementPlanStatus.Draft;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectCommitment — a FIRM financial commitment against the project
    // (a PO, subcontract, or blanket release). This is the entity the close
    // gate inspects: a project cannot close while any commitment is Open or
    // PartiallyReceived, unless explicitly waived. Set-once close stamp.
    // =====================================================================
    public class ProjectCommitment
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Optional links: the plan it fulfills, the WBS phase, the PO it
        // represents, and the vendor it commits spend to. All SET NULL — a
        // commitment outlives a deleted plan/phase/PO.
        public int? ProjectProcurementPlanId { get; set; }
        public ProjectProcurementPlan? ProjectProcurementPlan { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        public ProjectCommitmentType CommitmentType { get; set; } = ProjectCommitmentType.PurchaseOrder;

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // The committed value. Receipts draw this down.
        public decimal CommittedAmount { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public decimal? CommittedQuantity { get; set; }
        [StringLength(16)]
        public string? UnitOfMeasure { get; set; }

        public ProjectCommitmentStatus Status { get; set; } = ProjectCommitmentStatus.Open;

        public DateTime? CommittedDate { get; set; }
        public DateTime? ExpectedReceiptDate { get; set; }

        // Set-once close stamp (Received → Closed, or manual close).
        public DateTime? ClosedAt { get; set; }
        [StringLength(100)]
        public string? ClosedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public ICollection<ProjectReceipt> Receipts { get; set; } = new List<ProjectReceipt>();

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectReceipt — value/goods received against a commitment. Cumulative
    // receipts draw the commitment's open balance down; when fully received the
    // commitment auto-advances to Received. Owned by its commitment (single
    // CASCADE path). CustomerProjectId is denormalized (plain indexed int, no FK)
    // for project-scoped queries; GoodsReceiptId is a soft reference (no FK).
    // =====================================================================
    public class ProjectReceipt
    {
        public int Id { get; set; }

        public int ProjectCommitmentId { get; set; }
        public ProjectCommitment? Commitment { get; set; }

        // Denormalized owning project (no FK — reachable via the commitment;
        // a second CASCADE FK from the project would be a multiple-cascade-path).
        public int CustomerProjectId { get; set; }

        // Soft reference to the inventory GoodsReceipt that backs this, if any.
        public int? GoodsReceiptId { get; set; }

        [StringLength(64)]
        public string? ReceiptNumber { get; set; }

        public decimal ReceivedAmount { get; set; }
        public decimal? ReceivedQuantity { get; set; }

        public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — the 0 member is the CLR/model default (== DB default) to dodge
    // the EF enum-sentinel trap.
    // ---------------------------------------------------------------------

    public enum ProjectProcurementCategory
    {
        Material = 0,
        Subcontract = 1,
        Service = 2,
        Equipment = 3,
        Tooling = 4,
        Other = 5
    }

    public enum ProjectProcurementPlanStatus
    {
        Draft = 0,
        Approved = 1,
        Closed = 2,
        Cancelled = 3
    }

    public enum ProjectCommitmentType
    {
        PurchaseOrder = 0,
        Subcontract = 1,
        Blanket = 2,
        Other = 3
    }

    public enum ProjectCommitmentStatus
    {
        Open = 0,
        PartiallyReceived = 1,
        Received = 2,
        Closed = 3,
        Cancelled = 4
    }
}
