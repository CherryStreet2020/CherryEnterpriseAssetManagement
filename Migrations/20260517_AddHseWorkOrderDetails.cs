using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.11 — HseWorkOrderDetails satellite.
    //
    // Fourth and final classification satellite. Holds OSHA recordability,
    // ANSI Z10 risk-matrix scoring, JSA steps (jsonb), regulatory
    // notifications (jsonb), and DART-rate inputs (DaysAway,
    // DaysRestricted) for Classification=HSE work orders.
    //
    // Relationship:
    //   - 1:0..1 with WorkOrder via UNIQUE on WorkOrderId, ON DELETE CASCADE
    //   - No self-links (HSE incidents don't currently link to other WOs;
    //     a future PR could add LinkedRootCauseWorkOrderId for engineering
    //     remediation of an HSE finding)
    //
    // Source standards: ISO 45001, OSHA 29 CFR 1904, OSHA 3071 (JSA),
    // OSHA ITA (1904.41), ANSI Z10-2019.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddHseWorkOrderDetails")]
    public partial class AddHseWorkOrderDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""HseWorkOrderDetails"" (
                    ""Id""                              SERIAL PRIMARY KEY,
                    ""WorkOrderId""                     integer     NOT NULL,
                    ""HseIssueType""                    smallint    NOT NULL DEFAULT 1,
                    ""OshaCaseNumber""                  varchar(32) NULL,
                    ""RecordabilityClass""              smallint    NOT NULL DEFAULT 0,
                    ""HazardSeverity""                  smallint    NOT NULL DEFAULT 2,
                    ""Likelihood""                      smallint    NOT NULL DEFAULT 2,
                    ""RiskScore""                       integer     NOT NULL DEFAULT 4,
                    ""EmployeesAffected""               integer     NULL,
                    ""BodyPartAffected""                varchar(64) NULL,
                    ""InjuryType""                      varchar(80) NULL,
                    ""DaysAway""                        integer     NULL,
                    ""DaysRestricted""                  integer     NULL,
                    ""LostTimeIncident""                boolean     NOT NULL DEFAULT false,
                    ""OshaItaSubmissionRequired""       boolean     NOT NULL DEFAULT false,
                    ""RegulatoryNotifications""         jsonb       NULL,
                    ""JsaSteps""                        jsonb       NULL,
                    ""WitnessStatementsUrl""            varchar(500) NULL,
                    ""PhotosUrl""                       varchar(500) NULL,
                    ""CreatedAt""                       timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""                       timestamptz NULL
                );
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""HseWorkOrderDetails""
                ADD CONSTRAINT ""FK_HseWorkOrderDetails_WorkOrders_WorkOrderId""
                FOREIGN KEY (""WorkOrderId"") REFERENCES ""WorkOrders""(""Id"")
                ON DELETE CASCADE;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_HseWorkOrderDetails_WorkOrderId""
                ON ""HseWorkOrderDetails"" (""WorkOrderId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_HseWorkOrderDetails_OshaCaseNumber""
                ON ""HseWorkOrderDetails"" (""OshaCaseNumber"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_HseWorkOrderDetails_HseIssueType""
                ON ""HseWorkOrderDetails"" (""HseIssueType"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_HseWorkOrderDetails_RecordabilityClass""
                ON ""HseWorkOrderDetails"" (""RecordabilityClass"");
            ");

            // Highest-risk-first queue ordering.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_HseWorkOrderDetails_RiskScore""
                ON ""HseWorkOrderDetails"" (""RiskScore"" DESC);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_HseWorkOrderDetails_RiskScore"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_HseWorkOrderDetails_RecordabilityClass"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_HseWorkOrderDetails_HseIssueType"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_HseWorkOrderDetails_OshaCaseNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_HseWorkOrderDetails_WorkOrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""HseWorkOrderDetails"" DROP CONSTRAINT IF EXISTS ""FK_HseWorkOrderDetails_WorkOrders_WorkOrderId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""HseWorkOrderDetails"";");
        }
    }
}
