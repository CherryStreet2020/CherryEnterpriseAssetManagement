using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 2 PR #117.1 — Real sensor history table.
    //
    // Per Dean's correction on PR #117: "DO NOT HARDCODE DATA, CREATE A
    // TABLE AND SEED IT. IT ALWAYS CAUSES ISSUES DOWN THE ROAD."
    // Right. The original PR populated the denormalized Asset.Current*
    // cache columns directly with no history backing. This table is the
    // source of truth. Cache columns become a derived view written by
    // AssetSensorService on each insert.
    //
    // Schema:
    //   - AssetId FK to Assets, cascading on delete (history follows
    //     the asset out the door)
    //   - ReadingType enum (Temperature/Vibration/Pressure/Current/Speed/Flow/Power)
    //   - Value numeric(12,4) — covers PSI ≤ 99,999,999 with 4-decimal
    //     precision for fine sensors
    //   - ReadingAt timestamp with index
    //   - Source — "demo", "iot:{deviceId}", "manual", etc.
    //   - IsOutOfSpec — fast filter for AssetHealthService breach counts
    //
    // Indexes:
    //   - Composite (AssetId, ReadingType, ReadingAt DESC) for the
    //     "latest reading per type per asset" Plant Floor query
    //   - Single (ReadingAt) for retention-prune scans
    //   - Single (IsOutOfSpec) partial — would be ideal but we keep
    //     it simple; AssetHealthService's window-scoped query is fast
    //     enough at demo scale.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_AddAssetSensorReadings")]
    public partial class AddAssetSensorReadings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AssetSensorReadings"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""AssetId"" integer NOT NULL,
                    ""ReadingType"" integer NOT NULL,
                    ""Value"" numeric(12,4) NOT NULL,
                    ""Unit"" character varying(20) NOT NULL,
                    ""ReadingAt"" timestamp with time zone NOT NULL,
                    ""Source"" character varying(100) NOT NULL DEFAULT 'demo',
                    ""IsOutOfSpec"" boolean NOT NULL DEFAULT false,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_AssetSensorReadings_Asset_Type_At""
                ON ""AssetSensorReadings"" (""AssetId"", ""ReadingType"", ""ReadingAt"" DESC);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_AssetSensorReadings_ReadingAt""
                ON ""AssetSensorReadings"" (""ReadingAt"");
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""AssetSensorReadings""
                ADD CONSTRAINT ""FK_AssetSensorReadings_Asset""
                FOREIGN KEY (""AssetId"") REFERENCES ""Assets""(""Id"")
                ON DELETE CASCADE;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AssetSensorReadings"";");
        }
    }
}
