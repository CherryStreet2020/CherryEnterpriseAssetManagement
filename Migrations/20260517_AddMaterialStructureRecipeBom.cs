using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-013 / PR #119.14 — Phase E.3 polymorphic MaterialStructure +
    // Recipe content + RegulatoryProfile config.
    //
    // Tables created (6):
    //   - MaterialStructures   — polymorphic parent
    //   - Boms                 — Bom subtype (1:0..1 via UNIQUE)
    //   - Recipes              — Recipe subtype (1:0..1 via UNIQUE) —
    //                            FINALLY wires the RecipeRevision stub
    //                            from PR #119.13a into real content
    //   - MaterialStructureLines — shared lines table with LineKind enum
    //   - RecipePhases         — phases child of Recipe
    //   - RegulatoryProfiles   — config table for FDA / AS9100 / NADCAP /
    //                            REACH gating
    //
    // Columns added to existing ProductionOrders:
    //   - MaterialStructureId  int? NULL, FK SET NULL — which BOM/Recipe
    //                          is this order producing?
    //
    // Reference: ADR-013 §"Recommendation" item 3 — "polymorphic
    // MaterialStructure with two concrete subtypes (Bom, Recipe), both
    // pointing at a shared MaterialStructureLine with a LineKind enum
    // (component, co-product, by-product, scrap, packaging)."
    //
    // Idempotent throughout — CREATE IF NOT EXISTS, ADD COLUMN IF NOT
    // EXISTS, constraints in DO blocks with pg_constraint guards.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddMaterialStructureRecipeBom")]
    public partial class AddMaterialStructureRecipeBom : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1) RegulatoryProfiles ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""RegulatoryProfiles"" (
                    ""Id""                       SERIAL PRIMARY KEY,
                    ""Name""                     varchar(64)  NOT NULL,
                    ""Regime""                   smallint     NOT NULL DEFAULT 0,
                    ""Description""              varchar(500) NULL,
                    ""IsExternalRegime""         boolean      NOT NULL DEFAULT true,
                    ""MinimumRetentionYears""    integer      NULL,
                    ""IsActive""                 boolean      NOT NULL DEFAULT true,
                    ""Gates""                    jsonb        NULL,
                    ""CreatedAt""                timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""                varchar(100) NULL,
                    ""ModifiedAt""               timestamptz  NULL,
                    ""ModifiedBy""               varchar(100) NULL
                );
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RegulatoryProfiles_Name""
                ON ""RegulatoryProfiles"" (""Name"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_RegulatoryProfiles_Regime""
                ON ""RegulatoryProfiles"" (""Regime"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_RegulatoryProfiles_IsActive""
                ON ""RegulatoryProfiles"" (""IsActive"");
            ");

            // ---------- 2) MaterialStructures (polymorphic parent) ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""MaterialStructures"" (
                    ""Id""                       SERIAL PRIMARY KEY,
                    ""StructureNumber""          varchar(64)  NOT NULL,
                    ""Name""                     varchar(200) NOT NULL,
                    ""StructureType""            smallint     NOT NULL DEFAULT 0,
                    ""Status""                   smallint     NOT NULL DEFAULT 0,
                    ""Revision""                 varchar(16)  NULL,
                    ""OutputItemId""             integer      NULL,
                    ""MasterStructureId""        integer      NULL,
                    ""RegulatoryProfileId""      integer      NULL,
                    ""IsControlled""             boolean      NOT NULL DEFAULT false,
                    ""ControlledDocumentUrl""    varchar(500) NULL,
                    ""ApprovedAt""               timestamptz  NULL,
                    ""ApprovedBy""               varchar(100) NULL,
                    ""Notes""                    varchar(2000) NULL,
                    ""CreatedAt""                timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""                varchar(100) NULL,
                    ""ModifiedAt""               timestamptz  NULL,
                    ""ModifiedBy""               varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MaterialStructures_Items_OutputItemId') THEN
                        ALTER TABLE ""MaterialStructures""
                        ADD CONSTRAINT ""FK_MaterialStructures_Items_OutputItemId""
                        FOREIGN KEY (""OutputItemId"") REFERENCES ""Items""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MaterialStructures_MaterialStructures_MasterStructureId') THEN
                        ALTER TABLE ""MaterialStructures""
                        ADD CONSTRAINT ""FK_MaterialStructures_MaterialStructures_MasterStructureId""
                        FOREIGN KEY (""MasterStructureId"") REFERENCES ""MaterialStructures""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MaterialStructures_RegulatoryProfiles_RegulatoryProfileId') THEN
                        ALTER TABLE ""MaterialStructures""
                        ADD CONSTRAINT ""FK_MaterialStructures_RegulatoryProfiles_RegulatoryProfileId""
                        FOREIGN KEY (""RegulatoryProfileId"") REFERENCES ""RegulatoryProfiles""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialStructures_StructureNumber""
                ON ""MaterialStructures"" (""StructureNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialStructures_StructureType""
                ON ""MaterialStructures"" (""StructureType"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialStructures_Status""
                ON ""MaterialStructures"" (""Status"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialStructures_OutputItemId""
                ON ""MaterialStructures"" (""OutputItemId"");
            ");

            // ---------- 3) Boms ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Boms"" (
                    ""Id""                       SERIAL PRIMARY KEY,
                    ""MaterialStructureId""      integer      NOT NULL,
                    ""BomType""                  smallint     NOT NULL DEFAULT 0,
                    ""IsPhantom""                boolean      NOT NULL DEFAULT false,
                    ""TotalWeightKg""            numeric(12,4) NULL,
                    ""LeadTimeDays""             integer      NULL,
                    ""YieldPercent""             numeric(5,2) NULL,
                    ""CreatedAt""                timestamptz  NOT NULL DEFAULT now(),
                    ""UpdatedAt""                timestamptz  NULL
                );
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Boms_MaterialStructures_MaterialStructureId') THEN
                        ALTER TABLE ""Boms""
                        ADD CONSTRAINT ""FK_Boms_MaterialStructures_MaterialStructureId""
                        FOREIGN KEY (""MaterialStructureId"") REFERENCES ""MaterialStructures""(""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Boms_MaterialStructureId""
                ON ""Boms"" (""MaterialStructureId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Boms_BomType""
                ON ""Boms"" (""BomType"");
            ");

            // ---------- 4) Recipes ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Recipes"" (
                    ""Id""                       SERIAL PRIMARY KEY,
                    ""MaterialStructureId""      integer      NOT NULL,
                    ""RecipeRevisionId""         integer      NULL,
                    ""ScalingMode""              smallint     NOT NULL DEFAULT 0,
                    ""StandardBatchSize""        numeric(18,4) NULL,
                    ""BatchUom""                 varchar(16)  NULL,
                    ""YieldPercent""             numeric(5,2) NULL,
                    ""IntermediateItemId""       integer      NULL,
                    ""TotalDurationMinutes""     integer      NULL,
                    ""CreatedAt""                timestamptz  NOT NULL DEFAULT now(),
                    ""UpdatedAt""                timestamptz  NULL
                );
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Recipes_MaterialStructures_MaterialStructureId') THEN
                        ALTER TABLE ""Recipes""
                        ADD CONSTRAINT ""FK_Recipes_MaterialStructures_MaterialStructureId""
                        FOREIGN KEY (""MaterialStructureId"") REFERENCES ""MaterialStructures""(""Id"")
                        ON DELETE CASCADE;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Recipes_RecipeRevisions_RecipeRevisionId') THEN
                        ALTER TABLE ""Recipes""
                        ADD CONSTRAINT ""FK_Recipes_RecipeRevisions_RecipeRevisionId""
                        FOREIGN KEY (""RecipeRevisionId"") REFERENCES ""RecipeRevisions""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Recipes_Items_IntermediateItemId') THEN
                        ALTER TABLE ""Recipes""
                        ADD CONSTRAINT ""FK_Recipes_Items_IntermediateItemId""
                        FOREIGN KEY (""IntermediateItemId"") REFERENCES ""Items""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;
                END
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Recipes_MaterialStructureId""
                ON ""Recipes"" (""MaterialStructureId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Recipes_RecipeRevisionId""
                ON ""Recipes"" (""RecipeRevisionId"");
            ");

            // ---------- 5) MaterialStructureLines ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""MaterialStructureLines"" (
                    ""Id""                          SERIAL PRIMARY KEY,
                    ""MaterialStructureId""         integer      NOT NULL,
                    ""ItemId""                      integer      NOT NULL,
                    ""LineKind""                    smallint     NOT NULL DEFAULT 0,
                    ""Sequence""                    integer      NOT NULL DEFAULT 10,
                    ""Quantity""                    numeric(18,4) NOT NULL DEFAULT 0,
                    ""Uom""                         varchar(16)  NULL,
                    ""ScrapPercent""                numeric(5,2) NULL,
                    ""PhaseSequence""               integer      NULL,
                    ""TypeSpecificProperties""      jsonb        NULL,
                    ""Notes""                       varchar(500) NULL,
                    ""CreatedAt""                   timestamptz  NOT NULL DEFAULT now()
                );
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MaterialStructureLines_MaterialStructures_MaterialStructureId') THEN
                        ALTER TABLE ""MaterialStructureLines""
                        ADD CONSTRAINT ""FK_MaterialStructureLines_MaterialStructures_MaterialStructureId""
                        FOREIGN KEY (""MaterialStructureId"") REFERENCES ""MaterialStructures""(""Id"")
                        ON DELETE CASCADE;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MaterialStructureLines_Items_ItemId') THEN
                        ALTER TABLE ""MaterialStructureLines""
                        ADD CONSTRAINT ""FK_MaterialStructureLines_Items_ItemId""
                        FOREIGN KEY (""ItemId"") REFERENCES ""Items""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;
                END
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialStructureLines_StructureId_Sequence""
                ON ""MaterialStructureLines"" (""MaterialStructureId"", ""Sequence"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialStructureLines_LineKind""
                ON ""MaterialStructureLines"" (""LineKind"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialStructureLines_ItemId""
                ON ""MaterialStructureLines"" (""ItemId"");
            ");

            // ---------- 6) RecipePhases ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""RecipePhases"" (
                    ""Id""                       SERIAL PRIMARY KEY,
                    ""RecipeId""                 integer      NOT NULL,
                    ""Sequence""                 integer      NOT NULL,
                    ""Name""                     varchar(100) NOT NULL,
                    ""Description""              varchar(500) NULL,
                    ""DurationMinutes""          integer      NULL,
                    ""SetpointTempC""            numeric(8,2) NULL,
                    ""TempToleranceC""           numeric(8,2) NULL,
                    ""AtmosphereType""           varchar(32)  NULL,
                    ""AgitationSpec""            varchar(32)  NULL,
                    ""PressurePsi""              numeric(8,2) NULL,
                    ""RequiredEquipmentClass""   varchar(64)  NULL,
                    ""HasQualityHold""           boolean      NOT NULL DEFAULT false,
                    ""OperatorInstructions""     varchar(2000) NULL,
                    ""CreatedAt""                timestamptz  NOT NULL DEFAULT now(),
                    ""UpdatedAt""                timestamptz  NULL
                );
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_RecipePhases_Recipes_RecipeId') THEN
                        ALTER TABLE ""RecipePhases""
                        ADD CONSTRAINT ""FK_RecipePhases_Recipes_RecipeId""
                        FOREIGN KEY (""RecipeId"") REFERENCES ""Recipes""(""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RecipePhases_RecipeId_Sequence""
                ON ""RecipePhases"" (""RecipeId"", ""Sequence"");
            ");

            // ---------- 7) ProductionOrder.MaterialStructureId column + FK ----------
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProductionOrders""
                ADD COLUMN IF NOT EXISTS ""MaterialStructureId"" integer NULL;
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionOrders_MaterialStructures_MaterialStructureId') THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT ""FK_ProductionOrders_MaterialStructures_MaterialStructureId""
                        FOREIGN KEY (""MaterialStructureId"") REFERENCES ""MaterialStructures""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOrders_MaterialStructureId""
                ON ""ProductionOrders"" (""MaterialStructureId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 7) ProductionOrders rollback
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_MaterialStructureId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionOrders"" DROP CONSTRAINT IF EXISTS ""FK_ProductionOrders_MaterialStructures_MaterialStructureId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionOrders"" DROP COLUMN IF EXISTS ""MaterialStructureId"";");

            // 6) RecipePhases
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RecipePhases_RecipeId_Sequence"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RecipePhases"";");

            // 5) MaterialStructureLines
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructureLines_ItemId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructureLines_LineKind"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructureLines_StructureId_Sequence"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MaterialStructureLines"";");

            // 4) Recipes
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Recipes_RecipeRevisionId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Recipes_MaterialStructureId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Recipes"";");

            // 3) Boms
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Boms_BomType"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Boms_MaterialStructureId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Boms"";");

            // 2) MaterialStructures
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_OutputItemId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_StructureType"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_StructureNumber"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MaterialStructures"";");

            // 1) RegulatoryProfiles
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RegulatoryProfiles_IsActive"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RegulatoryProfiles_Regime"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RegulatoryProfiles_Name"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RegulatoryProfiles"";");
        }
    }
}
