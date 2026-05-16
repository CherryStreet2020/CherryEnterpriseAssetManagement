# ADR-011: Industrial Sensor Data Architecture

**Status:** Proposed v0.2 (2026-05-16) — awaiting Dean's sign-off.
**Date:** 2026-05-16 (v0.1: morning · v0.2: evening, after web-research pass)
**Deciders:** Dean Dunagan
**Categories:** Architecture · Data · Integration · Operations · Compliance · Security
**Closes:** PR #117 follow-up (the seeder-on-every-load problem); the ship-blocking slow loads behind `AssetHealthService.RecomputeAllAsync`; the missing audit-grade snapshot capability for FDA/SOX customers.
**Supersedes:** the implicit "single `AssetSensorReadings` table is sufficient" decision baked into PR #117.1.
**Related:** ADR-001 (Receiving accrual), ADR-003 (GL account resolver).

## What changed in v0.3 (deploy-day addendum, 2026-05-16 evening)

PR #118.1 deployed and crashed on the first migration apply — the Replit Agent caught a real architectural constraint I'd missed: **Replit Postgres ships the Apache 2.0 build of TimescaleDB 2.13, not the Community (TSL) build.** Several core features of v0.2's design are TSL-only:

- `CREATE MATERIALIZED VIEW ... WITH (timescaledb.continuous) AS ...` (continuous aggregates)
- `SELECT add_continuous_aggregate_policy(...)`
- `SELECT add_compression_policy(...)`
- `SELECT add_retention_policy(...)`
- `ALTER TABLE ... SET (timescaledb.compress, ...)`

All raise `Npgsql.PostgresException: 0A000: functionality not supported under the current "apache" license` on the Apache build, aborting EF's MigrateAsync transaction and preventing app startup.

PR #118.1.1 stripped these from the migration. The substrate now deploys cleanly, but the rollup/compression/retention machinery has to move from "Timescale does it for us" to **app-layer scheduled jobs**:

| Original (TSL, v0.2) | Replacement (Apache, v0.3) |
|---|---|
| `WITH (timescaledb.continuous)` continuous aggregates with auto-refresh | Regular `CREATE MATERIALIZED VIEW ... AS SELECT ...` + Hangfire/Quartz `REFRESH MATERIALIZED VIEW CONCURRENTLY` at the same cadence (1 min / 5 min / 1 hour) |
| `add_compression_policy('SensorEvents', INTERVAL '7 days')` | App-layer scheduled job calling `SELECT compress_chunk(chunk_name)` for chunks older than 7 days |
| `add_retention_policy('SensorEvents', INTERVAL '90 days')` | App-layer scheduled job calling `SELECT drop_chunks('SensorEvents', INTERVAL '90 days')` |
| `ALTER TABLE SET (timescaledb.compress, segmentby = ...)` | Set up segmentby on the chunk before `compress_chunk()` via raw SQL on first compression run |

Trade-offs:

- **Slower refresh of rollups** — TSL's continuous aggregates use incremental merge-based refresh. Our regular MV `REFRESH CONCURRENTLY` is a full re-aggregation over the bucket window each cycle. Acceptable at our scale; revisit if a customer-scale TSL upgrade becomes available.
- **More moving parts** — three background jobs to schedule, monitor, alert. Mitigated by Hangfire's dashboard + retry semantics.
- **Lighter Postgres-side dependency** — we use *less* of TimescaleDB. Easier to migrate to plain Postgres partitioning later if we ever need to leave TimescaleDB entirely. Net portability win.

Long-term escape hatch: if a Cherry deployment hosts on Azure Database for PostgreSQL Flexible Server (or any environment that supports TimescaleDB Community), we can re-introduce the continuous aggregates with a one-migration restoration. The schema is forward-compatible.

PR #118.2's `SensorIngestService` ships the write path; the three app-layer scheduled jobs (rollup refresh, compression, retention) land in PR #118.4. Same cadences as the original ADR.

## What changed in v0.2

After Dean's "is this best in class?" challenge, I did the web-research homework. Twelve gaps from v0.1 are now closed with standards-grounded specifics. Major additions:

1. **ISA-18.2-conformant alarm model** — priority, rationalization metadata, shelving with required expiry, suppression-by-mode, alarm-flood detection, operator-burden metrics. v0.1 had a toy alarm table; v0.2 has an industry-grade one.
2. **NAMUR NE 107 sensor health states** wired to `SensorEvent.QualityCode` (Good/Uncertain/Bad/Maintenance) and aligned with OPC UA DeviceHealth enum via the PA-DIM Companion Specification.
3. **Typed Units of Measure** (`UnitOfMeasure` enum + conversion table) per ISO 80000 / UNECE Recommendation 20. No more free-form `string Unit`.
4. **Schema versioning** on `SensorEvent.SchemaVersion` for forward compatibility.
5. **Out-of-order ingest contract** for edge store-and-forward (`IngestedAt` vs `ReadingAt` + continuous-aggregate refresh strategy).
6. **Asset Administration Shell (AAS) endpoint** sketch, conformant with IDTA-01002-3-2 (Industrie 4.0 digital-twin standard). First EAM to natively expose AAS.
7. **High-frequency (kHz) ingest path** acknowledged with a separate chunk strategy.
8. **ML feature-store view** as a continuous-aggregate-backed materialized view with rolling lag windows.
9. **IEC 62443 / ISA-99 zone model** — `SensorEvent.SourceZone` (Purdue L0-L3), zone-aware RLS in PR #122.
10. **Regional data residency** — `Asset.RegionCode` and per-region DbContext for multi-region deployments.
11. **Concrete TimescaleDB compression and retention defaults** with citations to TimescaleDB 2.17 production best-practices research.
12. **Standards conformance table** expanded to name ISA-18.2, NAMUR NE 107, ISO 80000, ISO 14224, GAMP 5 2nd Ed (+ FDA CSA Sept 2025), IEC 62443, Sparkplug B, PA-DIM, AAS/IDTA.

Plus a precise ISO 22400-2 OEE formula spec for PR #126.

---

## Context

