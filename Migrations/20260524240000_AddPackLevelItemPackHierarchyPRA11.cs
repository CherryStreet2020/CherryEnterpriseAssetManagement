// =============================================================================
// Sprint 13.5 PRA-11 — PackLevel + ItemPackHierarchy.
// Master Files Baseline cascade ship #9 of 10 (last small one before PRA-5b).
//
// TWO NEW TABLES:
//   - PackLevels             named pack tiers (CompanyId nullable)
//                            5 system templates seeded
//   - ItemPackHierarchies    per-Item physical config at each tier
//                            (CompanyId NOT NULL — operational)
//
// SEEDS — 5 system PackLevel templates (all CompanyId IS NULL):
//   EACH    Each / Unit              LevelOrder=1
//   INNER   Inner Pack / Bundle      LevelOrder=2
//   CASE    Case / Shipper           LevelOrder=3
//   PALLET  Pallet                   LevelOrder=4
//   TRUCK   Truckload / Container    LevelOrder=5
//
// IDEMPOTENT — CREATE TABLE IF NOT EXISTS + INSERT WHERE NOT EXISTS.
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md §6.9
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
//   - GS1 General Specifications (packaging hierarchy)
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524240000_AddPackLevelItemPackHierarchyPRA11")]
    public partial class AddPackLevelItemPackHierarchyPRA11 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) PackLevels
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PackLevels"" (
                    ""Id""              serial          PRIMARY KEY,
                    ""CompanyId""       integer         NULL,
                    ""Code""            varchar(32)     NOT NULL,
                    ""Name""            varchar(100)    NOT NULL,
                    ""Description""     varchar(500)    NULL,
                    ""LevelOrder""      integer         NOT NULL DEFAULT 1,
                    ""DefaultUomId""    integer         NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE SET NULL,
                    ""IsActive""        boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""        boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""       integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""       varchar(100)    NULL,
                    ""ModifiedAt""      timestamptz     NULL,
                    ""ModifiedBy""      varchar(100)    NULL,
                    CONSTRAINT ck_pack_levels_order CHECK (""LevelOrder"" BETWEEN 1 AND 99)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_pack_levels_system_code
                    ON ""PackLevels"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_pack_levels_company_code
                    ON ""PackLevels"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_pack_levels_order
                    ON ""PackLevels"" (""LevelOrder"");
            ");

            // ================================================================
            // 2) ItemPackHierarchies
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ItemPackHierarchies"" (
                    ""Id""                  serial          PRIMARY KEY,
                    ""CompanyId""           integer         NOT NULL,
                    ""ItemId""              integer         NOT NULL,
                    ""PackLevelId""         integer         NOT NULL REFERENCES ""PackLevels""(""Id"") ON DELETE RESTRICT,
                    ""QtyOfBaseUnits""      numeric(18,4)   NOT NULL,
                    ""BaseUomId""           integer         NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE SET NULL,
                    ""LengthCm""            numeric(10,2)   NULL,
                    ""WidthCm""             numeric(10,2)   NULL,
                    ""HeightCm""            numeric(10,2)   NULL,
                    ""TareWeightKg""        numeric(10,4)   NULL,
                    ""GrossWeightKg""       numeric(10,4)   NULL,
                    ""Gtin""                varchar(14)     NULL,
                    ""Upc""                 varchar(12)     NULL,
                    ""Ean""                 varchar(13)     NULL,
                    ""Sscc""                varchar(18)     NULL,
                    ""IsDefault""           boolean         NOT NULL DEFAULT FALSE,
                    ""IsShippable""         boolean         NOT NULL DEFAULT TRUE,
                    ""IsRecyclable""        boolean         NOT NULL DEFAULT TRUE,
                    ""IsActive""            boolean         NOT NULL DEFAULT TRUE,
                    ""Notes""               varchar(500)    NULL,
                    ""CreatedAt""           timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""           varchar(100)    NULL,
                    ""ModifiedAt""          timestamptz     NULL,
                    ""ModifiedBy""          varchar(100)    NULL,
                    CONSTRAINT ck_item_pack_hierarchies_qty CHECK (""QtyOfBaseUnits"" > 0),
                    CONSTRAINT ck_item_pack_hierarchies_dim_length CHECK (""LengthCm"" IS NULL OR ""LengthCm"" > 0),
                    CONSTRAINT ck_item_pack_hierarchies_dim_width CHECK (""WidthCm"" IS NULL OR ""WidthCm"" > 0),
                    CONSTRAINT ck_item_pack_hierarchies_dim_height CHECK (""HeightCm"" IS NULL OR ""HeightCm"" > 0),
                    CONSTRAINT ck_item_pack_hierarchies_weight CHECK (""GrossWeightKg"" IS NULL OR ""TareWeightKg"" IS NULL OR ""GrossWeightKg"" >= ""TareWeightKg"")
                );

                CREATE INDEX IF NOT EXISTS ix_item_pack_hierarchies_company_item_level
                    ON ""ItemPackHierarchies"" (""CompanyId"", ""ItemId"", ""PackLevelId"");
                CREATE INDEX IF NOT EXISTS ix_item_pack_hierarchies_gtin
                    ON ""ItemPackHierarchies"" (""Gtin"") WHERE ""Gtin"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_item_pack_hierarchies_item
                    ON ""ItemPackHierarchies"" (""ItemId"");
            ");

            // ================================================================
            // 3) Seed 5 system PackLevels
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""PackLevels"" (""CompanyId"", ""Code"", ""Name"", ""Description"", ""LevelOrder"", ""DefaultUomId"", ""IsSystem"", ""SortOrder"")
                SELECT *
                FROM (VALUES
                    (NULL::int, 'EACH',   'Each / Unit',           'Individual sellable unit — the base tier (e.g. one bottle, one bar, one assembled part)',                                 1, (SELECT ""Id"" FROM ""UnitsOfMeasure"" WHERE ""Code""='EA' AND ""CompanyId"" IS NULL), TRUE, 10),
                    (NULL::int, 'INNER',  'Inner Pack / Bundle',   'Multi-unit bundle inside a case (e.g. a 6-pack of bottles inside a 24-pack case)',                                       2, NULL::int,                                                                              TRUE, 20),
                    (NULL::int, 'CASE',   'Case / Shipper',        'Standard outer shipping unit — typically 12/24/48 EA depending on item. Used for distribution + retail backstock',     3, NULL::int,                                                                              TRUE, 30),
                    (NULL::int, 'PALLET', 'Pallet',                'Standard ISO/US pallet — typically 40-80 cases depending on item dimensions. WMS/forklift unit.',                       4, NULL::int,                                                                              TRUE, 40),
                    (NULL::int, 'TRUCK',  'Truckload / Container', 'Full truck or sea container — typically 20-30 pallets. Trans-load + freight planning unit.',                            5, NULL::int,                                                                              TRUE, 50)
                ) AS v(""CompanyId"", ""Code"", ""Name"", ""Description"", ""LevelOrder"", ""DefaultUomId"", ""IsSystem"", ""SortOrder"")
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""PackLevels""
                    WHERE ""CompanyId"" IS NULL AND ""Code"" = v.""Code""
                );
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"
                DROP TABLE IF EXISTS ""ItemPackHierarchies"";
                DROP TABLE IF EXISTS ""PackLevels"";
            ");
        }
    }
}
