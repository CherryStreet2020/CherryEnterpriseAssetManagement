using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.8 — CipWorkOrderDetails satellite.
    //
    // Per-classification satellite that holds CIP-only fields so the
    // unified WorkOrder header doesn't carry columns that are always
    // NULL for non-CIP classifications.
    //
    // Relationship:
    //   - 1:0..1 with WorkOrder
    //   - UNIQUE on WorkOrderId enforces "one CIP details row per WO"
    //   - ON DELETE CASCADE — satellite is owned by the WO
    //
    // ON DELETE SET NULL on TargetFixedAssetId because deleting the
    // capitalized asset shouldn't destroy the CIP audit history (which
    // still wants to know that capitalization once pointed at that
    // asset, even if the asset row has been hard-deleted).
    //
    // Indexes:
    //   - UQ on WorkOrderId — the 1:0..1 enforcer + the hot-path lookup
    //   - IX on AfeNumber — finance team filter
    //   - IX on Stage — operator dashboard filter ("show me everything
    //     in Construction or Commissioning")
    //   - IX on TargetFixedAssetId — reverse lookup ("what CIP funded
    //     this asset?")
    //
    // Source standards: ASC 360-10, ASC 835-20.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddCipWorkOrderDetails")]
    public partial class AddCipWorkOrderDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""CipWorkOrderDetails"" (
                    ""Id""                          SERIAL PRIMARY KEY,
                    ""WorkOrderId""                 integer       NOT NULL,
                    ""AfeNumber""                   varchar(32)   NOT NULL,
                    ""GlCipSubAccount""             varchar(32)   NULL,
                    ""ApprovedBudget""              numeric(18,2) NULL,
                    ""CapitalizedInterest""         numeric(18,2) NULL,
                    ""SubstantialCompletionDate""   timestamptz   NULL,
                    ""InServiceDate""               timestamptz   NULL,
                    ""UsefulLifeMonths""            integer       NULL,
                    ""DepreciationMethod""          smallint      NOT NULL DEFAULT 0,
                    ""TargetFixedAssetId""          integer       NULL,
                    ""Stage""                       smallint      NOT NULL DEFAULT 0,
                    ""ChangeOrderCount""            integer       NOT NULL DEFAULT 0,
                    ""RetainagePercent""            numeric(5,2)  NULL,
                    ""JvPartnerSplits""             jsonb         NULL,
                    ""RegulatoryAuthority""         varchar(40)   NULL,
                    ""CreatedAt""                   timestamptz   NOT NULL DEFAULT now(),
                    ""UpdatedAt""                   timestamptz   NULL
                );
            ");

            // FK to WorkOrder. CASCADE delete — satellite is owned.
            migrationBuilder.Sql(@"
                ALTER TABLE ""CipWorkOrderDetails""
                ADD CONSTRAINT ""FK_CipWorkOrderDetails_WorkOrders_WorkOrderId""
                FOREIGN KEY (""WorkOrderId"") REFERENCES ""WorkOrders""(""Id"")
                ON DELETE CASCADE;
            ");

            // Optional FK to Asset (capitalization target). SET NULL on
            // delete to preserve CIP audit history.
            migrationBuilder.Sql(@"
                ALTER TABLE ""CipWorkOrderDetails""
                ADD CONSTRAINT ""FK_CipWorkOrderDetails_Assets_TargetFixedAssetId""
                FOREIGN KEY (""TargetFixedAssetId"") REFERENCES ""Assets""(""Id"")
                ON DELETE SET NULL;
            ");

            // UNIQUE on WorkOrderId — 1:0..1 enforcer + hot-path lookup.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CipWorkOrderDetails_WorkOrderId""
                ON ""CipWorkOrderDetails"" (""WorkOrderId"");
            ");

            // Finance-team filter.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CipWorkOrderDetails_AfeNumber""
                ON ""CipWorkOrderDetails"" (""AfeNumber"");
            ");

            // Operator-dashboard filter (filter by lifecycle Stage).
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CipWorkOrderDetails_Stage""
                ON ""CipWorkOrderDetails"" (""Stage"");
            ");

            // Reverse lookup — "what CIP funded this asset?"
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_CipWorkOrderDetails_TargetFixedAssetId""
                ON ""CipWorkOrderDetails"" (""TargetFixedAssetId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CipWorkOrderDetails_TargetFixedAssetId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CipWorkOrderDetails_Stage"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CipWorkOrderDetails_AfeNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CipWorkOrderDetails_WorkOrderId"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""CipWorkOrderDetails""
                DROP CONSTRAINT IF EXISTS ""FK_CipWorkOrderDetails_Assets_TargetFixedAssetId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""CipWorkOrderDetails""
                DROP CONSTRAINT IF EXISTS ""FK_CipWorkOrderDetails_WorkOrders_WorkOrderId"";
            ");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""CipWorkOrderDetails"";");
        }
    }
}
