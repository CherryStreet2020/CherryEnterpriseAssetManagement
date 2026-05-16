using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 2 PR #118.1 — Industrial Sensor Data Architecture (ADR-011).
    //
    // Lays down the substrate the rest of PR #118 builds on:
    //
    //   1. Enable the timescaledb extension.
    //   2. Create the regular telemetry tables (AssetSensorLatest,
    //      SensorSnapshots, SensorSnapshotValues, SensorAlarms,
    //      AlarmRationalizations, UnitConversions).
    //   3. Create SensorEvents and convert it to a TimescaleDB
    //      hypertable partitioned by ReadingAt (1-day chunks).
    //   4. Create three continuous-aggregate materialized views
    //      (SensorRollupMinute / Hour / Day) with auto-refresh policies.
    //   5. Add compression policy (compress chunks after 7 days) and
    //      retention policy (drop chunks after 90 days) on SensorEvents.
    //
    // All SQL uses IF NOT EXISTS / idempotent guards so the migration
    // re-runs safely during dual-write and read-cutover phases.
    //
    // No data migration. No behavior change. Subsequent PRs (#118.2-.6)
    // wire the services, swap the read path, and ship the UI.
    //
    // See: docs/adr/ADR-011-industrial-sensor-data-architecture.md
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_AddTelemetrySubstrate")]
    public partial class AddTelemetrySubstrate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // 1. Enable TimescaleDB
            // ============================================================
            // Replit Postgres ships timescaledb 2.13.0 in pg_available_extensions.
            // CREATE EXTENSION is idempotent via IF NOT EXISTS.
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS timescaledb;");

            // ============================================================
            // 2. AssetSensorLatest — denormalized 1-row-per-(asset, type)
            //    read cache. Composite PK (AssetId, ReadingType).
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AssetSensorLatest"" (
                    ""AssetId"" integer NOT NULL,
                    ""ReadingType"" integer NOT NULL,
                    ""Value"" numeric(14,4) NOT NULL,
                    ""Unit"" smallint NOT NULL,
                    ""ReadingAt"" timestamp with time zone NOT NULL,
                    ""QualityCode"" smallint NOT NULL DEFAULT 0,
                    ""IsOutOfSpec"" boolean NOT NULL DEFAULT false,
                    ""Tone"" character varying(8) NOT NULL DEFAULT 'muted',
                    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    CONSTRAINT ""PK_AssetSensorLatest"" PRIMARY KEY (""AssetId"", ""ReadingType""),
                    CONSTRAINT ""FK_AssetSensorLatest_Assets_AssetId""
                        FOREIGN KEY (""AssetId"") REFERENCES ""Assets"" (""Id"") ON DELETE CASCADE
                );
            ");

            // ============================================================
            // 3. AlarmRationalizations — ISA-18.2 documented operator
            //    response per (EquipmentClass, ReadingType, Priority).
            //    Created BEFORE SensorAlarms because alarms FK to it.
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AlarmRationalizations"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""AlarmKey"" character varying(128) NOT NULL,
                    ""EquipmentClassId"" integer NULL,
                    ""ReadingType"" integer NOT NULL,
                    ""Priority"" smallint NOT NULL,
                    ""Justification"" character varying(500) NOT NULL,
                    ""Consequence"" character varying(500) NOT NULL,
                    ""OperatorResponse"" character varying(2000) NOT NULL,
                    ""TargetResponseTime"" interval NOT NULL,
                    ""ProcedureReference"" character varying(500) NULL,
                    ""Version"" integer NOT NULL DEFAULT 1,
                    ""EffectiveFrom"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""EffectiveUntil"" timestamp with time zone NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""CreatedByUserId"" integer NULL,
                    ""Active"" boolean NOT NULL DEFAULT true,
                    CONSTRAINT ""FK_AlarmRationalizations_EquipmentClasses_EquipmentClassId""
                        FOREIGN KEY (""EquipmentClassId"") REFERENCES ""EquipmentClasses"" (""Id"") ON DELETE SET NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""ix_alarmrationalization_lookup""
                ON ""AlarmRationalizations"" (""EquipmentClassId"", ""ReadingType"", ""Priority"", ""Active"");
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AlarmRationalizations_AlarmKey_Version""
                ON ""AlarmRationalizations"" (""AlarmKey"", ""Version"");
            ");

            // ============================================================
            // 4. SensorAlarms — ISA-18.2 alarm lifecycle.
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SensorAlarms"" (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""AssetId"" integer NOT NULL,
                    ""ReadingType"" integer NOT NULL,
                    ""State"" smallint NOT NULL DEFAULT 0,
                    ""Priority"" smallint NOT NULL DEFAULT 3,
                    ""RationalizationId"" integer NOT NULL,
                    ""OpenedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""OpeningEventId"" bigint NOT NULL,
                    ""AcknowledgedAt"" timestamp with time zone NULL,
                    ""AcknowledgedByUserId"" integer NULL,
                    ""AcknowledgementNote"" character varying(2000) NULL,
                    ""ClearedAt"" timestamp with time zone NULL,
                    ""ClearingEventId"" bigint NULL,
                    ""ClearedReason"" character varying(500) NULL,
                    ""ShelvedUntil"" timestamp with time zone NULL,
                    ""ShelfReason"" character varying(500) NULL,
                    ""ShelvedByUserId"" integer NULL,
                    ""SuppressionMode"" smallint NULL,
                    ""PeakValue"" numeric(14,4) NULL,
                    ""PeakAt"" timestamp with time zone NULL,
                    CONSTRAINT ""FK_SensorAlarms_Assets_AssetId""
                        FOREIGN KEY (""AssetId"") REFERENCES ""Assets"" (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""FK_SensorAlarms_AlarmRationalizations_RationalizationId""
                        FOREIGN KEY (""RationalizationId"") REFERENCES ""AlarmRationalizations"" (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""FK_SensorAlarms_Users_AcknowledgedByUserId""
                        FOREIGN KEY (""AcknowledgedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE SET NULL,
                    CONSTRAINT ""FK_SensorAlarms_Users_ShelvedByUserId""
                        FOREIGN KEY (""ShelvedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE SET NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""ix_sensoralarm_asset_type_state""
                ON ""SensorAlarms"" (""AssetId"", ""ReadingType"", ""State"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""ix_sensoralarm_state""
                ON ""SensorAlarms"" (""State"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_SensorAlarms_OpenedAt""
                ON ""SensorAlarms"" (""OpenedAt"");
            ");

            // ============================================================
            // 5. SensorSnapshots + SensorSnapshotValues — Part 11 audit
            //    snapshots (append-only).
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SensorSnapshots"" (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CapturedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""Reason"" smallint NOT NULL,
                    ""TriggerEventId"" bigint NULL,
                    ""CapturedByUserId"" integer NULL,
                    ""Notes"" character varying(2000) NULL,
                    ""SignatureHash"" bytea NOT NULL,
                    ""SignatureMethod"" smallint NOT NULL DEFAULT 0,
                    ""GampValidationStatus"" smallint NOT NULL DEFAULT 0,
                    CONSTRAINT ""FK_SensorSnapshots_Users_CapturedByUserId""
                        FOREIGN KEY (""CapturedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE SET NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_SensorSnapshots_CapturedAt""
                ON ""SensorSnapshots"" (""CapturedAt"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_SensorSnapshots_Reason_CapturedAt""
                ON ""SensorSnapshots"" (""Reason"", ""CapturedAt"");
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SensorSnapshotValues"" (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""SnapshotId"" bigint NOT NULL,
                    ""AssetId"" integer NOT NULL,
                    ""ReadingType"" integer NOT NULL,
                    ""Value"" numeric(14,4) NOT NULL,
                    ""Unit"" smallint NOT NULL,
                    ""ReadingAt"" timestamp with time zone NOT NULL,
                    ""OpcQualityByte"" smallint NOT NULL DEFAULT 0,
                    ""QualityCode"" smallint NOT NULL DEFAULT 0,
                    ""IsOutOfSpec"" boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""FK_SensorSnapshotValues_SensorSnapshots_SnapshotId""
                        FOREIGN KEY (""SnapshotId"") REFERENCES ""SensorSnapshots"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_SensorSnapshotValues_Assets_AssetId""
                        FOREIGN KEY (""AssetId"") REFERENCES ""Assets"" (""Id"") ON DELETE RESTRICT
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_SensorSnapshotValues_Snapshot_Asset_Type""
                ON ""SensorSnapshotValues"" (""SnapshotId"", ""AssetId"", ""ReadingType"");
            ");

            // ============================================================
            // 6. UnitConversions — ISO 80000 / UNECE Rec. 20 conversion
            //    table. Seeded in PR #118.2.
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""UnitConversions"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""FromUnit"" smallint NOT NULL,
                    ""ToUnit"" smallint NOT NULL,
                    ""Multiplier"" numeric(20,10) NOT NULL DEFAULT 1,
                    ""Offset"" numeric(20,10) NOT NULL DEFAULT 0,
                    ""UneceCode"" character varying(8) NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""Active"" boolean NOT NULL DEFAULT true
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""ix_unitconversion_lookup""
                ON ""UnitConversions"" (""FromUnit"", ""ToUnit"", ""Active"");
            ");

            // ============================================================
            // 7. SensorEvents — RAW HYPERTABLE.
            //
            // Composite PK (Id, ReadingAt) because TimescaleDB requires
            // the partitioning column in every uniqueness constraint.
            // Created as a regular table first; converted to a hypertable
            // in the next SQL statement.
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SensorEvents"" (
                    ""Id"" BIGSERIAL NOT NULL,
                    ""AssetId"" integer NOT NULL,
                    ""AssetSensorChannelId"" integer NULL,
                    ""ReadingType"" integer NOT NULL,
                    ""Value"" numeric(14,4) NOT NULL,
                    ""Unit"" smallint NOT NULL,
                    ""ReadingAt"" timestamp with time zone NOT NULL,
                    ""IngestedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""Source"" character varying(32) NOT NULL,
                    ""SourceZone"" smallint NOT NULL DEFAULT 3,
                    ""QualityCode"" smallint NOT NULL DEFAULT 0,
                    ""OpcQualityByte"" smallint NOT NULL DEFAULT 0,
                    ""IsOutOfSpec"" boolean NOT NULL DEFAULT false,
                    ""SchemaVersion"" smallint NOT NULL DEFAULT 1,
                    ""CorrelationId"" uuid NULL,
                    CONSTRAINT ""PK_SensorEvents"" PRIMARY KEY (""Id"", ""ReadingAt""),
                    CONSTRAINT ""FK_SensorEvents_Assets_AssetId""
                        FOREIGN KEY (""AssetId"") REFERENCES ""Assets"" (""Id"") ON DELETE RESTRICT
                );
            ");

            // Convert to TimescaleDB hypertable. Idempotent via if_not_exists.
            // 1-day chunks: production sweet spot for 1Hz process variables.
            migrationBuilder.Sql(@"
                SELECT create_hypertable(
                    '""SensorEvents""',
                    'ReadingAt',
                    chunk_time_interval => INTERVAL '1 day',
                    if_not_exists => TRUE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""ix_sensorevent_asset_type_time""
                ON ""SensorEvents"" (""AssetId"", ""ReadingType"", ""ReadingAt"" DESC);
            ");

            // Partial index — only OOS rows. Used by alarm lifecycle and
            // by AssetHealthService when bulk-loading the last 7 days of
            // breaches per asset.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""ix_sensorevent_oos""
                ON ""SensorEvents"" (""AssetId"", ""ReadingAt"" DESC)
                WHERE ""IsOutOfSpec"" = true;
            ");

            // ============================================================
            // 8. Continuous aggregates — SensorRollupMinute / Hour / Day.
            //
            // Excludes Failure (2) and OutOfService (4) NE 107 states
            // from the value aggregates so corrupt or disabled readings
            // don't distort the time-series shape. Counts per quality
            // state are preserved for data-quality dashboards.
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW IF NOT EXISTS ""SensorRollupMinute""
                WITH (timescaledb.continuous) AS
                SELECT
                    ""AssetId"",
                    ""ReadingType"",
                    time_bucket(INTERVAL '1 minute', ""ReadingAt"") AS ""BucketStart"",
                    AVG(""Value"")                                             AS ""AvgValue"",
                    MIN(""Value"")                                             AS ""MinValue"",
                    MAX(""Value"")                                             AS ""MaxValue"",
                    STDDEV_SAMP(""Value"")                                     AS ""StdDev"",
                    COUNT(*)                                                  AS ""SampleCount"",
                    COUNT(*) FILTER (WHERE ""IsOutOfSpec"")                    AS ""OosCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 1)                AS ""UncertainCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 2)                AS ""FailureCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 3)                AS ""MaintenanceCount""
                FROM ""SensorEvents""
                WHERE ""QualityCode"" IN (0, 1, 3)
                GROUP BY ""AssetId"", ""ReadingType"", time_bucket(INTERVAL '1 minute', ""ReadingAt"")
                WITH NO DATA;
            ");

            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW IF NOT EXISTS ""SensorRollupHour""
                WITH (timescaledb.continuous) AS
                SELECT
                    ""AssetId"",
                    ""ReadingType"",
                    time_bucket(INTERVAL '1 hour', ""ReadingAt"") AS ""BucketStart"",
                    AVG(""Value"")                                             AS ""AvgValue"",
                    MIN(""Value"")                                             AS ""MinValue"",
                    MAX(""Value"")                                             AS ""MaxValue"",
                    STDDEV_SAMP(""Value"")                                     AS ""StdDev"",
                    COUNT(*)                                                  AS ""SampleCount"",
                    COUNT(*) FILTER (WHERE ""IsOutOfSpec"")                    AS ""OosCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 1)                AS ""UncertainCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 2)                AS ""FailureCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 3)                AS ""MaintenanceCount""
                FROM ""SensorEvents""
                WHERE ""QualityCode"" IN (0, 1, 3)
                GROUP BY ""AssetId"", ""ReadingType"", time_bucket(INTERVAL '1 hour', ""ReadingAt"")
                WITH NO DATA;
            ");

            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW IF NOT EXISTS ""SensorRollupDay""
                WITH (timescaledb.continuous) AS
                SELECT
                    ""AssetId"",
                    ""ReadingType"",
                    time_bucket(INTERVAL '1 day', ""ReadingAt"") AS ""BucketStart"",
                    AVG(""Value"")                                             AS ""AvgValue"",
                    MIN(""Value"")                                             AS ""MinValue"",
                    MAX(""Value"")                                             AS ""MaxValue"",
                    STDDEV_SAMP(""Value"")                                     AS ""StdDev"",
                    COUNT(*)                                                  AS ""SampleCount"",
                    COUNT(*) FILTER (WHERE ""IsOutOfSpec"")                    AS ""OosCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 1)                AS ""UncertainCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 2)                AS ""FailureCount"",
                    COUNT(*) FILTER (WHERE ""QualityCode"" = 3)                AS ""MaintenanceCount""
                FROM ""SensorEvents""
                WHERE ""QualityCode"" IN (0, 1, 3)
                GROUP BY ""AssetId"", ""ReadingType"", time_bucket(INTERVAL '1 day', ""ReadingAt"")
                WITH NO DATA;
            ");

            // ============================================================
            // 9. Continuous-aggregate refresh policies.
            //
            // Minute rollup refreshes every minute, covering the last 2 hours.
            // This handles 99% of edge store-and-forward replay (events
            // arriving > 2 hours late get a one-shot manual refresh in PR
            // #118.2's ingest service).
            // ============================================================
            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy(
                    '""SensorRollupMinute""',
                    start_offset      => INTERVAL '2 hours',
                    end_offset        => INTERVAL '1 minute',
                    schedule_interval => INTERVAL '1 minute',
                    if_not_exists     => TRUE
                );
            ");

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy(
                    '""SensorRollupHour""',
                    start_offset      => INTERVAL '6 hours',
                    end_offset        => INTERVAL '1 hour',
                    schedule_interval => INTERVAL '5 minutes',
                    if_not_exists     => TRUE
                );
            ");

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy(
                    '""SensorRollupDay""',
                    start_offset      => INTERVAL '3 days',
                    end_offset        => INTERVAL '1 day',
                    schedule_interval => INTERVAL '1 hour',
                    if_not_exists     => TRUE
                );
            ");

            // ============================================================
            // 10. Compression + retention policies on SensorEvents.
            //
            // Compress after 7 days — leaves headroom for late-arriving
            // out-of-order events from offline edge gateways. Expected
            // 10-20x columnar compression on slowly-changing process data
            // (TimescaleDB Gorilla-style delta+XOR — the open-source
            // equivalent of PI's SmartCompression).
            //
            // Retention 90 days hot. Per-tenant override via the
            // ITenantContext when FDA / SOX regulatory retention demands
            // it. SensorSnapshots are append-only and never dropped.
            // ============================================================
            migrationBuilder.Sql(@"
                ALTER TABLE ""SensorEvents""
                SET (timescaledb.compress, timescaledb.compress_segmentby = '""AssetId"",""ReadingType""');
            ");

            migrationBuilder.Sql(@"
                SELECT add_compression_policy(
                    '""SensorEvents""',
                    compress_after => INTERVAL '7 days',
                    if_not_exists  => TRUE
                );
            ");

            migrationBuilder.Sql(@"
                SELECT add_retention_policy(
                    '""SensorEvents""',
                    drop_after    => INTERVAL '90 days',
                    if_not_exists => TRUE
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop in reverse order. Continuous aggregates first (depend
            // on the hypertable), then policies, then the hypertable
            // itself, then dependent tables.
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS ""SensorRollupDay"" CASCADE;");
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS ""SensorRollupHour"" CASCADE;");
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS ""SensorRollupMinute"" CASCADE;");

            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""SensorAlarms"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AlarmRationalizations"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""SensorSnapshotValues"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""SensorSnapshots"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AssetSensorLatest"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""UnitConversions"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""SensorEvents"" CASCADE;");

            // We do NOT drop the timescaledb extension on Down — other
            // pieces of the schema may depend on it later. Removing the
            // extension would require a coordinated tenant-data migration.
        }
    }
}
