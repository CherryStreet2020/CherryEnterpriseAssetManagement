using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// =============================================================================
// Sprint 13.5 PR #5c — ProductionOperation (execution-time instance)
//
// The UNIVERSAL EXECUTION-TIME ENTITY that the MES event layer hangs off:
//
//   DowntimeEvent   (PR #5e) → FK ProductionOperation
//   ScrapEvent      (PR #5e) → FK ProductionOperation
//   ReworkEvent     (PR #5e) → FK ProductionOperation
//   MaterialConsumption (PR #5e) → FK ProductionOperation
//   OeeEvent        (PR #5g) → FK ProductionOperation
//   LotGenealogy / SerialGenealogy (PR #5f) → FK ProductionOperation (via materials consumed)
//
// SNAPSHOT FIELDS: SequenceNumber, WorkCenterId, planned times, etc. are
// COPIED from RoutingOperation at ProductionOrder release time. Don't dynamic-
// lookup — editing the Routing master post-release must NOT change in-flight
// orders. RoutingOperationId remains as a nullable FK for traceability +
// audit. Ad-hoc inserts have null RoutingOperationId.
//
// 8-STATE STATUS MACHINE: per MES research convergence.
//
//   Scheduled  - On the schedule, not yet released to floor
//   Released   - Operator can pick it up; not started
//   InSetup    - Operator is doing setup (tool change, fixture install)
//   Running    - Production cycle active
//   Paused     - Operator paused (lunch, meeting, brief stop)
//   Completed  - All qty produced, including any scrap accounting
//   Skipped    - Op was IsOptional + operator skipped with reason
//   Scrapped   - Op aborted, work lost (rare — full op scrap not unit scrap)
//
// OPERATOR ATTRIBUTION: OperatorUserIdsCsv lets multiple operators clock onto
// one op (a 2-person setup, a team-build). The dedicated Labor table arrives
// in PR #5d (Operator Workbench) and gives per-operator time stamps.
//
// LIVE MACHINE LINK: AssetId is the SPECIFIC machine running this op (not just
// the WorkCenter). When a WC has multiple Assets (MultiResource capacity),
// the dispatcher picks one. AssetId enables drilling from the op into live
// IoT state (Asset.CurrentOEE / CurrentAvailability / CurrentVibration etc.).
// =============================================================================
public enum ProductionOperationStatus
{
    Scheduled = 0,
    Released = 1,
    InSetup = 2,
    Running = 3,
    Paused = 4,
    Completed = 5,
    Skipped = 6,
    Scrapped = 7,
}

[Table("ProductionOperations")]
public class ProductionOperation
{
    public int Id { get; set; }

    public int ProductionOrderId { get; set; }

    // Traceability — nullable for ad-hoc inserts.
    public int? RoutingOperationId { get; set; }

    // Sprint 13.5 PR #5c.2 — Tenant lineage SNAPSHOT (sync gap fix).
    // PR #5c.1 added the LocationIdSnapshot DB COLUMN with DEFAULT 0 + CHECK >= 0
    // but the C# property was missing, so every EF insert silently left it at 0.
    // PR #5c.2 closes the loop: properties added on the entity AND the service
    // (IProductionOperationService.ReleaseFromRoutingAsync) stamps both values
    // from ProductionOrder.LocationId + Location.CompanyId at release time.
    //
    // Snapshot semantics: editing the master Routing or Location post-release must
    // NOT retroactively change in-flight production. These columns freeze the
    // tenant/site lineage at the moment the operation hits the floor.
    public int LocationIdSnapshot { get; set; }
    public int CompanyIdSnapshot { get; set; }

    // Snapshot of routing rev at release (audit + reproducibility).
    [MaxLength(10)]
    public string? RoutingRevisionSnapshot { get; set; }

    // SequenceNumber — copied at release, editable on the floor.
    public int SequenceNumber { get; set; } = 10;

    // WorkCenter — copied at release, the floor can reassign.
    public int WorkCenterId { get; set; }

    // The specific machine (when MultiResource WC — dispatcher picks one).
    public int? AssetId { get; set; }

    public ProductionOperationType OperationType { get; set; } = ProductionOperationType.Run;
    public ProductionOperationStatus Status { get; set; } = ProductionOperationStatus.Scheduled;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    // Snapshot of the time estimates (in case routing master changes post-release).
    public decimal PlannedSetupMins { get; set; } = 0;
    public decimal PlannedRunMins { get; set; } = 0;
    public decimal PlannedQueueMins { get; set; } = 0;
    public decimal PlannedMoveMins { get; set; } = 0;
    public decimal PlannedWaitMins { get; set; } = 0;

    // Actuals (PR #5d Operator Workbench writes these).
    public decimal ActualSetupMins { get; set; } = 0;
    public decimal ActualRunMins { get; set; } = 0;
    public decimal ActualDownMins { get; set; } = 0;        // From DowntimeEvent rollup (PR #5e)

    // Schedule.
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }

    // Quantity tracking.
    public decimal PlannedQty { get; set; } = 0;
    public decimal CompletedQty { get; set; } = 0;
    public decimal ScrappedQty { get; set; } = 0;
    public decimal ReworkQty { get; set; } = 0;

    // Operators (CSV of usernames — PR #5d adds dedicated Labor table for time stamps).
    [MaxLength(500)]
    public string? OperatorUserIdsCsv { get; set; }

    [MaxLength(8000)]
    public string? Instructions { get; set; }              // Snapshot from RoutingOperation
    [MaxLength(2000)]
    public string? Notes { get; set; }                     // Operator notes — appended over time

    // Skip reason (when Status == Skipped).
    [MaxLength(200)]
    public string? SkipReason { get; set; }

    // Audit.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }
}
