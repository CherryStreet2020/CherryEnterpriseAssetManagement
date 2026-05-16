using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — ISA-18.2-conformant alarm lifecycle row.
    //
    // One SensorAlarm row per alarm instance. Lifecycle:
    //   Open  →  Acknowledged  →  Cleared
    //   Open  →  Shelved (with REQUIRED ShelvedUntil expiry)
    //   Open  →  Suppressed (by SuppressionMode while a context is active)
    //
    // The ingest service drives the state machine:
    //   - sensor's IsOutOfSpec flips true with no Open alarm for that
    //     (asset, type) → INSERT new SensorAlarm with State=Open
    //   - sensor's IsOutOfSpec still true while alarm Open → UPDATE
    //     PeakValue/PeakAt only
    //   - sensor's IsOutOfSpec flips false → UPDATE alarm to Cleared
    //     with ClearingEventId
    //
    // ISA-18.2 requires a documented operator response per alarm —
    // RationalizationId points at AlarmRationalization which holds the
    // (Justification, Consequence, OperatorResponse, TargetResponseTime,
    // ProcedureReference) text. /Alarms surfaces that text alongside
    // the live value so operators see WHAT to do, not just WHAT is bad.
    [Table("SensorAlarms")]
    public class SensorAlarm
    {
        public long Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public SensorReadingType ReadingType { get; set; }

        public AlarmState State { get; set; } = AlarmState.Open;

        public AlarmPriority Priority { get; set; } = AlarmPriority.P3_Medium;

        // FK to the AlarmRationalization row that documents the
        // operator response for this alarm type. Resolved at alarm-
        // open time by (EquipmentClassId, ReadingType, Priority).
        public int RationalizationId { get; set; }
        public AlarmRationalization? Rationalization { get; set; }

        // ---- Open ----
        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

        // FK to the SensorEvent that opened this alarm. Tied to the
        // bigserial Id from the hypertable; valid for joining across
        // chunks because the FK constraint is informational only — we
        // do not enforce referential integrity into the hypertable.
        public long OpeningEventId { get; set; }

        // ---- Acknowledged ----
        public DateTime? AcknowledgedAt { get; set; }
        public int? AcknowledgedByUserId { get; set; }
        public User? AcknowledgedByUser { get; set; }

        [StringLength(2000)]
        public string? AcknowledgementNote { get; set; }

        // ---- Cleared ----
        public DateTime? ClearedAt { get; set; }
        public long? ClearingEventId { get; set; }

        [StringLength(500)]
        public string? ClearedReason { get; set; }

        // ---- Shelved (ISA-18.2) ----
        // Shelving REQUIRES an expiry — the operator cannot indefinitely
        // hide an alarm. UI enforces the picker max at 7 days by default
        // (configurable per tenant). ShelvedUntil is non-null iff
        // State = Shelved.
        public DateTime? ShelvedUntil { get; set; }

        [StringLength(500)]
        public string? ShelfReason { get; set; }

        public int? ShelvedByUserId { get; set; }
        public User? ShelvedByUser { get; set; }

        // ---- Suppressed (mode-based, ISA-18.2) ----
        public AlarmSuppressionMode? SuppressionMode { get; set; }

        // ---- Peak tracking ----
        [Column(TypeName = "numeric(14,4)")]
        public decimal? PeakValue { get; set; }

        public DateTime? PeakAt { get; set; }
    }
}
