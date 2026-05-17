using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.10 — EngineeringWorkOrderDetails satellite.
    //
    // ECO + MOC + PSSR + BOM-revision + engineering-CAPA fields for
    // Classification=Engineering work orders. The MOC checklist
    // (PshUpdated / OperatingProceduresUpdated / TrainingRequired) and
    // PSSR completion flag map directly to OSHA 29 CFR 1910.119(l) +
    // (i) requirements.
    //
    // Relationship:
    //   - 1:0..1 with WorkOrder via UNIQUE on WorkOrderId, ON DELETE CASCADE
    //   - LinkedNcrWorkOrderId: optional self-FK to WorkOrders (the
    //     Quality NCR that triggered this CAPA), ON DELETE SET NULL
    //
    // Source standards: ASME Y14.35, OSHA 29 CFR 1910.119(l) + (i),
    // ISO 9001 Cl. 8.5.6, AS9100D Cl. 8.5.6.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddEngineeringWorkOrderDetails")]
    public partial class AddEngineeringWorkOrderDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""EngineeringWorkOrderDetails"" (
                    ""Id""                                  SERIAL PRIMARY KEY,
                    ""WorkOrderId""                         integer     NOT NULL,
                    ""EcoNumber""                           varchar(32) NULL,
                    ""EngineeringIssueType""                smallint    NOT NULL DEFAULT 0,
                    ""ChangeTypeFFF""                       smallint    NOT NULL DEFAULT 3,
                    ""RiskLevel""                           smallint    NOT NULL DEFAULT 0,
                    ""IsReplacementInKind""                 boolean     NOT NULL DEFAULT false,
                    ""MocPshUpdated""                       boolean     NOT NULL DEFAULT false,
                    ""MocOperatingProceduresUpdated""       boolean     NOT NULL DEFAULT false,
                    ""MocTrainingRequired""                 boolean     NOT NULL DEFAULT false,
                    ""PssrCompleted""                       boolean     NOT NULL DEFAULT false,
                    ""PssrCompletedAt""                     timestamptz NULL,
                    ""LinkedNcrWorkOrderId""                integer     NULL,
                    ""EffectiveDate""                       timestamptz NULL,
                    ""CutInSerial""                         varchar(64) NULL,
                    ""RegulatoryReview""                    boolean     NOT NULL DEFAULT false,
                    ""AffectedItems""                       jsonb       NULL,
                    ""CreatedAt""                           timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""                           timestamptz NULL
                );
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""EngineeringWorkOrderDetails""
                ADD CONSTRAINT ""FK_EngineeringWorkOrderDetails_WorkOrders_WorkOrderId""
                FOREIGN KEY (""WorkOrderId"") REFERENCES ""WorkOrders""(""Id"")
                ON DELETE CASCADE;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""EngineeringWorkOrderDetails""
                ADD CONSTRAINT ""FK_EngineeringWorkOrderDetails_WorkOrders_LinkedNcrWorkOrderId""
                FOREIGN KEY (""LinkedNcrWorkOrderId"") REFERENCES ""WorkOrders""(""Id"")
                ON DELETE SET NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EngineeringWorkOrderDetails_WorkOrderId""
                ON ""EngineeringWorkOrderDetails"" (""WorkOrderId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_EngineeringWorkOrderDetails_EcoNumber""
                ON ""EngineeringWorkOrderDetails"" (""EcoNumber"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_EngineeringWorkOrderDetails_EngineeringIssueType""
                ON ""EngineeringWorkOrderDetails"" (""EngineeringIssueType"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_EngineeringWorkOrderDetails_LinkedNcrWorkOrderId""
                ON ""EngineeringWorkOrderDetails"" (""LinkedNcrWorkOrderId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_EngineeringWorkOrderDetails_LinkedNcrWorkOrderId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_EngineeringWorkOrderDetails_EngineeringIssueType"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_EngineeringWorkOrderDetails_EcoNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_EngineeringWorkOrderDetails_WorkOrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EngineeringWorkOrderDetails"" DROP CONSTRAINT IF EXISTS ""FK_EngineeringWorkOrderDetails_WorkOrders_LinkedNcrWorkOrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EngineeringWorkOrderDetails"" DROP CONSTRAINT IF EXISTS ""FK_EngineeringWorkOrderDetails_WorkOrders_WorkOrderId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""EngineeringWorkOrderDetails"";");
        }
    }
}
