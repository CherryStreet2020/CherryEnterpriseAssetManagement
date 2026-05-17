using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.9 — QualityWorkOrderDetails satellite.
    //
    // Quality-only fields for NCRs, CAPAs, audit findings, customer
    // complaints, and supplier issues. Carries the full 8D record
    // (D0..D8 free-text body) and the two self-FKs that link an NCR
    // to its CAPA and vice versa.
    //
    // Relationship:
    //   - 1:0..1 with WorkOrder via UNIQUE on WorkOrderId, ON DELETE CASCADE
    //   - CapaWorkOrderId, LinkedNcrId: optional self-FKs to WorkOrders,
    //     both ON DELETE SET NULL (deleting one side preserves the audit
    //     trail on the other)
    //
    // Indexes:
    //   - UQ on WorkOrderId — 1:0..1 enforcer + hot-path lookup
    //   - IX on NcrNumber — QA filter
    //   - IX on DispositionCode — pending-disposition queue
    //   - IX on QualityIssueType — type filter on QA dashboard
    //   - IX on CapaWorkOrderId — NCR -> CAPA traversal
    //   - IX on LinkedNcrId — CAPA -> NCR traversal
    //
    // Source standards: ISO 9001 Cl. 8.7 + 10.2, FDA 21 CFR 820.90 + 820.100,
    // Ford G8D, IATF 16949, AS9100D.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddQualityWorkOrderDetails")]
    public partial class AddQualityWorkOrderDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""QualityWorkOrderDetails"" (
                    ""Id""                                  SERIAL PRIMARY KEY,
                    ""WorkOrderId""                         integer       NOT NULL,
                    ""NcrNumber""                           varchar(32)   NOT NULL,
                    ""QualityIssueType""                    smallint      NOT NULL DEFAULT 5,
                    ""Severity""                            smallint      NOT NULL DEFAULT 0,
                    ""Source""                              smallint      NOT NULL DEFAULT 0,
                    ""AffectedQuantity""                    numeric(18,4) NULL,
                    ""AffectedLotNumber""                   varchar(64)   NULL,
                    ""DispositionCode""                     smallint      NOT NULL DEFAULT 0,
                    ""RootCauseMethod""                     smallint      NOT NULL DEFAULT 0,
                    ""RootCauseCategory""                   smallint      NOT NULL DEFAULT 2,
                    ""CapaRequired""                        boolean       NOT NULL DEFAULT false,
                    ""CapaWorkOrderId""                     integer       NULL,
                    ""LinkedNcrId""                         integer       NULL,
                    ""EffectivenessVerificationDate""       timestamptz   NULL,
                    ""EffectivenessVerificationStatus""     smallint      NOT NULL DEFAULT 0,
                    ""RegulatoryReportable""                boolean       NOT NULL DEFAULT false,
                    ""D0PrepNotes""                         text          NULL,
                    ""D1Team""                              text          NULL,
                    ""D2ProblemDescription""                text          NULL,
                    ""D3ContainmentActions""                text          NULL,
                    ""D4RootCause""                         text          NULL,
                    ""D5PermanentCorrectiveActions""        text          NULL,
                    ""D6Implementation""                    text          NULL,
                    ""D7Prevention""                        text          NULL,
                    ""D8Recognition""                       text          NULL,
                    ""CreatedAt""                           timestamptz   NOT NULL DEFAULT now(),
                    ""UpdatedAt""                           timestamptz   NULL
                );
            ");

            // Owning FK — CASCADE.
            migrationBuilder.Sql(@"
                ALTER TABLE ""QualityWorkOrderDetails""
                ADD CONSTRAINT ""FK_QualityWorkOrderDetails_WorkOrders_WorkOrderId""
                FOREIGN KEY (""WorkOrderId"") REFERENCES ""WorkOrders""(""Id"")
                ON DELETE CASCADE;
            ");

            // Two optional self-links — both SET NULL on delete to preserve
            // the audit trail when the linked WO is removed.
            migrationBuilder.Sql(@"
                ALTER TABLE ""QualityWorkOrderDetails""
                ADD CONSTRAINT ""FK_QualityWorkOrderDetails_WorkOrders_CapaWorkOrderId""
                FOREIGN KEY (""CapaWorkOrderId"") REFERENCES ""WorkOrders""(""Id"")
                ON DELETE SET NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""QualityWorkOrderDetails""
                ADD CONSTRAINT ""FK_QualityWorkOrderDetails_WorkOrders_LinkedNcrId""
                FOREIGN KEY (""LinkedNcrId"") REFERENCES ""WorkOrders""(""Id"")
                ON DELETE SET NULL;
            ");

            // UNIQUE on WorkOrderId — 1:0..1 enforcer.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_QualityWorkOrderDetails_WorkOrderId""
                ON ""QualityWorkOrderDetails"" (""WorkOrderId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_QualityWorkOrderDetails_NcrNumber""
                ON ""QualityWorkOrderDetails"" (""NcrNumber"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_QualityWorkOrderDetails_DispositionCode""
                ON ""QualityWorkOrderDetails"" (""DispositionCode"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_QualityWorkOrderDetails_QualityIssueType""
                ON ""QualityWorkOrderDetails"" (""QualityIssueType"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_QualityWorkOrderDetails_CapaWorkOrderId""
                ON ""QualityWorkOrderDetails"" (""CapaWorkOrderId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_QualityWorkOrderDetails_LinkedNcrId""
                ON ""QualityWorkOrderDetails"" (""LinkedNcrId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_QualityWorkOrderDetails_LinkedNcrId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_QualityWorkOrderDetails_CapaWorkOrderId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_QualityWorkOrderDetails_QualityIssueType"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_QualityWorkOrderDetails_DispositionCode"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_QualityWorkOrderDetails_NcrNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_QualityWorkOrderDetails_WorkOrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""QualityWorkOrderDetails"" DROP CONSTRAINT IF EXISTS ""FK_QualityWorkOrderDetails_WorkOrders_LinkedNcrId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""QualityWorkOrderDetails"" DROP CONSTRAINT IF EXISTS ""FK_QualityWorkOrderDetails_WorkOrders_CapaWorkOrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""QualityWorkOrderDetails"" DROP CONSTRAINT IF EXISTS ""FK_QualityWorkOrderDetails_WorkOrders_WorkOrderId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""QualityWorkOrderDetails"";");
        }
    }
}
