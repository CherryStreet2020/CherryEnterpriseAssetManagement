using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-013 / PR #119.13b — Phase E.2 sheet & material traceability.
    //
    // Lands the physical-lot layer on top of PR #119.13a's batch backbone.
    // This is where heat number / mill cert / source PO data lives —
    // NOT on the Items master per Dean's correction.
    //
    // Tables created (4):
    //   - MaterialMasters    — reference table (ShopCode UNIQUE +
    //                          AstmDesignation structured)
    //   - StockReceipts      — physical-lot record (one per received
    //                          sheet, heat number / mill cert / source PO)
    //   - Remnants           — re-usable offcut child of StockReceipt
    //   - CutListLines       — to-cut queue per part-quantity request
    //
    // Columns added to existing Nests:
    //   - StockReceiptId     int? NULL, FK SET NULL — physical sheet ref
    //
    // Reference: PR #119.13a research report Q1-Q3 + ADR-013 §"Recommendation".
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddSheetTraceabilityLayer")]
    public partial class AddSheetTraceabilityLayer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1) MaterialMasters ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""MaterialMasters"" (
                    ""Id""                  SERIAL PRIMARY KEY,
                    ""ShopCode""            varchar(64)  NOT NULL,
                    ""AstmDesignation""     varchar(64)  NULL,
                    ""Description""         varchar(200) NULL,
                    ""Form""                smallint     NOT NULL DEFAULT 0,
                    ""DensityKgPerM3""      numeric(10,4) NULL,
                    ""IsAnisotropic""       boolean      NOT NULL DEFAULT false,
                    ""CreatedAt""           timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""           varchar(100) NULL,
                    ""ModifiedAt""          timestamptz  NULL,
                    ""ModifiedBy""          varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialMasters_ShopCode""
                ON ""MaterialMasters"" (""ShopCode"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialMasters_AstmDesignation""
                ON ""MaterialMasters"" (""AstmDesignation"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialMasters_Form""
                ON ""MaterialMasters"" (""Form"");
            ");

            // ---------- 2) StockReceipts ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""StockReceipts"" (
                    ""Id""                     SERIAL PRIMARY KEY,
                    ""ReceiptNumber""          varchar(32)  NOT NULL,
                    ""ItemId""                 integer      NOT NULL,
                    ""MaterialMasterId""       integer      NULL,
                    ""HeatNumber""             varchar(64)  NULL,
                    ""LotNumber""              varchar(64)  NULL,
                    ""MillCertUrl""            varchar(500) NULL,
                    ""Mill""                   varchar(128) NULL,
                    ""SourcePoNumber""         varchar(64)  NULL,
                    ""SourcePoLineId""         varchar(64)  NULL,
                    ""ReceivedAt""             timestamptz  NOT NULL DEFAULT now(),
                    ""ReceivedByUserId""       integer      NULL,
                    ""LocationId""             integer      NULL,
                    ""LengthMm""               numeric(10,2) NULL,
                    ""WidthMm""                numeric(10,2) NULL,
                    ""ThicknessMm""            numeric(10,2) NULL,
                    ""UsableLengthMm""         numeric(10,2) NULL,
                    ""UsableWidthMm""          numeric(10,2) NULL,
                    ""QuantityReceived""       numeric(18,4) NOT NULL DEFAULT 0,
                    ""QuantityRemaining""      numeric(18,4) NOT NULL DEFAULT 0,
                    ""Uom""                    varchar(16)  NULL,
                    ""Status""                 smallint     NOT NULL DEFAULT 0,
                    ""QuarantineReason""       varchar(500) NULL,
                    ""Notes""                  varchar(2000) NULL,
                    ""CreatedAt""              timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""              varchar(100) NULL,
                    ""ModifiedAt""             timestamptz  NULL,
                    ""ModifiedBy""             varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_StockReceipts_Items_ItemId') THEN
                        ALTER TABLE ""StockReceipts""
                        ADD CONSTRAINT ""FK_StockReceipts_Items_ItemId""
                        FOREIGN KEY (""ItemId"") REFERENCES ""Items""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_StockReceipts_MaterialMasters_MaterialMasterId') THEN
                        ALTER TABLE ""StockReceipts""
                        ADD CONSTRAINT ""FK_StockReceipts_MaterialMasters_MaterialMasterId""
                        FOREIGN KEY (""MaterialMasterId"") REFERENCES ""MaterialMasters""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_StockReceipts_Users_ReceivedByUserId') THEN
                        ALTER TABLE ""StockReceipts""
                        ADD CONSTRAINT ""FK_StockReceipts_Users_ReceivedByUserId""
                        FOREIGN KEY (""ReceivedByUserId"") REFERENCES ""Users""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_StockReceipts_Locations_LocationId') THEN
                        ALTER TABLE ""StockReceipts""
                        ADD CONSTRAINT ""FK_StockReceipts_Locations_LocationId""
                        FOREIGN KEY (""LocationId"") REFERENCES ""Locations""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_StockReceipts_ReceiptNumber""
                ON ""StockReceipts"" (""ReceiptNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_HeatNumber""
                ON ""StockReceipts"" (""HeatNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_LotNumber""
                ON ""StockReceipts"" (""LotNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_ItemId""
                ON ""StockReceipts"" (""ItemId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Status""
                ON ""StockReceipts"" (""Status"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_ReceivedAt""
                ON ""StockReceipts"" (""ReceivedAt"");
            ");

            // ---------- 3) Remnants ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Remnants"" (
                    ""Id""                  SERIAL PRIMARY KEY,
                    ""RemnantNumber""       varchar(32)  NOT NULL,
                    ""ParentReceiptId""     integer      NOT NULL,
                    ""ParentNestId""        integer      NULL,
                    ""MaterialMasterId""    integer      NULL,
                    ""HeatNumber""          varchar(64)  NULL,
                    ""LengthMm""            numeric(10,2) NULL,
                    ""WidthMm""             numeric(10,2) NULL,
                    ""ThicknessMm""         numeric(10,2) NULL,
                    ""LocationId""          integer      NULL,
                    ""Status""              smallint     NOT NULL DEFAULT 0,
                    ""ConsumedByNestId""    integer      NULL,
                    ""ConsumedAt""          timestamptz  NULL,
                    ""Notes""               varchar(500) NULL,
                    ""CreatedAt""           timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""           varchar(100) NULL,
                    ""ModifiedAt""          timestamptz  NULL,
                    ""ModifiedBy""          varchar(100) NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Remnants_StockReceipts_ParentReceiptId') THEN
                        ALTER TABLE ""Remnants""
                        ADD CONSTRAINT ""FK_Remnants_StockReceipts_ParentReceiptId""
                        FOREIGN KEY (""ParentReceiptId"") REFERENCES ""StockReceipts""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Remnants_Nests_ParentNestId') THEN
                        ALTER TABLE ""Remnants""
                        ADD CONSTRAINT ""FK_Remnants_Nests_ParentNestId""
                        FOREIGN KEY (""ParentNestId"") REFERENCES ""Nests""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Remnants_Nests_ConsumedByNestId') THEN
                        ALTER TABLE ""Remnants""
                        ADD CONSTRAINT ""FK_Remnants_Nests_ConsumedByNestId""
                        FOREIGN KEY (""ConsumedByNestId"") REFERENCES ""Nests""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Remnants_MaterialMasters_MaterialMasterId') THEN
                        ALTER TABLE ""Remnants""
                        ADD CONSTRAINT ""FK_Remnants_MaterialMasters_MaterialMasterId""
                        FOREIGN KEY (""MaterialMasterId"") REFERENCES ""MaterialMasters""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Remnants_Locations_LocationId') THEN
                        ALTER TABLE ""Remnants""
                        ADD CONSTRAINT ""FK_Remnants_Locations_LocationId""
                        FOREIGN KEY (""LocationId"") REFERENCES ""Locations""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Remnants_RemnantNumber""
                ON ""Remnants"" (""RemnantNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Remnants_HeatNumber""
                ON ""Remnants"" (""HeatNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Remnants_ParentReceiptId""
                ON ""Remnants"" (""ParentReceiptId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Remnants_Status""
                ON ""Remnants"" (""Status"");
            ");

            // ---------- 4) CutListLines ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""CutListLines"" (
                    ""Id""                          SERIAL PRIMARY KEY,
                    ""ItemId""                      integer      NOT NULL,
                    ""NestId""                      integer      NULL,
                    ""SourceProductionOrderId""     integer      NULL,
                    ""MaterialMasterId""            integer      NULL,
                    ""Quantity""                    numeric(10,2) NOT NULL DEFAULT 1,
                    ""LengthMm""                    numeric(10,2) NULL,
                    ""WidthMm""                     numeric(10,2) NULL,
                    ""ThicknessMm""                 numeric(10,2) NULL,
                    ""GrainDirection""              smallint     NOT NULL DEFAULT 0,
                    ""CommonLineGroup""             varchar(32)  NULL,
                    ""Priority""                    integer      NOT NULL DEFAULT 50,
                    ""Status""                      smallint     NOT NULL DEFAULT 0,
                    ""DueDate""                     timestamptz  NULL,
                    ""CutAt""                       timestamptz  NULL,
                    ""Notes""                       varchar(500) NULL,
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
                        WHERE conname = 'FK_CutListLines_Items_ItemId') THEN
                        ALTER TABLE ""CutListLines""
                        ADD CONSTRAINT ""FK_CutListLines_Items_ItemId""
                        FOREIGN KEY (""ItemId"") REFERENCES ""Items""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_CutListLines_Nests_NestId') THEN
                        ALTER TABLE ""CutListLines""
                        ADD CONSTRAINT ""FK_CutListLines_Nests_NestId""
                        FOREIGN KEY (""NestId"") REFERENCES ""Nests""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_CutListLines_ProductionOrders_SourceProductionOrderId') THEN
                        ALTER TABLE ""CutListLines""
                        ADD CONSTRAINT ""FK_CutListLines_ProductionOrders_SourceProductionOrderId""
                        FOREIGN KEY (""SourceProductionOrderId"") REFERENCES ""ProductionOrders""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_CutListLines_MaterialMasters_MaterialMasterId') THEN
                        ALTER TABLE ""CutListLines""
                        ADD CONSTRAINT ""FK_CutListLines_MaterialMasters_MaterialMasterId""
                        FOREIGN KEY (""MaterialMasterId"") REFERENCES ""MaterialMasters""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CutListLines_ItemId""
                ON ""CutListLines"" (""ItemId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CutListLines_NestId_Status""
                ON ""CutListLines"" (""NestId"", ""Status"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CutListLines_SourceProductionOrderId_Status""
                ON ""CutListLines"" (""SourceProductionOrderId"", ""Status"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CutListLines_Status""
                ON ""CutListLines"" (""Status"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CutListLines_Priority""
                ON ""CutListLines"" (""Priority"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CutListLines_DueDate""
                ON ""CutListLines"" (""DueDate"");
            ");

            // ---------- 5) Nests.StockReceiptId column + FK ----------
            migrationBuilder.Sql(@"
                ALTER TABLE ""Nests""
                ADD COLUMN IF NOT EXISTS ""StockReceiptId"" integer NULL;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Nests_StockReceipts_StockReceiptId') THEN
                        ALTER TABLE ""Nests""
                        ADD CONSTRAINT ""FK_Nests_StockReceipts_StockReceiptId""
                        FOREIGN KEY (""StockReceiptId"") REFERENCES ""StockReceipts""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Nests_StockReceiptId""
                ON ""Nests"" (""StockReceiptId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 5) Nests column rollback
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Nests_StockReceiptId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Nests"" DROP CONSTRAINT IF EXISTS ""FK_Nests_StockReceipts_StockReceiptId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Nests"" DROP COLUMN IF EXISTS ""StockReceiptId"";");

            // 4) CutListLines
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CutListLines_DueDate"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CutListLines_Priority"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CutListLines_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CutListLines_SourceProductionOrderId_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CutListLines_NestId_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CutListLines_ItemId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""CutListLines"";");

            // 3) Remnants
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Remnants_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Remnants_ParentReceiptId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Remnants_HeatNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Remnants_RemnantNumber"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Remnants"";");

            // 2) StockReceipts
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_ReceivedAt"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_ItemId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_LotNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_HeatNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_ReceiptNumber"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""StockReceipts"";");

            // 1) MaterialMasters
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialMasters_Form"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialMasters_AstmDesignation"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialMasters_ShopCode"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MaterialMasters"";");
        }
    }
}
