using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — Raw sensor event firehose.
    //
    // This is the source-of-truth for every reading. It is a TimescaleDB
    // hypertable partitioned by ReadingAt (1-day chunks). Compression
    // policy compresses chunks after 7 days (TimescaleDB columnar with
    // delta-of-delta timestamps + XOR floats — the same outcome as PI's
    // SmartCompression / swinging-door without a separate algorithm).
    // Retention policy drops chunks older than 90 days; FDA / SOX
    // tenants override per-company.
    //
    // Hypertables require the partitioning column to appear in any
    // uniqueness constraint, so the primary key is composite (Id,
    // ReadingAt). Id remains bigserial — Npgsql-EF maps long with
    // ValueGeneratedOnAdd to bigserial by default — but it is unique
    // only WITHIN a ReadingAt chunk. App-layer code addresses sensor
    // events by (AssetId, ReadingType, ReadingAt) which is the indexed
    // hot path; the Id is only used for FK references from snapshot
    // values and alarm open/close pointers.
    //
    // SchemaVersion supports forward-compatible field additions —
    // gateways and historical data coexist while we evolve the schema.
    [Table("SensorEvents")]
    public class SensorEvent
    {
        public long Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        // Optional FK to the SensorProfile row that defines thresholds
        // and metadata for this channel. May be null for ad-hoc readings
        // (e.g., demo seed data, manual operator entries).
        public int? AssetSensorChannelId { get; set; }

        public SensorReadingType ReadingType { get; set; }

        [Column(TypeName = "numeric(14,4)")]
        public decimal Value { get; set; }

        public UnitOfMeasure Unit { get; set; }

        // Event time as reported by the device. May be older than
        // IngestedAt for store-and-forward replay from offline edge
        // gateways. The hypertable partitions on this column.
        public DateTime ReadingAt { get; set; }

        // Server-receipt time — set by the API at ingest, ignored if
        // a client provides one. (ReadingAt - IngestedAt) > 1 hour
        // flags the event as "late" for gateway-disconnect diagnostics.
        public DateTime IngestedAt { get; set; } = DateTime.UtcNow;

        // Provenance label. Examples: "opcua", "mqtt", "sparkplugb",
        // "rest", "manual", "seed", "demo". Stamped on every row for
        // audit and integration diagnostics.
        [Required, StringLength(32)]
        public string Source { get; set; } = "";

        // IEC 62443 / ISA-99 source zone (Purdue level). See PurdueZone.
        public PurdueZone SourceZone { get; set; }

        // NAMUR NE 107 device health state. See DeviceHealthCode.
        public DeviceHealthCode QualityCode { get; set; }

        // Raw OPC UA quality byte preserved for audit. NE 107 enum
        // above is the application-level interpretation; we keep the
        // wire-level byte so a customer's audit team can reconstruct
        // the original gateway view.
        public byte OpcQualityByte { get; set; }

        // Computed at write time against SensorProfile thresholds.
        // Drives the SensorAlarm state machine.
        public bool IsOutOfSpec { get; set; }

        // Forward-compat for additive schema changes. New gateways
        // emit higher SchemaVersion values; older readers ignore
        // unknown extension fields.
        public byte SchemaVersion { get; set; } = 1;

        // Optional correlation id ties multiple events to a single
        // ingest batch (e.g., a 200-event POST /api/v1/sensors/events
        // call). Useful for partial-failure audit and replay.
        public Guid? CorrelationId { get; set; }
    }
}
