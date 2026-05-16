using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — FDA 21 CFR Part 11 / SOX §404 audit-grade
    // point-in-time records.
    //
    // A SensorSnapshot is an immutable, signed capture of the state of
    // one or more assets at a specific instant. Snapshots are
    // append-only — there is NO UpdatedAt, NO delete path, NO mutating
    // operations on this table after insert. Part 11 explicitly
    // requires this.
    //
    // Snapshots can be triggered four ways:
    //   1. Scheduled  — shift change, hourly, etc. (cron / pg_cron)
    //   2. Deviation  — automatic when SensorEvent.IsOutOfSpec flips
    //                   true; captures the whole asset's state at the
    //                   moment of the breach for the audit trail.
    //   3. ShiftChange — operator hits "End shift" on /Plant/Floor.
    //   4. UserRequest — explicit POST /api/v1/sensors/snapshot.
    //
    // SignatureHash is a SHA-256 over (CapturedAt + Reason + Values
    // JSON). Tamper-detection only in PR #118.6. Full Part 11
    // intent-to-sign e-signatures land in PR #123 (MFA) — at which
    // point SignatureMethod can flip from Sha256 to
    // ElectronicSignature on a per-snapshot basis.

    public enum SnapshotReason : byte
    {
        Scheduled = 0,
        Deviation = 1,
        ShiftChange = 2,
        UserRequest = 3,
        Audit = 4,
    }

    public enum SnapshotSignatureMethod : byte
    {
        Sha256 = 0,                  // tamper-detection only
        Sha256WithIntent = 1,        // captures an intent-to-sign declaration
        ElectronicSignature = 2,     // full Part 11 e-sig, requires MFA (PR #123)
    }

    public enum GampValidationStatus : byte
    {
        Unvalidated = 0,             // system was not in a validated state
        InValidation = 1,            // captured during validation activity
        Validated = 2,               // captured against a validated, released system
        Archived = 3,                // historical snapshot moved to cold storage
    }

    [Table("SensorSnapshots")]
    public class SensorSnapshot
    {
        public long Id { get; set; }

        // Server-stamped at insert. The single source of truth for
        // "when did this snapshot apply." NOT user-supplied.
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        public SnapshotReason Reason { get; set; }

        // Optional FK to the SensorEvent that triggered this snapshot
        // (deviation-driven). Null for scheduled / shift-change /
        // user-request snapshots.
        public long? TriggerEventId { get; set; }

        // Optional FK to User who requested this snapshot (manual).
        // Null for system-triggered.
        public int? CapturedByUserId { get; set; }
        public User? CapturedByUser { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        // SHA-256 over a canonical serialization of the snapshot's
        // contents. Tampering with stored Values is detectable by
        // re-hashing and comparing.
        [Required]
        public byte[] SignatureHash { get; set; } = Array.Empty<byte>();

        public SnapshotSignatureMethod SignatureMethod { get; set; } = SnapshotSignatureMethod.Sha256;

        public GampValidationStatus GampValidationStatus { get; set; } = GampValidationStatus.Unvalidated;

        public ICollection<SensorSnapshotValue> Values { get; set; } = new List<SensorSnapshotValue>();
    }

    // One row per (snapshot, asset, reading-type). Captures the
    // sensor value as it was at the moment of the snapshot. Values
    // are sourced from AssetSensorLatest at capture time, or from a
    // specific SensorEvent for deviation-triggered captures.
    [Table("SensorSnapshotValues")]
    public class SensorSnapshotValue
    {
        public long Id { get; set; }

        public long SnapshotId { get; set; }
        public SensorSnapshot? Snapshot { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public SensorReadingType ReadingType { get; set; }

        [Column(TypeName = "numeric(14,4)")]
        public decimal Value { get; set; }

        public UnitOfMeasure Unit { get; set; }

        public DateTime ReadingAt { get; set; }

        public byte OpcQualityByte { get; set; }

        public DeviceHealthCode QualityCode { get; set; }

        public bool IsOutOfSpec { get; set; }
    }
}
