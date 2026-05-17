using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — MrbDisposition stub.
    //
    // Material Review Board outcome record. Referenced by
    // ProductionBatch.QuarantineDispositionId and
    // ProductionBatchStateEvent.MrbDispositionId when a batch
    // transitions through the Quarantined -> ReleasedAfterReview
    // or Quarantined -> Cancelled paths.
    //
    // Shipped as a stub FK target so the references are valid from
    // day one. The "Hold flag became Quarantined state then needed
    // an MRB linkage" migration is the #3 documented regret pattern
    // across aerospace MES systems surveyed (PR #119.13a research
    // report Q10).
    //
    // What this stub holds:
    //   - Id, DispositionNumber, Outcome enum, Justification, Approver,
    //     ApprovedAt, audit
    //
    // What lands later (PR #119.13c):
    //   - Full Quarantine workflow UI
    //   - Linkage to corrective-action workflow
    //   - Approval routing + ApprovalAction integration
    //   - Quarantine reason taxonomy + photos / evidence URLs
    //
    // AS9100 8.7 Control of Nonconforming Outputs requires:
    //   - Identification of non-conforming material (covered by
    //     Quarantined state on the batch)
    //   - Disposition recorded (this table)
    //   - Authority to disposition recorded (ApprovedBy field)
    //   - Justification recorded (Justification field)
    //   - Re-inspection if reworked (event log handles)
    //
    // Reference: ADR-013 §"Recommendation" item 6 + PR #119.13a
    // research report Q4 + Q7.
    [Table("MrbDispositions")]
    public class MrbDisposition
    {
        public int Id { get; set; }

        [Required]
        [StringLength(32)]
        [Display(Name = "MRB #")]
        public string DispositionNumber { get; set; } = string.Empty;

        public MrbOutcome Outcome { get; set; } = MrbOutcome.PendingReview;

        // The reason / justification for the chosen disposition.
        // Required at service-layer for non-PendingReview outcomes.
        [StringLength(2000)]
        public string? Justification { get; set; }

        // Free-text classification of the non-conformance type for
        // categorization in audits. Free text 64 chars — every shop
        // has its own taxonomy.
        [StringLength(64)]
        public string? NonConformanceType { get; set; }

        // The MRB approver. Per AS9100 8.7 the authority to disposition
        // must be recorded.
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // Optional pointer to evidence (photos, lab results, customer
        // concession PDFs).
        [StringLength(500)]
        public string? EvidenceUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }

    // MRB disposition outcomes per AS9100 8.7 Control of Nonconforming
    // Outputs.
    public enum MrbOutcome
    {
        PendingReview = 0,
        UseAsIs = 1,            // accept the non-conforming output with documented justification
        Rework = 2,              // bring back into conformance via additional processing
        Repair = 3,              // bring into a usable condition without full conformance
        ReturnToSupplier = 4,
        Scrap = 5,
        Concession = 6,          // customer-granted exception
    }
}
