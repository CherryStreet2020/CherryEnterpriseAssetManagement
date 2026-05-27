// B8 PR-PRO-4 (2026-05-27) — ProductionOperationTransaction entity.
//
// EVENT LOG for every operation state change on the shop floor.
// Complements the ProductionOperation status machine — each transition
// (Start, Pause, Resume, Complete, etc.) creates a transaction record.
// The ProductionOperation holds the CURRENT state; this entity holds
// the HISTORY.
//
// WHY:
//   1. AUDIT — who changed what, when, and what were the quantities.
//   2. TIME ANALYSIS — setup vs run vs queue vs downtime decomposition.
//   3. COST — per-transaction labor + machine cost capture.
//   4. REVERSAL — completion reversals create paired records.
//   5. OPERATION INSERTION — AddOperation + InsertReworkOperation create
//      new ProductionOperation rows at runtime (absorbs B3 + B5b).
//
// 19 TRANSACTION TYPES:
//   Start, Pause, Resume, Stop, LogTime, EditTime, AddEmployee,
//   StartSetup, CompleteSetup, StartRun, CompleteRun,
//   Complete, PartialComplete, FinalComplete, ReverseCompletion,
//   SkipOperation, AddOperation, InsertReworkOperation, ChangeResource

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// Type of operation state change or action.
    /// </summary>
    public enum OperationTransactionType
    {
        Start = 0,                  // Released → Running (or InSetup if setup required)
        Pause = 1,                  // Running → Paused
        Resume = 2,                 // Paused → Running
        Stop = 3,                   // Running → Released (abnormal stop)
        LogTime = 4,                // Record time without state change
        EditTime = 5,               // Modify a prior time entry (audit trail)
        AddEmployee = 6,            // Add operator to the operation
        StartSetup = 7,             // Released → InSetup
        CompleteSetup = 8,          // InSetup → Running (setup → run transition)
        StartRun = 9,               // InSetup → Running (explicit run start)
        CompleteRun = 10,           // Running → Completed (run phase done)
        Complete = 11,              // Running → Completed (generic completion)
        PartialComplete = 12,       // Report partial quantity, stay Running
        FinalComplete = 13,         // Complete + mark as final op → triggers FG receipt
        ReverseCompletion = 14,     // Completed → Running (undo completion)
        SkipOperation = 15,         // Released → Skipped (optional ops only)
        AddOperation = 16,          // Insert new operation at runtime
        InsertReworkOperation = 17, // Insert rework op after current
        ChangeResource = 18,       // Reassign work center / machine / employee
    }

    [Table("ProductionOperationTransactions")]
    public class ProductionOperationTransaction
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        // ===== Transaction identity =========================================

        [Required] [StringLength(32)]
        public string TransactionNumber { get; set; } = string.Empty;

        public OperationTransactionType TransactionType { get; set; } = OperationTransactionType.Start;

        public DateTime TransactionDateUtc { get; set; } = DateTime.UtcNow;

        // ===== Source — which PRO + operation ================================

        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        public int OperationId { get; set; }
        public ProductionOperation? Operation { get; set; }

        /// <summary>Operation sequence at time of transaction.</summary>
        public int OperationSequence { get; set; }

        // ===== State transition ==============================================

        /// <summary>Operation status BEFORE this transaction.</summary>
        public ProductionOperationStatus StatusBefore { get; set; }

        /// <summary>Operation status AFTER this transaction.</summary>
        public ProductionOperationStatus StatusAfter { get; set; }

        // ===== Quantity =====================================================

        /// <summary>Good quantity reported in this transaction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal GoodQuantity { get; set; }

        /// <summary>Scrap quantity reported in this transaction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal ScrapQuantity { get; set; }

        /// <summary>Rework quantity reported in this transaction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReworkQuantity { get; set; }

        /// <summary>Reject quantity (failed inspection).</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal RejectQuantity { get; set; }

        // ===== Time =========================================================

        /// <summary>Setup time recorded in this transaction (minutes).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal SetupMinutes { get; set; }

        /// <summary>Run time recorded in this transaction (minutes).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal RunMinutes { get; set; }

        /// <summary>Machine time (may differ from run time for multi-machine ops).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal MachineMinutes { get; set; }

        /// <summary>Labor time (may differ from run time for multi-operator ops).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LaborMinutes { get; set; }

        // ===== Resource =====================================================

        /// <summary>Work center where this transaction occurred.</summary>
        public int? WorkCenterId { get; set; }

        /// <summary>Specific machine/asset used.</summary>
        public int? AssetId { get; set; }

        /// <summary>Operator who performed the action.</summary>
        [StringLength(100)]
        public string? OperatorId { get; set; }

        /// <summary>Crew size for this transaction.</summary>
        public int? CrewSize { get; set; }

        /// <summary>For ChangeResource: previous work center.</summary>
        public int? PreviousWorkCenterId { get; set; }

        /// <summary>For ChangeResource: previous asset.</summary>
        public int? PreviousAssetId { get; set; }

        // ===== Cost =========================================================

        /// <summary>Labor cost for this transaction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? LaborCost { get; set; }

        /// <summary>Machine/burden cost for this transaction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MachineCost { get; set; }

        /// <summary>Overhead/burden cost for this transaction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? BurdenCost { get; set; }

        // ===== Completion details ===========================================

        /// <summary>True if this is the final operation in the routing.</summary>
        public bool IsFinalOperation { get; set; }

        /// <summary>True if backflush materials on completion.</summary>
        public bool BackflushMaterials { get; set; }

        /// <summary>For FinalComplete: lot/serial numbers for FG receipt.</summary>
        [StringLength(500)]
        public string? CompletedLotSerials { get; set; }

        /// <summary>Destination for completed goods (next op or stock).</summary>
        [StringLength(50)]
        public string? DestinationLocation { get; set; }

        // ===== Skip / rework / add-op details ===============================

        /// <summary>For SkipOperation: reason for skipping.</summary>
        [StringLength(500)]
        public string? SkipReason { get; set; }

        /// <summary>For InsertReworkOperation: the newly created operation ID.</summary>
        public int? NewOperationId { get; set; }

        /// <summary>For InsertReworkOperation: rework instructions.</summary>
        [StringLength(4000)]
        public string? ReworkInstructions { get; set; }

        /// <summary>For AddOperation/InsertRework: the sequence number assigned.</summary>
        public int? NewOperationSequence { get; set; }

        // ===== Scrap detail =================================================

        [StringLength(50)]
        public string? ScrapReasonCode { get; set; }

        [StringLength(50)]
        public string? DefectCode { get; set; }

        [StringLength(50)]
        public string? CauseCode { get; set; }

        // ===== Reversal =====================================================

        public bool IsReversal { get; set; }
        public int? OriginalTransactionId { get; set; }
        public ProductionOperationTransaction? OriginalTransaction { get; set; }

        // ===== Quality flags ================================================

        public bool InspectionRequired { get; set; }
        public bool QualityHold { get; set; }

        // ===== Audit ========================================================

        [StringLength(100)]
        public string? PerformedBy { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [StringLength(100)] public string? CreatedBy { get; set; }
        [StringLength(100)] public string? UpdatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
