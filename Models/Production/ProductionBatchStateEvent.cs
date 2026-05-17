using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — ProductionBatchStateEvent audit log.
    //
    // Per-transition audit row for the ProductionBatch state machine.
    // AS9100 8.5.2 + NADCAP AC7102 6.x both require that batch-level
    // state changes (especially Quarantined and ReleasedAfterReview)
    // capture: actor, timestamp, reason, FK to disposition where
    // applicable.
    //
    // Cardinality: many StateEvents per ProductionBatch. Append-only;
    // never updated or deleted. Cascade-delete with the batch on
    // explicit batch delete, but expected usage is "batches never get
    // deleted in regulated workflows."
    //
    // Why a separate table (vs a column on ProductionBatch):
    //   - Multiple Hold/Resume cycles per batch are common
    //   - MRB disposition workflow needs each Quarantine event captured
    //     individually (a batch can go through Quarantine more than
    //     once if Material Review releases then re-quarantines)
    //   - Auditor requirement: linear append-only history, not a
    //     "current status snapshot"
    //
    // Reference: PR #119.13a research report Q4 (status machine) + Q10
    // (audit-table regret pattern).
    [Table("ProductionBatchStateEvents")]
    public class ProductionBatchStateEvent
    {
        public int Id { get; set; }

        public int ProductionBatchId { get; set; }
        public ProductionBatch? ProductionBatch { get; set; }

        public ProductionBatchStatus FromStatus { get; set; }
        public ProductionBatchStatus ToStatus { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? ChangedBy { get; set; }

        // Free-text reason. For Hold/Quarantined transitions a reason
        // is required at the service layer.
        [StringLength(500)]
        public string? Reason { get; set; }

        // Optional FK to a Material Review Board disposition — set on
        // Quarantined -> ReleasedAfterReview or Quarantined -> Cancelled
        // transitions. MrbDisposition is a stub table in this PR.
        public int? MrbDispositionId { get; set; }
        public MrbDisposition? MrbDisposition { get; set; }
    }
}
