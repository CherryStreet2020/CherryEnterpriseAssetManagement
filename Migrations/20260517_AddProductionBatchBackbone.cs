using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-013 / PR #119.13a — Phase E.2 batch backbone.
    //
    // Lands the polymorphic ProductionBatch parent + Nest and ProcessBatch
    // subtypes + allocation table + equipment-link child + state-event
    // audit log + two stub FK target tables (RecipeRevision, MrbDisposition).
    //
    // Tables created (8 total):
    //   - ProductionBatches             — polymorphic parent
    //   - Nests                          — cutting subtype (1:0..1 via UNIQUE)
    //   - ProcessBatches                 — heat-treat / paint / plating
    //                                       subtype (1:0..1 via UNIQUE)
    //   - ProductionBatchAllocations     — cost-allocation rows
    //   - ProductionBatchEquipmentLinks  — multi-equipment child
    //   - ProductionBatchStateEvents     — append-only state audit
    //   - RecipeRevisions                — stub FK target (content schema
    //                                       lands in PR #119.14)
    //   - MrbDispositions                — stub FK target (full workflow
    //                                       lands in PR #119.13c)
    //
    // Columns added to existing WorkOrderOperations:
    //   - BatchPoolCode                  varchar(64) NULL
    //   - ProductionBatchId              int NULL, FK SET NULL
    //   - BatchSequenceNo                int NULL
    //
    // Schema cleanup of PR #119.12 placeholders on ProductionJobShopDetails:
    //   - DROP CutListId column (no longer needed — cut-list lookup goes
    //     via CutListLine.SourceProductionOrderId in PR #119.13b)
    //   - ADD FK NestPlanId -> Nests(Id) ON DELETE SET NULL
    //
    // What this PR does NOT do (PR #119.13b and later):
    //   - StockReceipts physical-lot table + Remnants + MaterialMasters
    //   - CutListLines table (lands in #119.13b with the traceability layer)
    //   - Full HeatTreatChart / TankChemistryReading / WitnessCoupon /
    //     PyrometryCal content tables
    //   - Recipe content schema (Recipe + RecipeStep + RecipeIngredient)
    //   - MaterialStructure polymorphic parent + Bom subtype
    //
    // Idempotent throughout — CREATE IF NOT EXISTS, ADD COLUMN IF NOT
    // EXISTS, constraint adds inside DO blocks with pg_constraint guards.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddProductionBatchBackbone")]
    public partial class AddProductionBatchBackbone : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1) Stub FK targets (RecipeRevisions, MrbDispositions) ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""RecipeRevisions"" (
                    ""Id""                     SERIAL PRIMARY KEY,
                    ""Name""                   varchar(64)  NOT NULL,
                    ""Version""                varchar(16)  NOT NULL DEFAULT 'A',
                    ""MasterRecipeId""         integer      NULL,
                    ""Status""                 smallint     NOT NULL DEFAULT 0,
                    ""IsControlled""           boolean      NOT NULL DEFAULT false,
                    ""ControlledDocumentUrl""  varchar(500) NULL,
                    ""CreatedAt""              timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""              varchar(100) NULL,
                    ""ApprovedAt""             timestamptz  NULL,
                    ""ApprovedBy""             varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_RecipeRevisions_RecipeRevisions_MasterRecipeId') THEN
                        ALTER TABLE ""RecipeRevisions""
                        ADD CONSTRAINT ""FK_RecipeRevisions_RecipeRevisions_MasterRecipeId""
                        FOREIGN KEY (""MasterRecipeId"") REFERENCES ""RecipeRevisions""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RecipeRevisions_Name_Version""
                ON ""RecipeRevisions"" (""Name"", ""Version"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_RecipeRevisions_Status""
                ON ""RecipeRevisions"" (""Status"");
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""MrbDispositions"" (
                    ""Id""                     SERIAL PRIMARY KEY,
                    ""DispositionNumber""      varchar(32)  NOT NULL,
                    ""Outcome""                smallint     NOT NULL DEFAULT 0,
                    ""Justification""          varchar(2000) NULL,
                    ""NonConformanceType""     varchar(64)  NULL,
                    ""ApprovedBy""             varchar(100) NULL,
                    ""ApprovedAt""             timestamptz  NULL,
                    ""EvidenceUrl""            varchar(500) NULL,
                    ""CreatedAt""              timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""              varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MrbDispositions_DispositionNumber""
                ON ""MrbDispositions"" (""DispositionNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MrbDispositions_Outcome""
                ON ""MrbDispositions"" (""Outcome"");
            ");

            // ---------- 2) ProductionBatch polymorphic parent ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProductionBatches"" (
                    ""Id""                          SERIAL PRIMARY KEY,
                    ""BatchNumber""                 varchar(32)  NOT NULL,
                    ""BatchType""                   smallint     NOT NULL DEFAULT 0,
                    ""Status""                      smallint     NOT NULL DEFAULT 0,
                    ""BatchPoolCode""               varchar(64)  NULL,
                    ""PrimaryEquipmentId""          integer      NULL,
                    ""RecipeRevisionId""            integer      NULL,
                    ""ScheduledStartAt""            timestamptz  NULL,
                    ""ActualStartAt""               timestamptz  NULL,
                    ""ActualEndAt""                 timestamptz  NULL,
                    ""OperatorUserId""              integer      NULL,
                    ""SupervisorUserId""            integer      NULL,
                    ""AllocationMethod""            smallint     NOT NULL DEFAULT 0,
                    ""TotalCost""                   numeric(18,4) NULL,
                    ""HoldReason""                  varchar(256) NULL,
                    ""QuarantineDispositionId""     integer      NULL,
                    ""Notes""                       varchar(2000) NULL,
                    ""CreatedAt""                   timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""                   varchar(100) NULL,
                    ""ModifiedAt""                  timestamptz  NULL,
                    ""ModifiedBy""                  varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatches_Assets_PrimaryEquipmentId') THEN
                        ALTER TABLE ""ProductionBatches""
                        ADD CONSTRAINT ""FK_ProductionBatches_Assets_PrimaryEquipmentId""
                        FOREIGN KEY (""PrimaryEquipmentId"") REFERENCES ""Assets""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatches_RecipeRevisions_RecipeRevisionId') THEN
                        ALTER TABLE ""ProductionBatches""
                        ADD CONSTRAINT ""FK_ProductionBatches_RecipeRevisions_RecipeRevisionId""
                        FOREIGN KEY (""RecipeRevisionId"") REFERENCES ""RecipeRevisions""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatches_MrbDispositions_QuarantineDispositionId') THEN
                        ALTER TABLE ""ProductionBatches""
                        ADD CONSTRAINT ""FK_ProductionBatches_MrbDispositions_QuarantineDispositionId""
                        FOREIGN KEY (""QuarantineDispositionId"") REFERENCES ""MrbDispositions""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionBatches_BatchNumber""
                ON ""ProductionBatches"" (""BatchNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatches_BatchType""
                ON ""ProductionBatches"" (""BatchType"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatches_Status""
                ON ""ProductionBatches"" (""Status"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatches_BatchPoolCode""
                ON ""ProductionBatches"" (""BatchPoolCode"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatches_ScheduledStartAt""
                ON ""ProductionBatches"" (""ScheduledStartAt"");
            ");

            // ---------- 3) Nests subtype ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Nests"" (
                    ""Id""                     SERIAL PRIMARY KEY,
                    ""ProductionBatchId""      integer      NOT NULL,
                    ""StockItemId""            integer      NULL,
                    ""DxfFileUrl""             varchar(500) NULL,
                    ""NestingSoftware""        varchar(64)  NULL,
                    ""SheetLengthMm""          numeric(10,2) NULL,
                    ""SheetWidthMm""           numeric(10,2) NULL,
                    ""SheetCount""             integer      NOT NULL DEFAULT 1,
                    ""Utilization""            numeric(5,4) NULL,
                    ""RevisionNumber""         integer      NOT NULL DEFAULT 1,
                    ""PiecesPlanned""          integer      NOT NULL DEFAULT 0,
                    ""PiecesCut""              integer      NULL,
                    ""PierceCount""            integer      NULL,
                    ""CutPathLengthMm""        numeric(12,2) NULL,
                    ""CuttingTimeSeconds""     integer      NULL,
                    ""CutByUserId""            integer      NULL,
                    ""CreatedAt""              timestamptz  NOT NULL DEFAULT now(),
                    ""UpdatedAt""              timestamptz  NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Nests_ProductionBatches_ProductionBatchId') THEN
                        ALTER TABLE ""Nests""
                        ADD CONSTRAINT ""FK_Nests_ProductionBatches_ProductionBatchId""
                        FOREIGN KEY (""ProductionBatchId"") REFERENCES ""ProductionBatches""(""Id"")
                        ON DELETE CASCADE;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Nests_Items_StockItemId') THEN
                        ALTER TABLE ""Nests""
                        ADD CONSTRAINT ""FK_Nests_Items_StockItemId""
                        FOREIGN KEY (""StockItemId"") REFERENCES ""Items""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Nests_ProductionBatchId""
                ON ""Nests"" (""ProductionBatchId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Nests_StockItemId""
                ON ""Nests"" (""StockItemId"");
            ");

            // ---------- 4) ProcessBatches subtype ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProcessBatches"" (
                    ""Id""                          SERIAL PRIMARY KEY,
                    ""ProductionBatchId""           integer      NOT NULL,
                    ""ProcessType""                 smallint     NOT NULL DEFAULT 0,
                    ""SetpointTempC""               numeric(8,2) NULL,
                    ""SoakTimeMinutes""             integer      NULL,
                    ""QuenchMedium""                varchar(32)  NULL,
                    ""AtmosphereType""              varchar(32)  NULL,
                    ""ColorCode""                   varchar(32)  NULL,
                    ""PaintBatchLotItemId""         integer      NULL,
                    ""ChemistrySpec""               varchar(64)  NULL,
                    ""TankConcentrationPct""        numeric(6,3) NULL,
                    ""BathPh""                      numeric(4,2) NULL,
                    ""LoadMassKg""                  numeric(10,3) NULL,
                    ""HeatTreatChartUrl""           varchar(500) NULL,
                    ""WitnessCouponLotId""          integer      NULL,
                    ""RackPositionNotes""           varchar(256) NULL,
                    ""CreatedAt""                   timestamptz  NOT NULL DEFAULT now(),
                    ""UpdatedAt""                   timestamptz  NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProcessBatches_ProductionBatches_ProductionBatchId') THEN
                        ALTER TABLE ""ProcessBatches""
                        ADD CONSTRAINT ""FK_ProcessBatches_ProductionBatches_ProductionBatchId""
                        FOREIGN KEY (""ProductionBatchId"") REFERENCES ""ProductionBatches""(""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProcessBatches_ProductionBatchId""
                ON ""ProcessBatches"" (""ProductionBatchId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProcessBatches_ProcessType""
                ON ""ProcessBatches"" (""ProcessType"");
            ");

            // ---------- 5) ProductionBatchAllocations ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProductionBatchAllocations"" (
                    ""Id""                            SERIAL PRIMARY KEY,
                    ""ProductionBatchId""             integer      NOT NULL,
                    ""WorkOrderOperationId""          integer      NOT NULL,
                    ""ProductionOrderId""             integer      NULL,
                    ""ProductionOrderOperationId""    integer      NULL,
                    ""AllocationBasis""               numeric(18,4) NOT NULL DEFAULT 0,
                    ""AllocationPct""                 numeric(7,4) NOT NULL DEFAULT 0,
                    ""AllocatedCost""                 numeric(18,4) NULL,
                    ""Origin""                        smallint     NOT NULL DEFAULT 0,
                    ""CreatedAt""                     timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""                     varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatchAllocations_ProductionBatches_ProductionBatchId') THEN
                        ALTER TABLE ""ProductionBatchAllocations""
                        ADD CONSTRAINT ""FK_ProductionBatchAllocations_ProductionBatches_ProductionBatchId""
                        FOREIGN KEY (""ProductionBatchId"") REFERENCES ""ProductionBatches""(""Id"")
                        ON DELETE CASCADE;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatchAllocations_WorkOrderOperations_WorkOrderOperationId') THEN
                        ALTER TABLE ""ProductionBatchAllocations""
                        ADD CONSTRAINT ""FK_ProductionBatchAllocations_WorkOrderOperations_WorkOrderOperationId""
                        FOREIGN KEY (""WorkOrderOperationId"") REFERENCES ""WorkOrderOperations""(""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionBatchAllocations_BatchId_OperationId""
                ON ""ProductionBatchAllocations"" (""ProductionBatchId"", ""WorkOrderOperationId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatchAllocations_ProductionOrderId""
                ON ""ProductionBatchAllocations"" (""ProductionOrderId"");
            ");

            // ---------- 6) ProductionBatchEquipmentLinks ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProductionBatchEquipmentLinks"" (
                    ""Id""                     SERIAL PRIMARY KEY,
                    ""ProductionBatchId""      integer      NOT NULL,
                    ""EquipmentId""            integer      NOT NULL,
                    ""SequenceNo""             integer      NOT NULL DEFAULT 1,
                    ""Role""                   smallint     NOT NULL DEFAULT 0,
                    ""EnteredAt""              timestamptz  NULL,
                    ""ExitedAt""               timestamptz  NULL,
                    ""Notes""                  varchar(256) NULL,
                    ""CreatedAt""              timestamptz  NOT NULL DEFAULT now()
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatchEquipmentLinks_ProductionBatches_ProductionBatchId') THEN
                        ALTER TABLE ""ProductionBatchEquipmentLinks""
                        ADD CONSTRAINT ""FK_ProductionBatchEquipmentLinks_ProductionBatches_ProductionBatchId""
                        FOREIGN KEY (""ProductionBatchId"") REFERENCES ""ProductionBatches""(""Id"")
                        ON DELETE CASCADE;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatchEquipmentLinks_Assets_EquipmentId') THEN
                        ALTER TABLE ""ProductionBatchEquipmentLinks""
                        ADD CONSTRAINT ""FK_ProductionBatchEquipmentLinks_Assets_EquipmentId""
                        FOREIGN KEY (""EquipmentId"") REFERENCES ""Assets""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatchEquipmentLinks_BatchId_Sequence""
                ON ""ProductionBatchEquipmentLinks"" (""ProductionBatchId"", ""SequenceNo"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatchEquipmentLinks_EquipmentId""
                ON ""ProductionBatchEquipmentLinks"" (""EquipmentId"");
            ");

            // ---------- 7) ProductionBatchStateEvents ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProductionBatchStateEvents"" (
                    ""Id""                     SERIAL PRIMARY KEY,
                    ""ProductionBatchId""      integer      NOT NULL,
                    ""FromStatus""             smallint     NOT NULL,
                    ""ToStatus""               smallint     NOT NULL,
                    ""ChangedAt""              timestamptz  NOT NULL DEFAULT now(),
                    ""ChangedBy""              varchar(100) NULL,
                    ""Reason""                 varchar(500) NULL,
                    ""MrbDispositionId""       integer      NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatchStateEvents_ProductionBatches_ProductionBatchId') THEN
                        ALTER TABLE ""ProductionBatchStateEvents""
                        ADD CONSTRAINT ""FK_ProductionBatchStateEvents_ProductionBatches_ProductionBatchId""
                        FOREIGN KEY (""ProductionBatchId"") REFERENCES ""ProductionBatches""(""Id"")
                        ON DELETE CASCADE;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionBatchStateEvents_MrbDispositions_MrbDispositionId') THEN
                        ALTER TABLE ""ProductionBatchStateEvents""
                        ADD CONSTRAINT ""FK_ProductionBatchStateEvents_MrbDispositions_MrbDispositionId""
                        FOREIGN KEY (""MrbDispositionId"") REFERENCES ""MrbDispositions""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatchStateEvents_BatchId_ChangedAt""
                ON ""ProductionBatchStateEvents"" (""ProductionBatchId"", ""ChangedAt"");
            ");

            // ---------- 8) WorkOrderOperations extension columns ----------
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderOperations""
                ADD COLUMN IF NOT EXISTS ""BatchPoolCode""        varchar(64)  NULL,
                ADD COLUMN IF NOT EXISTS ""ProductionBatchId""    integer      NULL,
                ADD COLUMN IF NOT EXISTS ""BatchSequenceNo""      integer      NULL;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_WorkOrderOperations_ProductionBatches_ProductionBatchId') THEN
                        ALTER TABLE ""WorkOrderOperations""
                        ADD CONSTRAINT ""FK_WorkOrderOperations_ProductionBatches_ProductionBatchId""
                        FOREIGN KEY (""ProductionBatchId"") REFERENCES ""ProductionBatches""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkOrderOperations_ProductionBatchId""
                ON ""WorkOrderOperations"" (""ProductionBatchId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkOrderOperations_BatchPoolCode""
                ON ""WorkOrderOperations"" (""BatchPoolCode"");
            ");

            // ---------- 9) Schema cleanup of PR #119.12 placeholders ----------

            // Drop CutListId column from ProductionJobShopDetails. Cut-list
            // lookups go via CutListLine.SourceProductionOrderId once the
            // CutListLines table lands in PR #119.13b.
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProductionJobShopDetails""
                DROP COLUMN IF EXISTS ""CutListId"";
            ");

            // FK-wire NestPlanId -> Nests(Id).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionJobShopDetails_Nests_NestPlanId') THEN
                        ALTER TABLE ""ProductionJobShopDetails""
                        ADD CONSTRAINT ""FK_ProductionJobShopDetails_Nests_NestPlanId""
                        FOREIGN KEY (""NestPlanId"") REFERENCES ""Nests""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 9) JobShop FK rollback (CutListId column is not restored — it
            //    was a placeholder, no data lived there)
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionJobShopDetails"" DROP CONSTRAINT IF EXISTS ""FK_ProductionJobShopDetails_Nests_NestPlanId"";");

            // 8) WorkOrderOperations rollback
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderOperations_BatchPoolCode"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderOperations_ProductionBatchId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""WorkOrderOperations"" DROP CONSTRAINT IF EXISTS ""FK_WorkOrderOperations_ProductionBatches_ProductionBatchId"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderOperations""
                DROP COLUMN IF EXISTS ""BatchSequenceNo"",
                DROP COLUMN IF EXISTS ""ProductionBatchId"",
                DROP COLUMN IF EXISTS ""BatchPoolCode"";
            ");

            // 7) ProductionBatchStateEvents
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatchStateEvents_BatchId_ChangedAt"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProductionBatchStateEvents"";");

            // 6) ProductionBatchEquipmentLinks
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatchEquipmentLinks_EquipmentId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatchEquipmentLinks_BatchId_Sequence"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProductionBatchEquipmentLinks"";");

            // 5) ProductionBatchAllocations
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatchAllocations_ProductionOrderId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatchAllocations_BatchId_OperationId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProductionBatchAllocations"";");

            // 4) ProcessBatches
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProcessBatches_ProcessType"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProcessBatches_ProductionBatchId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProcessBatches"";");

            // 3) Nests
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Nests_StockItemId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Nests_ProductionBatchId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Nests"";");

            // 2) ProductionBatches
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_ScheduledStartAt"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_BatchPoolCode"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_BatchType"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_BatchNumber"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProductionBatches"";");

            // 1) Stubs
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MrbDispositions_Outcome"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MrbDispositions_DispositionNumber"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MrbDispositions"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RecipeRevisions_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RecipeRevisions_Name_Version"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RecipeRevisions"";");
        }
    }
}
