using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Catalog;

namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — ISA-18.2 documented operator response.
    //
    // ISA-18.2 mandates that every alarm have a rationalization — a
    // documented explanation of why the alarm exists, the consequence
    // of ignoring it, the prescribed operator response, and the
    // target response time. /Alarms surfaces this content alongside
    // each live alarm so the operator sees the playbook, not just
    // the symptom.
    //
    // Rationalizations are keyed by (EquipmentClass, ReadingType,
    // Priority). The seeder ships 168 sensible defaults (14 classes
    // × 3 primary reading types × 4 priorities) on first run; admins
    // tune the wording in /Admin/AlarmRationalization. Revisions are
    // versioned and audited so a regulator can trace the operator
    // response text in effect at any past moment.
    [Table("AlarmRationalizations")]
    public class AlarmRationalization
    {
        public int Id { get; set; }

        // Human-readable composite key for ergonomics in the seed
        // catalog and admin UI. Example: "CNC_MACHINING_CENTER.Vibration.P2_High".
        // Unique with (EquipmentClassId, ReadingType, Priority, Version).
        [Required, StringLength(128)]
        public string AlarmKey { get; set; } = "";

        // Optional FK — null = applies to any asset whose AssetType
        // does not resolve to a specific EquipmentClass. The seeder
        // creates a generic fallback per (ReadingType, Priority).
        public int? EquipmentClassId { get; set; }
        public EquipmentClass? EquipmentClass { get; set; }

        public SensorReadingType ReadingType { get; set; }

        public AlarmPriority Priority { get; set; }

        // ---- The ISA-18.2 rationalization content ----

        [Required, StringLength(500)]
        public string Justification { get; set; } = "";

        [Required, StringLength(500)]
        public string Consequence { get; set; } = "";

        [Required, StringLength(2000)]
        public string OperatorResponse { get; set; } = "";

        // Target Response Time (ISA-18.2 TRT). The maximum acceptable
        // delay between alarm Open and operator Acknowledged for this
        // priority. Used by the operator-burden widget on /Alarms to
        // flag overdue alarms.
        public TimeSpan TargetResponseTime { get; set; }

        // Optional pointer to the SOP, runbook, or work-instruction
        // doc that fully describes the response.
        [StringLength(500)]
        public string? ProcedureReference { get; set; }

        // ---- Versioning ----
        // Rationalizations are append-only for audit. A revision adds
        // a new row with Version++ and the prior row's EffectiveUntil
        // is set. /Alarms always looks up the row where now() falls
        // within [EffectiveFrom, EffectiveUntil ?? +infinity).
        public int Version { get; set; } = 1;

        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

        public DateTime? EffectiveUntil { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByUserId { get; set; }

        public bool Active { get; set; } = true;
    }
}
