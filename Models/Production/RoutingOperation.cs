using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// =============================================================================
// Sprint 13.5 PR #5c — RoutingOperation (template step on a Routing)
//
// One step on a Routing. The template — when a ProductionOrder gets released
// against this Routing, each RoutingOperation gets COPIED into a
// ProductionOperation row on the order (snapshot discipline — don't dynamic-
// lookup post-release).
//
// SEQUENCE NUMBERING: SAP/Oracle/Epicor convention is steps of 10 (10, 20, 30...)
// so inserts don't require renumbering. We follow that.
//
// 5-TIME DECOMPOSITION (per SAP "standard value key" + Oracle's identical
// model): Setup (per-batch one-time) + Run (per-unit, scales with qty) +
// Queue (waiting at WC before start) + Move (transit between WCs) + Wait
// (between operations for cure / cool / cleared inspection). All vendors
// converge on this 5-time pattern; we adopt it verbatim.
//
// OperationType: convergent across all 8 vendors researched.
//
// PredecessorOperationId + IsParallel: nullable predecessor allows parallel ops
// without forcing strict linear sequence — Oracle 24C added this; it's table
// stakes for ETO and complex fab.
//
// IsOptional: some ops conditional (e.g. "deburr if HardnessRC > 30") — set true
// and the operator can skip with a reason code.
// =============================================================================
public enum ProductionOperationType
{
    Setup = 0,         // Tool change, fixture install, machine warm-up
    Run = 1,           // Production cycle (the meat of the routing)
    Inspect = 2,       // In-process inspection, FAI, final QC
    Move = 3,          // Transit between WCs
    Wait = 4,          // Cure / cool / dry / waiting for clearance
    Subcontract = 5,   // Send to outside vendor (heat treat, plating, paint)
    Teardown = 6,      // Tool removal, fixture removal, machine cleanup
    Rework = 7,        // Rework loop (typically inserted ad-hoc, not on the routing master)
}

[Table("RoutingOperations")]
public class RoutingOperation
{
    public int Id { get; set; }

    public int RoutingId { get; set; }
    public Routing? Routing { get; set; }

    // Sequence (in 10s — leaves gaps for inserts).
    public int SequenceNumber { get; set; } = 10;

    // PR #5c.1 — snapshot of Routing.LocationId at create time. Lets site-scoped
    // queries avoid joining through Routing AND survives the parent routing being
    // re-pointed to a different site post-create (which shouldn't happen but the
    // snapshot is the guard).
    public int LocationIdSnapshot { get; set; }

    // Where this op runs.
    public int WorkCenterId { get; set; }

    public ProductionOperationType OperationType { get; set; } = ProductionOperationType.Run;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    // 5-time decomposition (SAP standard value key).
    public decimal SetupTimeMins { get; set; } = 0;          // Per batch (one-time)
    public decimal RunTimePerUnitMins { get; set; } = 0;     // Scales with batch qty
    public decimal QueueTimeMins { get; set; } = 0;          // Waiting at WC before start
    public decimal MoveTimeMins { get; set; } = 0;           // Transit from prior WC
    public decimal WaitTimeMins { get; set; } = 0;           // After this op, before next can start

    // Yield (Oracle 24C — table stakes).
    public decimal YieldPct { get; set; } = 100;             // 0-100; <100 expects scrap

    // Sequencing flexibility.
    public int? PredecessorOperationId { get; set; }         // null = follows by SequenceNumber
    public bool IsParallel { get; set; } = false;
    public bool IsOptional { get; set; } = false;            // Operator can skip with reason

    // Operator-facing.
    [MaxLength(8000)]
    public string? Instructions { get; set; }                // Rich text / markdown

    [MaxLength(500)]
    public string? RequiredSkillCodes { get; set; }          // CSV — "CNC_OP, FANUC_PROG"
    [MaxLength(500)]
    public string? RequiredToolingIds { get; set; }          // CSV of tooling IDs

    // Costing override (default: WorkCenter's rate).
    public decimal? CostRateOverridePerHour { get; set; }

    // Audit.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }
}