CherryAI EAM is positioning to displace Maximo / SAP PM / Infor EAM / OSIsoft AVEVA PI. Today's data layer for sensor history cannot do that. The detail on what's broken is the same as v0.1's Context section — preserved below — and the bar for "best in class" is higher than v0.1's data substrate could reach. v0.2 raises the bar.

### What we have today (post PR #117.8)

- One table: `AssetSensorReadings(Id, AssetId, ReadingType, Value, Unit, ReadingAt, Source, IsOutOfSpec, CreatedAt)`.
- Three denormalized columns on `Asset`: `CurrentTemperature`, `CurrentVibration`, `CurrentPressure`, `SensorReadingsLastUpdated`.
- Health computed in app code by `AssetHealthService` from raw OOS counts.
- Seeding via `IndustrialAssetSeeder` (~5K rows on startup).

### What's broken or missing (carried from v0.1, no changes)

1. Schema mixes raw ingest with read cache.
2. No time-series partitioning (vanilla Postgres heap doesn't scale to 10K assets × 1Hz).
3. No native rollups.
4. No audit-grade snapshots (FDA 21 CFR Part 11 + SOX §404).
5. No alarm lifecycle.
6. No standards conformance plumbing.
7. No ingest protocol path (customers run OPC-UA / MQTT / Sparkplug B).
8. Operational immaturity (seed-on-page-load was the symptom).

### What v0.2 also addresses (new in v0.2)

9. **No standardized device-health vocabulary.** Process industries expect NAMUR NE 107 (Good/Uncertain/Bad/Maintenance). A `byte Quality` field is wrong; we need a named enum mapped to the OPC UA DeviceHealth via PA-DIM.
10. **No typed Units of Measure.** Free-form strings break automatic conversions, audits, and cross-asset comparisons.
11. **No documented alarm rationalization.** ISA-18.2 requires a documented operator response per alarm. Without that, we can't claim "best in class" against any process-industry customer.
12. **No edge buffering / out-of-order ingest contract.** Real plants lose network connections. Out-of-order events corrupt continuous aggregates if not specified.
13. **No digital-twin standard.** Industrie 4.0 customers expect AAS endpoints. Being the first EAM to ship AAS natively is actual disruption.
14. **No security-zone awareness.** IEC 62443 zone/conduit segmentation is table-stakes for critical infrastructure (water, power, oil & gas).
15. **No regional data residency story.** EU customers and US Federal customers won't sign without it.

---

## Decision

Adopt a **6-table industrial sensor data architecture** (v0.1 had 5; v0.2 adds `AlarmRationalization`) backed by **TimescaleDB** as a Postgres extension. The architecture is conformant with: ISA-95 layering, ISO 22400-2 KPIs, ISA-18.2 alarm management, NAMUR NE 107 sensor health, ISO 8000-8 data quality, ISO 80000 / UNECE Rec. 20 units of measure, ISO 14224 reliability taxonomy, FDA 21 CFR Part 11, GAMP 5 Second Edition + FDA CSA (Sept 2025), SOX §404, IEC 62443 / ISA-99 cybersecurity zones, Sparkplug B (Eclipse Tahu), OPC UA + PA-DIM, and Industrie 4.0 / IDTA-01002-3-2 (Asset Administration Shell).

### D-011-1. TimescaleDB extension on Postgres

```sql
CREATE EXTENSION IF NOT EXISTS timescaledb;
```

TimescaleDB v2.17+ gives us: hypertables with chunk partitioning, continuous aggregates with **merge-based refresh** (added in 2.17 — dramatically faster than the older delete-and-reinsert refresh), columnar compression (`hypercore`, 10-20x typical reduction), retention policies, 100+ hyperfunctions. All five tables live in the same Postgres instance, same `AppDbContext`, same migrations pipeline.

### D-011-2. The six tables

```
┌───────────────────┐      ┌──────────────────────┐
│  SensorEvent      │─────▶│ SensorRollupMinute   │  TimescaleDB
│  (hypertable)     │      │ SensorRollupHour     │  continuous
│                   │      │ SensorRollupDay      │  aggregates
└─────────┬─────────┘      └──────────────────────┘
          │                          │
          │ writer also              │ ML feature store reads
          │ updates atomically:      │ from rollups (view-only)
          ▼                          ▼
┌───────────────────┐
│ AssetSensorLatest │  denormalized 1-row-per-(asset, type) read cache
└───────────────────┘

┌───────────────────┐
│ SensorSnapshot    │  immutable point-in-time records (audit/compliance)
│ + SensorSnapshot- │
│   Value           │
└───────────────────┘

┌───────────────────┐    ┌────────────────────────┐
│ SensorAlarm       │───▶│ AlarmRationalization   │  documented operator
│ (lifecycle row)   │    │ (catalog, ISA-18.2)    │  response per alarm
└───────────────────┘    └────────────────────────┘
```

### D-011-3. `SensorEvent` — the raw firehose (v0.2 schema)

```csharp
public class SensorEvent
{
    public long Id { get; set; }                             // bigserial
    public int AssetId { get; set; }
    public int? AssetSensorChannelId { get; set; }           // FK to SensorProfile row
    public SensorReadingType ReadingType { get; set; }
    public decimal Value { get; set; }                       // numeric(14,4)
    public UnitOfMeasure Unit { get; set; }                  // ENUM (v0.2 — typed UoM)
    public DateTime ReadingAt { get; set; }                  // event time (from device)
    public DateTime IngestedAt { get; set; }                 // server time (for late-data detection)
    public string Source { get; set; } = "";                 // "opcua" / "mqtt" / "sparkplugb" / "rest" / "seed"
    public PurdueZone SourceZone { get; set; }               // L0-L3, IEC 62443 (v0.2)
    public DeviceHealthCode QualityCode { get; set; }        // NE 107 (v0.2 — replaces byte Quality)
    public byte OpcQualityByte { get; set; }                 // raw OPC UA quality byte preserved for audit
    public bool IsOutOfSpec { get; set; }                    // computed at write time vs SensorProfile thresholds
    public byte SchemaVersion { get; set; } = 1;             // v0.2 — forward compat
    public Guid? CorrelationId { get; set; }                 // ties multiple events to one ingest batch
}
```

Where:

```csharp
public enum DeviceHealthCode : byte
{
    // NAMUR NE 107 + OPC UA DeviceHealth via PA-DIM
    Good = 0,                  // green
    Uncertain = 1,             // yellow — may be out-of-spec
    Failure = 2,               // red — invalid signal
    Maintenance = 3,           // blue — valid but service required soon
    OutOfService = 4,          // device disabled / not reporting
}

public enum PurdueZone : byte
{
    // IEC 62443 zones via Purdue Reference Model
    L0Process = 0,             // physical sensors/actuators
    L1Control = 1,             // PLCs
    L2Supervisory = 2,         // SCADA / HMI
    L3Operations = 3,          // MES / CMMS — Cherry's natural home
    // L4 ERP, L5 enterprise — out of scope for sensor ingest
}
```

Hypertable creation, indexing, retention:

```sql
SELECT create_hypertable('SensorEvent', 'ReadingAt',
  chunk_time_interval => INTERVAL '1 day');

CREATE INDEX ix_sensorevent_asset_type_time
  ON SensorEvent (AssetId, ReadingType, ReadingAt DESC);

CREATE INDEX ix_sensorevent_oos
  ON SensorEvent (AssetId, ReadingAt DESC)
  WHERE IsOutOfSpec = true;

-- Compression after the active write window (production best-practice
-- per TimescaleDB 2.17 community guidance: compress when chunk unlikely
-- to be modified further; 7 days leaves headroom for late-arriving
-- out-of-order data from edge buffers).
SELECT add_compression_policy('SensorEvent', INTERVAL '7 days');

-- Retention: 90 days hot, then drop. Per-tenant override for FDA/SOX
-- customers (regulatory retention 2-7 years; snapshots persist forever).
SELECT add_retention_policy('SensorEvent', INTERVAL '90 days');
```

**Why these defaults**: 1-day chunks are the production sweet-spot for 1Hz process variables; smaller chunks (6-hour) for high-frequency channels (see D-011-12). 7-day compress-after preserves the active write window plus tolerance for out-of-order edge replay. Expected compression ratio: 10-20x on slowly-changing process data (TimescaleDB columnar with delta-of-delta timestamps + XOR floats achieves the same outcome as PI's swinging-door without a separate algorithm).

### D-011-4. Continuous aggregates — auto-refreshed rollups

```sql
CREATE MATERIALIZED VIEW SensorRollupMinute
  WITH (timescaledb.continuous) AS
SELECT
  AssetId,
  ReadingType,
  time_bucket('1 minute', ReadingAt) AS BucketStart,
  AVG(Value)            AS AvgValue,
  MIN(Value)            AS MinValue,
  MAX(Value)            AS MaxValue,
  STDDEV_SAMP(Value)    AS StdDev,
  COUNT(*)              AS SampleCount,
  COUNT(*) FILTER (WHERE IsOutOfSpec)                AS OosCount,
  COUNT(*) FILTER (WHERE QualityCode = 'Uncertain')  AS UncertainCount,
  COUNT(*) FILTER (WHERE QualityCode = 'Failure')    AS FailureCount,
  COUNT(*) FILTER (WHERE QualityCode = 'Maintenance') AS MaintenanceCount
FROM SensorEvent
WHERE QualityCode IN ('Good', 'Uncertain', 'Maintenance')  -- exclude Failure/OutOfService from averages
GROUP BY AssetId, ReadingType, BucketStart;

-- Hour and Day rollups follow the same pattern with different bucket sizes.

SELECT add_continuous_aggregate_policy('SensorRollupMinute',
  start_offset      => INTERVAL '2 hours',
  end_offset        => INTERVAL '1 minute',
  schedule_interval => INTERVAL '1 minute');
```

NE 107 quality is preserved through the rollup so the ML feature store and dashboards can reason about data quality, not just values.

### D-011-5. `AssetSensorLatest` — denormalized read cache

```csharp
public class AssetSensorLatest
{
    public int AssetId { get; set; }
    public SensorReadingType ReadingType { get; set; }       // composite PK
    public decimal Value { get; set; }
    public UnitOfMeasure Unit { get; set; }                  // v0.2 — typed
    public DateTime ReadingAt { get; set; }
    public DeviceHealthCode QualityCode { get; set; }        // v0.2
    public bool IsOutOfSpec { get; set; }
    public string Tone { get; set; } = "ok";                 // "ok" / "warn" / "crit" / "muted" — view-side classification
    public DateTime UpdatedAt { get; set; }
}
```

Same role as v0.1: the Plant Floor view's source of truth for the per-asset tile. One row per `(AssetId, ReadingType)`, point-lookup join, sub-millisecond.

### D-011-6. `SensorSnapshot` + `SensorSnapshotValue` — Part 11 audit-grade

Unchanged from v0.1 in shape, with two additions:

- `SensorSnapshot.GampValidationStatus` — `Unvalidated | InValidation | Validated | Archived`. Pharma customers need to mark snapshots that were captured during a validated state of the system (GAMP 5 Second Edition critical-thinking + FDA CSA).
- `SensorSnapshot.SignatureMethod` — `Sha256 | Sha256WithIntent | ElectronicSignature`. Pure hash for tamper detection today; intent-to-sign signatures land with PR #123 (MFA) for full Part 11 e-signature.

### D-011-7. `SensorAlarm` — ISA-18.2-conformant lifecycle (v0.2 — full rewrite)

```csharp
public class SensorAlarm
{
    public long Id { get; set; }
    public int AssetId { get; set; }
    public SensorReadingType ReadingType { get; set; }
    public AlarmState State { get; set; }
    public AlarmPriority Priority { get; set; }              // ISA-18.2 priority 1-4 (v0.2)
    public int RationalizationId { get; set; }               // FK to AlarmRationalization (v0.2)

    public DateTime OpenedAt { get; set; }
    public long OpeningEventId { get; set; }

    public DateTime? AcknowledgedAt { get; set; }
    public int? AcknowledgedByUserId { get; set; }
    public string? AcknowledgementNote { get; set; }

    public DateTime? ClearedAt { get; set; }
    public long? ClearingEventId { get; set; }
    public string? ClearedReason { get; set; }

    // ISA-18.2 shelving (v0.2) — temporary suppression with REQUIRED expiry
    public DateTime? ShelvedUntil { get; set; }
    public string? ShelfReason { get; set; }
    public int? ShelvedByUserId { get; set; }

    // ISA-18.2 mode-based suppression (v0.2)
    public AlarmSuppressionMode? SuppressionMode { get; set; }    // Startup | Shutdown | Maintenance | Calibration

    public decimal? PeakValue { get; set; }
    public DateTime? PeakAt { get; set; }
}

public enum AlarmState
{
    Open,
    Acknowledged,
    Cleared,
    Shelved,            // explicitly hidden until ShelvedUntil
    Suppressed,         // hidden by mode (Startup, Maintenance, etc.)
}

public enum AlarmPriority
{
    P1_Emergency = 1,   // immediate operator action required, safety/environmental consequence
    P2_High      = 2,   // prompt action, significant consequence
    P3_Medium    = 3,   // routine response, moderate consequence
    P4_Low       = 4,   // informational, log-only
}

public enum AlarmSuppressionMode
{
    Startup, Shutdown, Maintenance, Calibration, ManualOverride
}
```

### D-011-8. `AlarmRationalization` — documented operator response (NEW in v0.2)

This is the table ISA-18.2 requires. Every alarm has a documented:

- Reason it exists (justification)
- Consequence if ignored
- Operator response (what to do)
- Allowable time to respond (Target Response Time, TRT)

```csharp
public class AlarmRationalization
{
    public int Id { get; set; }
    public string AlarmKey { get; set; } = "";               // e.g. "CNC_MACHINING_CENTER.Vibration.Critical"
    public int? EquipmentClassId { get; set; }               // FK to EquipmentClass
    public SensorReadingType ReadingType { get; set; }
    public AlarmPriority Priority { get; set; }
    public string Justification { get; set; } = "";          // why this alarm exists
    public string Consequence { get; set; } = "";            // what happens if ignored
    public string OperatorResponse { get; set; } = "";       // documented action steps
    public TimeSpan TargetResponseTime { get; set; }         // ISA-18.2 TRT — how fast must we ack/clear
    public string? ProcedureReference { get; set; }          // pointer to SOP / runbook
    public int Version { get; set; } = 1;                    // rationalization revisions are audited
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
}
```

When the `/Alarms` page renders an alarm, it surfaces the `OperatorResponse` text alongside the value — so the operator sees not just "high vibration on AST-00012" but also "Operator response: throttle spindle to 60%, inspect coolant flow, file WO if persists > 5 min."

This is the single biggest "best in class" upgrade in v0.2.

### D-011-9. Alarm-flood detection and operator-burden metrics (NEW in v0.2)

ISA-18.2 sets explicit operator-burden targets: **maximum 6 new alarms per 10-minute window per operator** (industry "manageable" threshold). "Alarm flood" begins above 10/min.

```sql
-- View backing /Alarms operator-burden widget
CREATE VIEW VwAlarmRate10Min AS
SELECT
  SiteId,
  COUNT(*) FILTER (WHERE OpenedAt >= now() - INTERVAL '10 minutes') AS Last10Min,
  COUNT(*) FILTER (WHERE OpenedAt >= now() - INTERVAL '1 hour')     AS LastHour,
  CASE
    WHEN COUNT(*) FILTER (WHERE OpenedAt >= now() - INTERVAL '1 minute') >= 10 THEN 'FLOOD'
    WHEN COUNT(*) FILTER (WHERE OpenedAt >= now() - INTERVAL '10 minutes') > 6  THEN 'OVERLOADED'
    WHEN COUNT(*) FILTER (WHERE OpenedAt >= now() - INTERVAL '10 minutes') > 2  THEN 'BUSY'
    ELSE 'NORMAL'
  END AS LoadState
FROM SensorAlarm
JOIN Asset ON SensorAlarm.AssetId = Asset.Id
WHERE State IN ('Open', 'Acknowledged')
GROUP BY SiteId;
```

`/Alarms` shows a red banner when `LoadState = FLOOD`. The same metric is exported via OpenTelemetry so an Ops dashboard can graph load over time.

### D-011-10. `UnitOfMeasure` enum + conversion table (NEW in v0.2)

```csharp
public enum UnitOfMeasure : short
{
    // Process variables
    DegreesCelsius = 100, DegreesFahrenheit = 101, Kelvin = 102,
    PSI = 200, Bar = 201, KiloPascal = 202, MegaPascal = 203,
    RPM = 300,
    Millimeters = 400, Meters = 401, Inches = 402,

    // Vibration
    MillimetersPerSecond = 500,                              // velocity (ISO 10816)
    GravityForce = 501,                                      // acceleration (g)

    // Flow
    LitersPerMinute = 600, CubicMetersPerHour = 601, GallonsPerMinute = 602,

    // Electrical
    Volts = 700, Amperes = 701, Watts = 702, KiloWattHours = 703,
    PowerFactor = 704, HertzAC = 705,

    // Dimensionless / counts
    Percent = 900, Ratio = 901, Count = 902, Boolean = 903,
}

// Conversion handled by a small lookup table seeded at startup.
// Includes UNECE Rec. 20 code (e.g. "CEL" for celsius) for export
// compatibility with FDA submissions and partner integrations.
public class UnitConversion
{
    public UnitOfMeasure From { get; set; }
    public UnitOfMeasure To { get; set; }
    public decimal Multiplier { get; set; }
    public decimal Offset { get; set; }                      // for affine conversions (°C ↔ °F)
    public string UneceCode { get; set; } = "";              // e.g. "CEL", "PSI", "RPM"
}
```

Plant managers can override per-site display preferences (a German pharma plant prefers °C/Bar; a Texas plant prefers °F/PSI). The stored value is the source-of-truth unit; display unit is a render-time conversion.

### D-011-11. Edge store-and-forward / out-of-order ingest contract (NEW in v0.2)

Real plants lose network. Edge gateways buffer locally and replay when reconnected. Out-of-order events are normal, not exceptional.

**Contract for ingest API:**

- `ReadingAt` is the device-time event timestamp; can be older than now() by any amount.
- `IngestedAt` is server-receipt timestamp; set by the API, ignored if client provides one.
- An event is "late" if `IngestedAt - ReadingAt > 1 hour` (Sparkplug B sequence-gap typical threshold). Late events trigger a domain event so the Ops dashboard surfaces gateway disconnection patterns.
- The continuous-aggregate refresh policy uses `start_offset => INTERVAL '2 hours'` — i.e. rollups recompute the last 2 hours of buckets on every refresh. Events arriving > 2 hours late get a one-shot manual aggregate refresh queued for them. This handles 99% of real-world disconnects.
- For events arriving > 24 hours late (rare), the system emits an `AlarmPriority.P4_Low` alarm so an operator can investigate the gateway.

This is the spec PI/AVEVA call "store and forward with bounded rewind."

### D-011-12. High-frequency (kHz) ingest path (NEW in v0.2)

Process variables run at sub-Hz to 1 Hz. Vibration analysis runs at 1-10 kHz. Acoustic emission runs at 100+ kHz. The architecture above is sized for 1 Hz. For higher frequencies:

- Separate hypertable `SensorEventHighFrequency` with `chunk_time_interval = 1 hour`.
- No continuous aggregates by default — too expensive. The ML feature store reads windowed samples directly.
- Compression after 24 hours (shorter active-write window, smaller chunks).
- Retention 30 days hot (raw kHz is rarely needed past 30 days).
- Ingest goes to a separate endpoint `POST /api/v1/sensors/highfreq` with chunked binary payloads (Sparkplug B Protocol Buffers, not REST JSON).

Decision: ship the 1 Hz path in Sprint 2 (PR #118). Defer high-frequency to a Sprint 3 follow-up (PR #128) when the first vibration-analytics customer signs.

### D-011-13. Single write path — `ISensorIngestService`

```csharp
public interface ISensorIngestService
{
    Task IngestAsync(SensorEvent evt, CancellationToken ct);
    Task IngestBatchAsync(IEnumerable<SensorEvent> events, CancellationToken ct);
}
```

`IngestAsync` performs, in one transaction:

1. Insert into `SensorEvent`.
2. Update `AssetSensorLatest` (upsert by composite PK).
3. Re-evaluate `SensorProfile` thresholds → set `IsOutOfSpec`, `Tone`, `QualityCode`.
4. Drive `SensorAlarm` state machine (open / update peak / close, honoring shelving + suppression).
5. Stamp `IngestedAt` server-side.
6. (Post-commit, async) enqueue domain event for webhook outbox.

### D-011-14. Ingest API surface

```
POST /api/v1/sensors/events            # bulk write (1-1000 events per call)
POST /api/v1/sensors/snapshot          # explicit snapshot capture
POST /api/v1/sensors/alarms/{id}/ack   # acknowledge an alarm
POST /api/v1/sensors/alarms/{id}/shelve # ISA-18.2 shelving (requires reason + ShelvedUntil)
POST /api/v1/sensors/alarms/{id}/clear # manual clear with reason
GET  /api/v1/sensors/latest?assetIds=  # bulk read of AssetSensorLatest
GET  /api/v1/sensors/rollup?...&grain=hour&from=...&to=...  # continuous-aggregate read
GET  /api/v1/sensors/snapshots/{id}    # retrieve a snapshot for compliance export
GET  /api/v1/sensors/alarms?state=open&siteId=  # active alarms
GET  /api/v1/sensors/feature-store/window?...&lookbackMin=60   # ML feature store read (v0.2, see D-011-15)
```

All endpoints cookie-authenticated. Each ingest call carries a `Source` field and a `SourceZone` (Purdue level) — IEC 62443 zone provenance flows end-to-end.

### D-011-15. ML feature store (NEW in v0.2)

ML consumers (anomaly detection, RUL prediction, RCM/FMEA) read from a continuous-aggregate-backed view that exposes windowed lag features:

```sql
CREATE MATERIALIZED VIEW VwFeatureStoreHourly
  WITH (timescaledb.continuous) AS
SELECT
  AssetId,
  ReadingType,
  time_bucket('1 hour', ReadingAt) AS BucketStart,
  AVG(Value)                           AS AvgValue,
  LAG(AVG(Value), 1) OVER w            AS AvgValue_Lag1h,
  LAG(AVG(Value), 24) OVER w           AS AvgValue_Lag24h,
  LAG(AVG(Value), 168) OVER w          AS AvgValue_Lag1wk,
  STDDEV_SAMP(Value)                   AS StdDev,
  -- derived features
  AVG(Value) - LAG(AVG(Value), 1) OVER w  AS DeltaFromPrior
FROM SensorEvent
WHERE QualityCode = 'Good'
WINDOW w AS (PARTITION BY AssetId, ReadingType ORDER BY time_bucket('1 hour', ReadingAt))
GROUP BY AssetId, ReadingType, BucketStart;
```

This is read-only by external ML tooling (scikit-learn, PyTorch, Databricks via the Postgres JDBC). No new tables; the feature store is a derived view. Aspen Mtell's "Anomaly Agent" and PI Asset Analytics's "Asset Analytics" feature templates can both consume from here.

Anomaly detection itself (the model + scoring) is deferred to a future PR (PR #131, Sprint 3); v0.2 only specifies the consumer interface and the data shape.

### D-011-16. Asset Administration Shell (AAS) endpoint (NEW in v0.2)

Industrie 4.0 defines the digital twin via the Asset Administration Shell (AAS). Specification: IDTA-01002-3-2. We expose:

```
GET  /api/v1/aas/shells/{aasId}                       # Asset Administration Shell descriptor
GET  /api/v1/aas/shells/{aasId}/submodels             # list submodels for this asset
GET  /api/v1/aas/shells/{aasId}/submodels/{smIdShort} # single submodel content
GET  /api/v1/aas/registry/shells                      # AAS Registry — discovery
GET  /api/v1/aas/registry/submodels                   # Submodel Registry
```

Submodels we publish for each asset:

- **Nameplate** — manufacturer, model, serial, install date (from `EquipmentModel` + `Asset`)
- **Identification** — global asset ID, type, location (ISA-95 hierarchy)
- **TechnicalData** — datasheet excerpts from `EquipmentModel`
- **Documentation** — links to manuals, calibration records, certificates
- **TimeSeriesData** — sensor history pointers (links to `/api/v1/sensors/rollup`)
- **MaintenanceLog** — work order history from existing `MaintenanceEvent`
- **HealthMonitoring** — current `AssetSensorLatest` + open alarms

Mapping to existing models lives in `Services/Telemetry/AasMapping/`. JSON-LD output conforms to the IDTA OpenAPI schema.

**Why this matters**: Siemens, Bosch, SAP, and the entire Industrie 4.0 ecosystem expose AAS. Cherry being the first EAM to natively read/write AAS lets us drop into a plant's existing digital-twin stack without an adapter layer. This is what "disruption" actually looks like.

Implementation note: full AAS conformance is a multi-PR effort. PR #118 ships the **Nameplate**, **Identification**, **TimeSeriesData**, **HealthMonitoring**, **MaintenanceLog** submodels. **TechnicalData** and **Documentation** land in PR #119-#120 alongside the broader EquipmentModel work.

### D-011-17. UI surfaces

- `/Plant/Floor` — same view, but per-type loop replaced with one `AssetSensorLatest` query. Per-site render becomes one query (sub-100ms regardless of asset count).
- `/Plant/Floor/Asset/{id}` (NEW) — per-asset detail with live sparklines from `SensorRollupMinute`, NE 107 health badge, open-alarm list, AAS submodel viewer.
- `/Admin/Snapshots` (NEW) — list, filter, view, export snapshots. PDF export with audit trail.
- `/Alarms` (NEW) — active alarm board with `LoadState` banner, per-priority filter, bulk-ack, shelving UI with required expiry input, suppression-by-mode toggle.
- `/Admin/AlarmRationalization` (NEW) — editor for the documented operator response per alarm. Version-controlled; revisions audited.
- `/Admin/AAS/Registry` (NEW) — Asset Administration Shell registry view; lets partners discover what's available.

### D-011-18. Standards conformance table (rewritten in v0.2)

| Standard | What it covers | How v0.2 conforms |
|---|---|---|
| **ISA-95** | Layered manufacturing system architecture (L0-L4) | `SensorEvent.SourceZone` tags L0/L1/L2 origin. `/api/v1/sensors/events` is the L3 (MES/CMMS) boundary. AAS submodels expose the L3 ↔ L4 ERP boundary. |
| **ISO 22400-2** | Manufacturing KPIs (OEE, Availability, Performance, Quality) | PR #126 OEE module computes from `SensorRollupDay`. Formula spec in D-011-19. |
| **ISA-18.2** | Alarm management lifecycle (rationalization, priority, shelving, suppression, flood) | `SensorAlarm` + `AlarmRationalization` + flood detection view. `/Admin/AlarmRationalization` is the documented-response editor. |
| **NAMUR NE 107** | Sensor self-diagnostics (Good/Uncertain/Bad/Maintenance/OutOfService) | `SensorEvent.QualityCode` enum, propagated through rollups and surfaced on per-asset health badge. |
| **ISO 8000-8** | Data quality (accuracy, completeness, timeliness, validity) | `Quality` byte (OPC raw) + `IngestedAt` vs `ReadingAt` skew + per-asset DQ score computed nightly from rollup statistics. |
| **ISO 80000 / UNECE Rec. 20** | Units of measure | `UnitOfMeasure` enum + conversion table with UNECE codes (`CEL`, `PSI`, `RPM`, etc.). |
| **ISO 14224** | Petroleum/petrochem reliability & maintenance data taxonomy | `Asset.Iso14224Taxonomy` (NEW field, JSONB) — 9-level equipment hierarchy + failure mode codes. Oil & gas customers map their existing taxonomy into ours. |
| **FDA 21 CFR Part 11** | Electronic records and signatures | Append-only `SensorSnapshot` + `SignatureHash`. E-signature with intent-to-sign lands with PR #123 MFA. |
| **GAMP 5 Second Edition (2022) + FDA CSA (Sept 2025)** | Computer system validation, critical-thinking + risk-based | `SensorSnapshot.GampValidationStatus`. Validation evidence package generator (PR #119+). |
| **SOX §404** | Internal control over financial reporting | Snapshot-backed evidence for sensor readings feeding depreciation / impairment. |
| **IEC 62443 / ISA-99** | Industrial cybersecurity zones & conduits | `SensorEvent.SourceZone` (Purdue L0-L3). PR #122 RLS gets a zone column. Ingest endpoints enforce zone-aware auth: `/api/v1/sensors/events` accepts only L2/L3 gateway-authenticated sources. |
| **Sparkplug B (Eclipse Tahu)** | MQTT-based IIoT messaging (NBIRTH/DBIRTH/NDATA/DDATA + sequence-loss detection) | Gateway service in PR #128 (`ADR-012`) speaks Sparkplug B + Protocol Buffers natively. |
| **OPC UA + PA-DIM Companion Spec** | OT/IT device information modeling | PA-DIM-aligned `DeviceHealthCode` enum. PR #128 includes an OPC UA gateway client. |
| **IDTA-01002-3-2 (Asset Administration Shell, Industrie 4.0)** | Digital twin standard | `/api/v1/aas/*` endpoints with Nameplate / Identification / TimeSeriesData / HealthMonitoring / MaintenanceLog submodels. |

Notes:

- We do not need to be fully certified in any of these on day one. The architecture is conformant; certification is sales/legal work.
- For pharma customers, GAMP 5 + Part 11 conformance is sales-critical. For oil & gas, ISO 14224 conformance is sales-critical. For any process industry, ISA-18.2 conformance is sales-critical. The architecture above hits all three.

### D-011-19. ISO 22400-2 OEE formula spec (for PR #126)

OEE = **Availability × Performance × Quality**

```
Availability  = APT / PBT
Performance   = (PRI × PQ) / APT
Quality       = GQ / PQ

OEE           = Availability × Performance × Quality
```

Where (per ISO 22400-2:2014):

- **PBT** (Planned Busy Time) — total time the equipment was scheduled to produce.
- **APT** (Actual Production Time) — PBT minus all downtime (planned + unplanned).
- **PRI** (Planned Run Time per Item) — theoretical fastest cycle time per unit.
- **PQ** (Produced Quantity) — total units produced.
- **GQ** (Good Quantity) — first-pass yield (FPY / FTR / FTQ).

Source events:

- `PBT`, `APT` — derived from `Asset.StateChange` events (a new `AssetStateEvent` table, deferred to PR #126).
- `PRI` — stored on `EquipmentModel` (already exists; we add a `TheoreticalCycleSeconds` field).
- `PQ`, `GQ` — from production-counting sensors (a `ReadingType.PartCount` and `ReadingType.GoodPartCount` added to the existing enum).

PR #126's job is the math + the dashboard. The data substrate is this ADR.

---

## Migration plan

Phased migration is unchanged from v0.1 in shape (Phase A-F, six PRs, ~5 working days), with two refinements:

- **Phase A** also creates `AlarmRationalization` + `UnitConversion` reference tables and seeds them from data files (parallel to `EquipmentCatalogSeeder` pattern).
- **Phase F** ships `/Admin/AlarmRationalization`, `/Admin/AAS/Registry`, and the full alarm-flood detection widget on `/Alarms`.

Per-PR breakdown (revised for v0.2):

- [ ] **PR #118.1** (~1 day) — TimescaleDB extension + 6 tables (`SensorEvent`, 3 rollups, `AssetSensorLatest`, `SensorSnapshot/Value`, `SensorAlarm`, `AlarmRationalization`, `UnitConversion`) + entity types + DI. No behavior change.
- [ ] **PR #118.2** (~1 day) — `ISensorIngestService` + dual-write from seeder. New read endpoints (events, latest, rollup, feature-store). Seeds `AlarmRationalization` + `UnitConversion` from catalog JSON.
- [ ] **PR #118.3** (~1 day) — Read cutover: `/Plant/Floor` reads `AssetSensorLatest`. `AssetHealthService` reads from `SensorRollupHour`. Legacy view code deleted. Per-asset detail page `/Plant/Floor/Asset/{id}` ships with live sparklines + NE 107 health badge.
- [ ] **PR #118.4** (~0.5 day) — Single-write: seeder drops old-table writes. `IAssetSensorService` marked `[Obsolete]`.
- [ ] **PR #118.5** (~0.5 day) — Drop old `AssetSensorReadings` table + `Asset.Current*` columns.
- [ ] **PR #118.6** (~1.5 days) — `/Admin/Snapshots`, `/Alarms` with full ISA-18.2 controls (shelving, suppression, flood banner), `/Admin/AlarmRationalization`, AAS endpoints + `/Admin/AAS/Registry`. Scheduled-snapshot job. Deviation-triggered snapshot wired in.

**Total: ~5.5 working days, six PRs.** Each ships through ship-workflow v0.2 (the plugin we built today).

---

## Azure portability appendix

Unchanged from v0.1. Three additions in v0.2:

- **Region-aware deployments.** `Asset.RegionCode` (ISO 3166-1) + per-region tenant routing. Azure Front Door for geo-routing; per-region Azure Database for PostgreSQL Flexible Server instances. Customer data never crosses borders unless explicitly opted in.
- **FedRAMP path.** Azure Government cloud, separate tenant. ISO 14224 customers in US federal energy programs (DOE, FERC) sign here.
- **GAMP 5 validation packs.** Azure ARM templates + Terraform modules for Pharma customers' IQ/OQ/PQ documentation, generated automatically from the deployment.

---

## Alternatives Considered

All five v0.1 alternatives are still rejected for the same reasons. Two new alternatives considered in v0.2:

### Alternative 6: Don't write our own AAS endpoints; integrate with an existing AAS server (BaSyx, NOVAAS)

- **Pros:** Less code; community-maintained AAS infrastructure.
- **Cons:** Extra service to operate; AAS servers expect to OWN the data (asset registry, submodels). We'd duplicate every asset in two systems. The integration tax doesn't justify the saved code.
- **Why rejected:** We have the data already. Exposing it as AAS is a thin mapping layer (~500 lines of C#). External AAS servers solve a different problem (multi-vendor integration); we are the single source.

### Alternative 7: Use Kafka instead of direct REST for sensor ingest

- **Pros:** Higher write throughput. Decoupling between gateway and writer.
- **Cons:** Operational complexity. Replit doesn't host Kafka natively; we'd add a third-party MSK / Confluent dependency. The 1 Hz process-variable use case doesn't need it.
- **Why rejected:** Premature for the 1 Hz path. When we ship high-frequency ingest (PR #128), Kafka or Azure Event Hubs becomes the right buffer in front of `POST /api/v1/sensors/highfreq`. Document this in ADR-012 (Gateway Service) when we get there.

---

## Consequences

All v0.1 consequences still apply. v0.2 adds:

### Positive (v0.2)

- **Pharma customers close on weeks, not 9-month implementations.** GAMP 5 + Part 11 + ISA-18.2 + ISO 22400 conformance means the answer to "is your system validatable?" is yes.
- **Oil & gas opens.** ISO 14224 taxonomy support is the entry ticket.
- **Industrie 4.0 partners can integrate without an adapter.** Cherry is the first EAM to expose AAS natively.
- **Operator burden is measurable.** Alarm rate per 10 minutes is shown on `/Alarms` and exported via OpenTelemetry. We can publish operator-burden benchmarks against Maximo and PI.
- **The story to plant engineers is clean.** Instead of "our database is fast," it's "ISA-18.2 alarms, NE 107 device health, AAS digital twin, ISO 22400 OEE, GAMP 5 validatable, IEC 62443 zones, ISO 14224 taxonomy — open Postgres + TimescaleDB underneath, MIT-license Cherry on top."

### Negative (v0.2)

- **More tables.** 6 instead of v0.1's 5. Plus `UnitConversion` and `AlarmRationalization` reference tables. Mitigation: each is single-purpose and seeded from JSON catalogs (zero hand-entry).
- **More upfront content work.** `AlarmRationalization` needs documented operator-response text per EquipmentClass × ReadingType × Priority. Initial seeding from public ISA-18.2 templates; tenants customize over time. Mitigation: this is a content task, parallelizable with engineering.

### Neutral (v0.2)

- AAS submodel layouts evolve (IDTA-01002 has releases every ~6 months). We pin to V3.1.2 and bump on a quarterly cadence. Backward-compat is fine; AAS clients negotiate version.

---

## Open questions (v0.2 — refined)

1. **Snapshot retention** — append-only forever (FDA/SOX default) OK?
2. **Snapshot signature scope** — SHA-256 in PR #118.6, full Part 11 e-signature with intent-to-sign in PR #123 (after MFA lands). OK?
3. **Alarm ack scope** — per-alarm (industry default) or per-user (some pharma preference)? I'd default to per-alarm with operator-attribution in `AcknowledgedByUserId`.
4. **Sprint 2 vs Sprint 3 for snapshot+alarm UIs** — keep in Sprint 2 (PR #118.6) as planned, OR push to Sprint 3 if you want Sprint 2 to land just the data substrate?
5. **Pull PR #126 OEE forward into Sprint 2?** Much smaller now that the substrate is here.
6. **Demo data backfill** — keep ~5K rows or generate 30 days of 1-min data (~14M rows, ~1-2 GB compressed) for realistic sparklines?
7. **AlarmRationalization seed content** — do you want me to seed sensible defaults for the 14 EquipmentClass × 3 ReadingType × 4 Priority = 168 alarm types from the ISA-18.2 sample library, or empty + admin enters them as needed?
8. **AAS submodel scope for PR #118.6** — ship the 5 submodels I listed (Nameplate, Identification, TimeSeriesData, HealthMonitoring, MaintenanceLog) or trim to 3 (Nameplate + TimeSeriesData + HealthMonitoring) to keep the PR small?
9. **High-frequency path** — defer to PR #128 (Sprint 3) as proposed, or build a "vibration-only" early version into Sprint 2 for vibration-analytics demos?
10. **OPC UA / Sparkplug B gateway** — separate plugin service (ADR-012), or embed in the main app? Operational simplicity says embed; protocol-translation purity says separate. I'd embed for v1 and extract later if scale demands.

---

## Implementation Notes

File layout (v0.2):

```
Models/
  Telemetry/
    SensorEvent.cs
    SensorRollup.cs                  (entity types for the three rollup grains)
    AssetSensorLatest.cs
    SensorSnapshot.cs
    SensorSnapshotValue.cs
    SensorAlarm.cs
    AlarmRationalization.cs          (NEW v0.2)
    UnitOfMeasure.cs                 (enum, NEW v0.2)
    UnitConversion.cs                (NEW v0.2)
    DeviceHealthCode.cs              (enum, NEW v0.2)
    PurdueZone.cs                    (enum, NEW v0.2)
    AlarmState.cs                    (enum)
    AlarmPriority.cs                 (enum, NEW v0.2)
    AlarmSuppressionMode.cs          (enum, NEW v0.2)
Services/Telemetry/
  ISensorIngestService.cs
  SensorIngestService.cs
  ISensorAlarmService.cs
  SensorAlarmService.cs
  ISensorSnapshotService.cs
  SensorSnapshotService.cs
  AasMapping/                        (NEW v0.2)
    IAasShellMapper.cs
    NameplateSubmodelMapper.cs
    HealthMonitoringSubmodelMapper.cs
    TimeSeriesSubmodelMapper.cs
Controllers/Api/
  SensorEventsController.cs
  SensorAlarmsController.cs
  SensorSnapshotsController.cs
  AasController.cs                   (NEW v0.2)
Pages/Plant/
  Asset.cshtml(.cs)                  (per-asset detail with live charts)
Pages/Admin/
  Snapshots.cshtml(.cs)
  AlarmRationalization.cshtml(.cs)   (NEW v0.2)
  AasRegistry.cshtml(.cs)            (NEW v0.2)
Pages/Alarms/
  Index.cshtml(.cs)
data/                                (seed catalogs — JSON)
  alarm-rationalization.json         (NEW v0.2 — ISA-18.2 starter templates)
  unit-conversions.json              (NEW v0.2)
```

### Operational concerns

- TimescaleDB version pin: 2.17+ (confirmed available on Replit Postgres).
- Compression policy default: 7 days after chunk start.
- Retention default: 90 days for `SensorEvent`; forever for `SensorSnapshot`.
- AAS API version pin: IDTA-01002-3-2 (V3.1.2 release, current as of 2026-05).
- Sparkplug B version pin: Eclipse Tahu specification (latest as of search — version 2.2 confirmed in current Eclipse spec; 3.0 referenced in some 2024-2026 implementations but not yet on Eclipse Foundation's published spec page).
- IEC 62443 zone tagging requires gateway authentication. Gateways present a client certificate; the cert carries a zone claim; the API enforces it.

---

## Related Documents

- `MASTER_PLAN.md` — Sprint 2 / Sprint 3 backlog.
- `EQUIPMENT_CATALOG.md` — `SensorProfile` thresholds power `IsOutOfSpec` and seed `AlarmRationalization`.
- ADR-001 — Receiving accrual pattern.
- ADR-003 — Central GL account resolver.
- PR #117.6 — Seeder simplified to single-pass single-SaveChanges.
- PR #117.8 — Plant page performance fix.
- Future ADR-012 — Sparkplug B / OPC UA gateway service.

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-05-16 morning | Claude (with Dean) | v0.1 — initial 5-table proposal. |
| 2026-05-16 evening | Claude (with Dean) | v0.2 — after web-research pass closing 12 gaps from Dean's "is this best in class?" challenge. Added ISA-18.2 alarm rationalization, NAMUR NE 107, typed UoM, schema versioning, out-of-order ingest contract, AAS endpoints, high-frequency path, ML feature store, IEC 62443 zones, regional data residency, concrete TimescaleDB defaults, expanded standards conformance table, ISO 22400-2 OEE formula. |
| 2026-05-16 night | Claude (with Dean) | v0.3 — deploy-day addendum. Replit Postgres ships Apache 2.0 TimescaleDB, not Community/TSL. Continuous aggregates + auto-policies move from "Timescale does it for us" to app-layer scheduled jobs (Hangfire/Quartz REFRESH MATERIALIZED VIEW CONCURRENTLY + periodic compress_chunk / drop_chunks). Schema is forward-compatible; can restore continuous aggregates on TSL deployments later. |
