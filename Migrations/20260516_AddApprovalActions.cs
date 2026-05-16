using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 2 PR #115 — Approval Hierarchy + SoD.
    //
    // Immutable decision-log table. Every approve/reject action against a
    // PurchaseOrder / VendorInvoice / etc. is one row. Indexed on the
    // (TargetEntityType, TargetEntityId) tuple so the SoD enforcement and
    // pending-queue queries hit the index.
    //
    // CreatorUserId is stored at decision time (not as a FK) because EF's
    // ASP.NET Identity user PK is a string column. We index it to make the
    // "did this user create this doc" SoD check fast.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_AddApprovalActions")]
    public partial class AddApprovalActions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ApprovalActions"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""TargetEntityType"" character varying(50) NOT NULL,
                    ""TargetEntityId"" integer NOT NULL,
                    ""ApprovalWorkflowId"" integer NULL,
                    ""StepNumber"" integer NOT NULL DEFAULT 1,
                    ""Decision"" integer NOT NULL,
                    ""DecidedByUserId"" character varying(450) NOT NULL,
                    ""DecidedByUsername"" character varying(256) NOT NULL,
                    ""DecidedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""Comment"" character varying(1000) NULL,
                    ""ApproverRole"" character varying(100) NULL,
                    ""CompanyId"" integer NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ApprovalActions_Target""
                ON ""ApprovalActions"" (""TargetEntityType"", ""TargetEntityId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ApprovalActions_DecidedByUserId""
                ON ""ApprovalActions"" (""DecidedByUserId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ApprovalActions_ApprovalWorkflowId""
                ON ""ApprovalActions"" (""ApprovalWorkflowId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ApprovalActions_CompanyId""
                ON ""ApprovalActions"" (""CompanyId"");
            ");

            // FK constraints — ON DELETE SET NULL so deleting a workflow
            // doesn't wipe the historical decision (audit-preserving).
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApprovalActions""
                ADD CONSTRAINT ""FK_ApprovalActions_ApprovalWorkflow""
                FOREIGN KEY (""ApprovalWorkflowId"") REFERENCES ""ApprovalWorkflows""(""Id"")
                ON DELETE SET NULL;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApprovalActions""
                ADD CONSTRAINT ""FK_ApprovalActions_Company""
                FOREIGN KEY (""CompanyId"") REFERENCES ""Companies""(""Id"")
                ON DELETE SET NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ApprovalActions"";");
        }
    }
}
