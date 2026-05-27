// B8 PR-PRO-5 (2026-05-27) — ProductionWipMove.
//
// Every movement of work-in-process between operations — automatic OR manual.
//
// THE BIC DIFFERENCE: Auto-advance on completion is the DEFAULT. When an
// operator completes quantity at Op 20, that quantity is IMMEDIATELY available
// at Op 30. No extra click. No separate "move" transaction. No supervisor
// intervention. The WipMove record is created automatically as an audit trail.
//
// Manual moves are the EXCEPTION — Send-Back (rework), Move-to-Specific
// (non-sequential), Split (partial to multiple ops), Hold (quality block).
// Every exception requires a reason. Every move is audited with full chain
// of custody back to the triggering ProductionOperationTransaction.
//
// Industry comparison:
//   SAP: Separate goods movement (MIGO) required after confirmation
//   Oracle: Explicit move transaction required
//   Epicor: "Auto-move" checkbox buried in routing setup
//   Plex: Auto-advance is default (closest to BIC)
//   Infor CSI: Requires explicit move transactions
//   IndustryOS: Auto-advance is DEFAULT + quality-hold gating + full audit
//
// AS9100 §8.5.1: "The organization shall implement production... under
// controlled conditions." The WipMove audit trail IS the controlled condition.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ────────────────────────────────────────────
    // ENUMS
    // ────────────────────────────────────────────

    /// <summary>
    /// How the WIP movement was triggered.
    /// </summary>
    public enum WipMoveType
    {
        /// <summary>System-generated on operation completion. DEFAULT. No operator action.</summary>
        AutoAdvance = 0,

        /// <summary>Operator/supervisor explicitly moved quantity to next sequential op.</summary>
        ManualMoveNext = 1,

        /// <summary>Rework — send material back to a prior operation.</summary>
        SendBack = 2,

        /// <summary>Non-sequential move to a specific operation (skip, parallel, rework insert).</summary>
        MoveToSpecific = 3,

        /// <summary>Split quantity across multiple destination operations.</summary>
        SplitMove = 4,

        /// <summary>Skip operation — quantity bypasses this op entirely.</summary>
        SkipAdvance = 5,

        /// <summary>Release from quality hold — triggers the pending auto-advance.</summary>
        HoldRelease = 6,

        /// <summary>Reversal of a prior move (undo).</summary>
        Reversal = 7,
    }

    /// <summary>
    /// Status of the WIP move record.
    /// </summary>
    public enum WipMoveStatus
    {
        Completed = 0,      // Move executed successfully
        Pending = 1,        // Move blocked by quality hold — will execute on release
        Reversed = 2,       // Move was reversed (undo)
        Cancelled = 3,      // Move cancelled before execution
    }

    // ────────────────────────────────────────────
    // ENTITY
    // ────────────────────────────────────────────

    /// <summary>
    /// Audit record for every WIP movement between production operations.
    /// One record per move. Auto-advance creates these silently on completion.
    /// </summary>
    [Table("ProductionWipMoves")]
    public class ProductionWipMove
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        // ----- Identity -----
        [Required] [StringLength(48)]
        public string MoveNumber { get; set; } = string.Empty;

        // ----- Production Order -----
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        // ----- From / To operations -----
        public int FromOperationId { get; set; }
        public ProductionOperation? FromOperation { get; set; }

        public int ToOperationId { get; set; }
        public ProductionOperation? ToOperation { get; set; }

        public int FromSequenceNumber { get; set; }
        public int ToSequenceNumber { get; set; }

        // ----- Move classification -----
        public WipMoveType MoveType { get; set; } = WipMoveType.AutoAdvance;
        public WipMoveStatus Status { get; set; } = WipMoveStatus.Completed;

        // ----- Quantity -----
        public decimal Quantity { get; set; }

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; }

        // ----- Reason (required for exceptions, auto-populated for auto-advance) -----
        [StringLength(2000)]
        public string? MoveReason { get; set; }

        // ----- Chain of custody: what triggered this move -----
        /// <summary>FK to the ProductionOperationTransaction that triggered this move (e.g., Complete action).</summary>
        public int? TriggeredByTransactionId { get; set; }
        public ProductionOperationTransaction? TriggeredByTransaction { get; set; }

        /// <summary>FK to the original move being reversed (when MoveType = Reversal).</summary>
        public int? OriginalMoveId { get; set; }
        public ProductionWipMove? OriginalMove { get; set; }

        // ----- Quality hold gating -----
        /// <summary>If true, the destination operation has a quality hold — move is Pending until released.</summary>
        public bool QualityHoldBlocked { get; set; }
        [StringLength(500)]
        public string? QualityHoldReason { get; set; }
        public DateTime? QualityHoldReleasedAtUtc { get; set; }
        [StringLength(120)]
        public string? QualityHoldReleasedBy { get; set; }

        // ----- Cost snapshot at move time -----
        public decimal? UnitCostAtMove { get; set; }
        public decimal? TotalCostAtMove { get; set; }

        // ----- Lot/Serial tracking -----
        [StringLength(500)]
        public string? LotNumbers { get; set; }
        [StringLength(500)]
        public string? SerialNumbers { get; set; }

        // ----- Audit -----
        public DateTime MovedAtUtc { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? MovedBy { get; set; }

        [StringLength(4000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        [StringLength(120)]
        public string? UpdatedBy { get; set; }

        // ----- Concurrency -----
        public byte[]? RowVersion { get; set; }
    }
}
