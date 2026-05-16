using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 2 PR #117.2 — Equipment Catalog tables.
    //
    // Per Dean's direction "Best in Class Process to Produce a Best In Class
    // product." The catalog is curated in EQUIPMENT_CATALOG.md, seeded into
    // these three tables on startup, and read by IndustrialAssetSeeder and
    // Plant Floor. No more hardcoded brand+type pairings; no more universal
    // Temp/Vib/PSI sensor regime on every asset class.
    //
    // Schema:
    //   EquipmentClasses    — the category (CNC_MACHINING_CENTER, etc.)
    //   EquipmentModels     — Mfr + Model + photo + manual + cost (FK -> Class)
    //   SensorProfiles      — per-class sensor definitions (FK -> Class)
    //
    // Idempotent CREATE TABLE IF NOT EXISTS + UNIQUE indexes so the
    // migration is safe to re-run during the Replit pull cycle.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_AddEquipmentCatalog")]
    public partial class AddEquipmentCatalog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----- EquipmentClasses -----
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""EquipmentClasses"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Code"" character varying(64) NOT NULL,
                    ""Name"" character varying(120) NOT NULL,
                    ""Description"" character varying(500) NULL,
                    ""Category"" character varying(60) NOT NULL,
                    ""IconCode"" character varying(40) NULL,
                    ""DisplayOrder"" integer NOT NULL DEFAULT 100,
                    ""Active"" boolean NOT NULL DEFAULT true,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EquipmentClasses_Code""
                ON ""EquipmentClasses"" (""Code"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_EquipmentClasses_Category""
                ON ""EquipmentClasses"" (""Category"");
            ");

            // ----- EquipmentModels -----
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""EquipmentModels"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""EquipmentClassId"" integer NOT NULL,
                    ""Manufacturer"" character varying(120) NOT NULL,
                    ""ModelNumber"" character varying(120) NOT NULL,
                    ""DisplayName"" character varying(180) NULL,
                    ""ProductPageUrl"" character varying(500) NULL,
                    ""ImageUrl"" character varying(500) NULL,
                    ""MaintenanceManualUrl"" character varying(500) NULL,
                    ""TypicalAcquisitionCost"" numeric(14,2) NULL,
                    ""ServiceLifeYears"" integer NULL,
                    ""Notes"" character varying(1000) NULL,
                    ""Weight"" integer NOT NULL DEFAULT 1,
                    ""Active"" boolean NOT NULL DEFAULT true,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_EquipmentModels_EquipmentClassId""
                ON ""EquipmentModels"" (""EquipmentClassId"");
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EquipmentModels_Mfr_Model""
                ON ""EquipmentModels"" (""Manufacturer"", ""ModelNumber"");
            ");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_EquipmentModels_EquipmentClass'
                    ) THEN
                        ALTER TABLE ""EquipmentModels""
                        ADD CONSTRAINT ""FK_EquipmentModels_EquipmentClass""
                        FOREIGN KEY (""EquipmentClassId"") REFERENCES ""EquipmentClasses""(""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END $$;
            ");

            // ----- SensorProfiles -----
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SensorProfiles"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""EquipmentClassId"" integer NOT NULL,
                    ""SensorName"" character varying(80) NOT NULL,
                    ""ReadingType"" integer NOT NULL,
                    ""Unit"" character varying(20) NOT NULL,
                    ""NormalMin"" numeric(14,4) NOT NULL,
                    ""NormalMax"" numeric(14,4) NOT NULL,
                    ""WarningThreshold"" numeric(14,4) NULL,
                    ""CriticalThreshold"" numeric(14,4) NULL,
                    ""BreachOnHighSide"" boolean NOT NULL DEFAULT true,
                    ""SampleRateMinutes"" integer NOT NULL DEFAULT 60,
                    ""IsPrimary"" boolean NOT NULL DEFAULT false,
                    ""DisplayOrder"" integer NOT NULL DEFAULT 100,
                    ""Notes"" character varying(500) NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_SensorProfiles_EquipmentClassId""
                ON ""SensorProfiles"" (""EquipmentClassId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_SensorProfiles_Class_Reading""
                ON ""SensorProfiles"" (""EquipmentClassId"", ""ReadingType"");
            ");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_SensorProfiles_EquipmentClass'
                    ) THEN
                        ALTER TABLE ""SensorProfiles""
                        ADD CONSTRAINT ""FK_SensorProfiles_EquipmentClass""
                        FOREIGN KEY (""EquipmentClassId"") REFERENCES ""EquipmentClasses""(""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""SensorProfiles"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""EquipmentModels"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""EquipmentClasses"";");
        }
    }
}
