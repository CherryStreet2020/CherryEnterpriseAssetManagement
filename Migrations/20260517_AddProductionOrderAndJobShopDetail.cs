using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-013 / PR #119.12 — Phase E.1.
    //
    // Lands the ProductionOrder header (sibling to WorkOrder), the
    // JobShop-only satellite, and the SAP PP02 outside-processing
    // extension columns on WorkOrderOperation.
    //
    // Tables created:
    //   - ProductionOrders            — sibling header, ProductionType
    //                                   discriminator, status machine,
    //                                   revision-chain self-FK
    //   - ProductionJobShopDetails    — 1:0..1 with ProductionOrders via
    //                                   UNIQUE on ProductionOrderId,
    //                                   ON DELETE CASCADE
    //
    // Columns added to existing WorkOrderOperations table:
    //   - IsExternal                  bool, default false
    //   - VendorId                    int?, FK Vendors(Id) ON DELETE SET NULL
    //   - AutoGeneratePR              bool, default false
    //   - VendorPoLineId              varchar(32)
    //   - VendorExpectedReturnDate    timestamptz
    //
    // What this PR does NOT do (PR #119.13 / #119.14):
    //   - CutListId / NestPlanId FKs on ProductionJobShopDetails (columns
    //     are placeholders here; FKs land with the entities)
    //   - MaterialStructure / Bom / Recipe / Nest tables
    //   - ProductionProcessDetail satellite
    //   - RegulatoryProfile config
    //
    // Idempotent throughout — CREATE IF NOT EXISTS, ADD COLUMN IF NOT
    // EXISTS, ADD CONSTRAINT inside a DO block, CREATE INDEX IF NOT
    // EXISTS — so a partial replay is safe.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddProductionOrderAndJobShopDetail")]
    public partial class AddProductionOrderAndJobShopDetail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1) ProductionOrders header ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProductionOrders"" (
                    ""Id""                          SERIAL PRIMARY KEY,
                    ""OrderNumber""                 varchar(32)   NOT NULL,
                    ""Type""                        smallint      NOT NULL DEFAULT 0,
                    ""Status""                      smallint      NOT NULL DEFAULT 0,
                    ""Title""                       varchar(200)  NOT NULL,
                    ""Description""                 varchar(2000) NULL,
                    ""ItemId""                      integer       NULL,
                    ""LocationId""                  integer       NULL,
                    ""CustomerId""                  integer       NULL,
                    ""QuantityOrdered""             numeric(18,4) NOT NULL DEFAULT 0,
                    ""QuantityCompleted""           numeric(18,4) NOT NULL DEFAULT 0,
                    ""QuantityScrapped""            numeric(18,4) NOT NULL DEFAULT 0,
                    ""Uom""                         varchar(16)   NULL,
                    ""ScheduledStart""              timestamptz   NULL,
                    ""ScheduledEnd""                timestamptz   NULL,
                    ""ActualStart""                 timestamptz   NULL,
                    ""ActualEnd""                   timestamptz   NULL,
                    ""Priority""                    integer       NOT NULL DEFAULT 50,
                    ""MasterProductionOrderId""     integer       NULL,
                    ""Revision""                    integer       NOT NULL DEFAULT 0,
                    ""CreatedAt""                   timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""                   varchar(100)  NULL,
                    ""ModifiedAt""                  timestamptz   NULL,
                    ""ModifiedBy""                  varchar(100)  NULL
                );
            ");

            // FKs added inside a DO block — IF NOT EXISTS lookup against
            // pg_constraint so partial replays don't double-add.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionOrders_ProductionOrders_MasterProductionOrderId'
                    ) THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT ""FK_ProductionOrders_ProductionOrders_MasterProductionOrderId""
                        FOREIGN KEY (""MasterProductionOrderId"") REFERENCES ""ProductionOrders""(""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionOrders_Items_ItemId'
                    ) THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT ""FK_ProductionOrders_Items_ItemId""
                        FOREIGN KEY (""ItemId"") REFERENCES ""Items""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionOrders_Locations_LocationId'
                    ) THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT ""FK_ProductionOrders_Locations_LocationId""
                        FOREIGN KEY (""LocationId"") REFERENCES ""Locations""(""Id"")
                        ON DELETE RESTRICT;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionOrders_Customers_CustomerId'
                    ) THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT ""FK_ProductionOrders_Customers_CustomerId""
                        FOREIGN KEY (""CustomerId"") REFERENCES ""Customers""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionOrders_OrderNumber""
                ON ""ProductionOrders"" (""OrderNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOrders_Type""
                ON ""ProductionOrders"" (""Type"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOrders_Status""
                ON ""ProductionOrders"" (""Status"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOrders_ScheduledStart""
                ON ""ProductionOrders"" (""ScheduledStart"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOrders_ScheduledEnd""
                ON ""ProductionOrders"" (""ScheduledEnd"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOrders_MasterProductionOrderId_Revision""
                ON ""ProductionOrders"" (""MasterProductionOrderId"", ""Revision"");
            ");

            // ---------- 2) ProductionJobShopDetails satellite ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProductionJobShopDetails"" (
                    ""Id""                          SERIAL PRIMARY KEY,
                    ""ProductionOrderId""           integer       NOT NULL,
                    ""CutListId""                   integer       NULL,
                    ""NestPlanId""                  integer       NULL,
                    ""DrawingNumber""               varchar(64)   NULL,
                    ""DrawingRevision""             varchar(16)   NULL,
                    ""HasOutsideOperations""        boolean       NOT NULL DEFAULT false,
                    ""OutsideOperationCount""       integer       NOT NULL DEFAULT 0,
                    ""MaterialIssueMethod""         smallint      NOT NULL DEFAULT 0,
                    ""SerializedOutput""            boolean       NOT NULL DEFAULT false,
                    ""QualityHoldOnCompletion""     boolean       NOT NULL DEFAULT false,
                    ""InspectionNotes""             varchar(1000) NULL,
                    ""PriorityRank""                integer       NULL,
                    ""CreatedAt""                   timestamptz   NOT NULL DEFAULT now(),
                    ""UpdatedAt""                   timestamptz   NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ProductionJobShopDetails_ProductionOrders_ProductionOrderId'
                    ) THEN
                        ALTER TABLE ""ProductionJobShopDetails""
                        ADD CONSTRAINT ""FK_ProductionJobShopDetails_ProductionOrders_ProductionOrderId""
                        FOREIGN KEY (""ProductionOrderId"") REFERENCES ""ProductionOrders""(""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionJobShopDetails_ProductionOrderId""
                ON ""ProductionJobShopDetails"" (""ProductionOrderId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionJobShopDetails_DrawingNumber""
                ON ""ProductionJobShopDetails"" (""DrawingNumber"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionJobShopDetails_PriorityRank""
                ON ""ProductionJobShopDetails"" (""PriorityRank"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionJobShopDetails_HasOutsideOperations""
                ON ""ProductionJobShopDetails"" (""HasOutsideOperations"");
            ");

            // ---------- 3) WorkOrderOperations outside-processing columns ----------
            // All five columns added IF NOT EXISTS so re-runs are safe.
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderOperations""
                ADD COLUMN IF NOT EXISTS ""IsExternal""               boolean      NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS ""VendorId""                 integer      NULL,
                ADD COLUMN IF NOT EXISTS ""AutoGeneratePR""           boolean      NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS ""VendorPoLineId""           varchar(32)  NULL,
                ADD COLUMN IF NOT EXISTS ""VendorExpectedReturnDate"" timestamptz  NULL;
            ");

            // FK to Vendors — ON DELETE SET NULL so vendor deletion
            // preserves the historical operation record.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_WorkOrderOperations_Vendors_VendorId'
                    ) THEN
                        ALTER TABLE ""WorkOrderOperations""
                        ADD CONSTRAINT ""FK_WorkOrderOperations_Vendors_VendorId""
                        FOREIGN KEY (""VendorId"") REFERENCES ""Vendors""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkOrderOperations_IsExternal""
                ON ""WorkOrderOperations"" (""IsExternal"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkOrderOperations_VendorId""
                ON ""WorkOrderOperations"" (""VendorId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 3) WorkOrderOperations columns
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderOperations_VendorId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderOperations_IsExternal"";");
            migrationBuilder.Sql(@"ALTER TABLE ""WorkOrderOperations"" DROP CONSTRAINT IF EXISTS ""FK_WorkOrderOperations_Vendors_VendorId"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderOperations""
                DROP COLUMN IF EXISTS ""VendorExpectedReturnDate"",
                DROP COLUMN IF EXISTS ""VendorPoLineId"",
                DROP COLUMN IF EXISTS ""AutoGeneratePR"",
                DROP COLUMN IF EXISTS ""VendorId"",
                DROP COLUMN IF EXISTS ""IsExternal"";
            ");

            // 2) ProductionJobShopDetails
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionJobShopDetails_HasOutsideOperations"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionJobShopDetails_PriorityRank"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionJobShopDetails_DrawingNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionJobShopDetails_ProductionOrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionJobShopDetails"" DROP CONSTRAINT IF EXISTS ""FK_ProductionJobShopDetails_ProductionOrders_ProductionOrderId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProductionJobShopDetails"";");

            // 1) ProductionOrders
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_MasterProductionOrderId_Revision"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_ScheduledEnd"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_ScheduledStart"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_Type"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_OrderNumber"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionOrders"" DROP CONSTRAINT IF EXISTS ""FK_ProductionOrders_Customers_CustomerId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionOrders"" DROP CONSTRAINT IF EXISTS ""FK_ProductionOrders_Locations_LocationId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionOrders"" DROP CONSTRAINT IF EXISTS ""FK_ProductionOrders_Items_ItemId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductionOrders"" DROP CONSTRAINT IF EXISTS ""FK_ProductionOrders_ProductionOrders_MasterProductionOrderId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProductionOrders"";");
        }
    }
}
